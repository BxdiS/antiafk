using System.Runtime.InteropServices;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;
using AntiAfk.Infrastructure.Win32;

namespace AntiAfk.Infrastructure.Services;

public sealed class InputService : IInputService
{
    // Virtual keys that live on the extended part of the keyboard. They share a scan code with a
    // numpad key, so without KEYEVENTF_EXTENDEDKEY the target sees the numpad key instead: VK_DOWN
    // maps to scan code 0x50, which is Numpad 2. Games reading raw scan codes (GTA V does) act on
    // the wrong key.
    private static readonly HashSet<ushort> ExtendedKeys =
    [
        0x21, 0x22, 0x23, 0x24, // PageUp, PageDown, End, Home
        0x25, 0x26, 0x27, 0x28, // Left, Up, Right, Down
        0x2D, 0x2E,             // Insert, Delete
        0x2C,                   // PrintScreen
        0x90,                   // NumLock
        0x6F,                   // Divide
        0xA3, 0xA5              // Right Ctrl, Right Alt
    ];

    // Time for a window to actually come to the front and take input focus after being raised.
    private static readonly TimeSpan ForegroundSettle = TimeSpan.FromMilliseconds(300);

    private readonly IWindowService _windowService;
    private readonly IAppLogger _logger;
    private readonly Func<TimingSettings> _timingsProvider;
    private readonly Random _random = new();
    private DateTime _lastClickFinishedUtc = DateTime.MinValue;

    public InputService(IWindowService windowService, IAppLogger logger, Func<TimingSettings> timingsProvider)
    {
        _windowService = windowService;
        _logger = logger;
        _timingsProvider = timingsProvider;
    }

    public void SendKey(ushort virtualKey, double durationSeconds)
    {
        var scanCode = (byte)NativeMethods.MapVirtualKey(virtualKey, 0);
        var extended = ExtendedKeys.Contains(virtualKey) ? NativeMethods.KeyeventfExtendedkey : 0u;

        NativeMethods.keybd_event((byte)virtualKey, scanCode, extended, UIntPtr.Zero);
        Thread.Sleep(TimeSpan.FromSeconds(durationSeconds));
        NativeMethods.keybd_event((byte)virtualKey, scanCode, extended | NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
    }

    public void SendKeyToGame(IntPtr gameHandle, ushort virtualKey, double durationSeconds)
    {
        _windowService.ForceForeground(gameHandle);
        // 300 ms after focus change: on lower-end machines the game window needs longer to
        // regain input focus than the previous 150 ms allowed, so the first click/keystroke
        // sometimes landed on the previously focused window instead.
        Thread.Sleep(300);
        SendKey(virtualKey, durationSeconds);
    }

    public void MoveAndClickBackground(IntPtr windowHandle, int clientX, int clientY)
    {
        if (!NativeMethods.GetClientRect(windowHandle, out var clientRect))
        {
            clientRect = new NativeMethods.Rect { Right = 1920, Bottom = 1080 };
        }

        var clientWidth = Math.Max(1, clientRect.Right - clientRect.Left);
        var clientHeight = Math.Max(1, clientRect.Bottom - clientRect.Top);
        var startX = _random.Next(20, Math.Max(21, clientWidth - 20));
        var startY = _random.Next(20, Math.Max(21, clientHeight - 20));
        var steps = _random.Next(15, 26);

        for (var i = 0; i < steps; i++)
        {
            var t = Math.Sin((i / (double)(steps - 1)) * Math.PI / 2);
            var currentX = (int)(startX + (clientX - startX) * t);
            var currentY = (int)(startY + (clientY - startY) * t);
            SendMouseMessage(windowHandle, NativeMethods.WmMousemove, IntPtr.Zero, NativeMethods.MakeLParam(currentX, currentY));
            // 25 ms per step (was 10). At 15-25 steps the trajectory now takes ~400-600 ms
            // instead of ~150-250 ms, which reads as a natural mouse glide rather than a
            // teleport.
            Thread.Sleep(25);
        }

        var finalX = clientX + _random.Next(-4, 5);
        var finalY = clientY + _random.Next(-4, 5);
        var lParam = NativeMethods.MakeLParam(finalX, finalY);

        SendMouseMessage(windowHandle, NativeMethods.WmMousemove, IntPtr.Zero, lParam);
        // 200 ms (was 50) between "cursor arrived" and mouse-down. 50 ms was short enough that
        // the click sometimes registered before the target element noticed the hover, which is
        // when the click landed on a neighbouring button.
        Thread.Sleep(200);
        SendMouseMessage(windowHandle, NativeMethods.WmLbuttondown, (IntPtr)NativeMethods.MkLbutton, lParam);
        Thread.Sleep(TimeSpan.FromMilliseconds(_random.Next(70, 151)));
        SendMouseMessage(windowHandle, NativeMethods.WmLbuttonup, IntPtr.Zero, lParam);
        // 200 ms after the up-event so the next MoveAndClickBackground call cannot start
        // repositioning the cursor before the click has been fully processed.
        Thread.Sleep(200);
    }

    public void ClickScreen(int screenX, int screenY)
    {
        try
        {
            var timings = _timingsProvider();
            MoveCursorTo(screenX, screenY, timings);

            // Press and release are their own events, carrying no movement. Windows coalesces
            // WM_MOUSEMOVE by default, so an event that is both a move and a button change can be
            // merged with a later move and delivered at that move's position - the button then
            // appears to be held and only released once the cursor sets off for the next target.
            // Button flags signal a state change and apply wherever the cursor already is, which is
            // the point MoveCursorTo has just parked it on.
            var cursorAtPress = System.Windows.Forms.Cursor.Position;
            SendButton(NativeMethods.MouseeventfLeftdown);
            Thread.Sleep(TimeSpan.FromSeconds(timings.ClickHoldDuration));
            SendButton(NativeMethods.MouseeventfLeftup);
            var cursorAtRelease = System.Windows.Forms.Cursor.Position;

            // Reports where the cursor actually was around the press. If a click still lands on the
            // wrong control while these read the intended point, the cursor is not the cause and
            // the target is interpreting the input itself.
            if (cursorAtPress.X != screenX || cursorAtPress.Y != screenY ||
                cursorAtRelease.X != screenX || cursorAtRelease.Y != screenY)
            {
                _logger.Warning(
                    $"Click ({screenX},{screenY}): cursor was ({cursorAtPress.X},{cursorAtPress.Y}) at press and " +
                    $"({cursorAtRelease.X},{cursorAtRelease.Y}) at release - something moved it mid-click.");
            }
            else
            {
                _logger.Info($"Click ({screenX},{screenY}): cursor held on target through press and release.");
            }

            // Keep the cursor where it is until the target has had a chance to act on the click.
            // MoveCursorTo tops this up if the next move comes sooner than the full hold.
            _lastClickFinishedUtc = DateTime.UtcNow;
            Thread.Sleep(TimeSpan.FromSeconds(timings.PostClickCursorHold));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Exception: {ex.Message}");
            throw;
        }
    }

    public void ClickScreenOnGame(IntPtr gameHandle, int screenX, int screenY) =>
        ClickScreenOnWindow(gameHandle, screenX, screenY);

    public void ClickScreenOnWindow(IntPtr windowHandle, int screenX, int screenY)
    {
        // Whatever we are about to click has to be the window actually receiving input, otherwise
        // the press goes to whichever window happens to be on top at that moment.
        _windowService.ForceForeground(windowHandle);

        // On lower-end machines the window needs a moment to regain input focus; without this the
        // first click landed on the previously focused window instead.
        Thread.Sleep(ForegroundSettle);
        ClickScreen(screenX, screenY);
    }

    // Single choke point for cursor movement, so the post-click hold cannot be skipped by a caller
    // that clicks and then immediately asks for the next position.
    private void MoveCursorTo(int screenX, int screenY, TimingSettings timings)
    {
        var hold = TimeSpan.FromSeconds(timings.PostClickCursorHold);
        var sinceLastClick = DateTime.UtcNow - _lastClickFinishedUtc;
        if (sinceLastClick < hold)
        {
            Thread.Sleep(hold - sinceLastClick);
        }

        // A real move event rather than SetCursorPos: it travels through the same input queue as
        // the click that follows, so a target watching the input stream registers the hover first.
        MoveAbsolute(screenX, screenY);
        Thread.Sleep(TimeSpan.FromSeconds(timings.CursorArrivalSettle));
    }

    // Movement only. NOCOALESCE keeps this from being merged with any other move, so the cursor is
    // demonstrably at the target before the button events follow.
    private static void MoveAbsolute(int screenX, int screenY)
    {
        var (normalisedX, normalisedY) = ToVirtualDesktopAbsolute(screenX, screenY);

        var input = new NativeMethods.Input
        {
            type = NativeMethods.InputMouse,
            mi = new NativeMethods.MouseInput
            {
                dx = normalisedX,
                dy = normalisedY,
                dwFlags = NativeMethods.MouseeventfMove
                    | NativeMethods.MouseeventfAbsolute
                    | NativeMethods.MouseeventfVirtualdesk
                    | NativeMethods.MouseeventfMoveNocoalesce
            }
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>());
    }

    // Button state change only - no movement flag, so this cannot be coalesced into a move.
    private static void SendButton(uint buttonFlag)
    {
        var input = new NativeMethods.Input
        {
            type = NativeMethods.InputMouse,
            mi = new NativeMethods.MouseInput { dwFlags = buttonFlag }
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>());
    }

    // Absolute mouse input is normalised to 0..65535 across the whole virtual desktop, so a second
    // monitor - including one positioned left of or above the primary, which makes the origin
    // negative - is handled by subtracting the virtual origin rather than assuming (0,0).
    private static (int X, int Y) ToVirtualDesktopAbsolute(int screenX, int screenY)
    {
        var originX = NativeMethods.GetSystemMetrics(NativeMethods.SmXvirtualscreen);
        var originY = NativeMethods.GetSystemMetrics(NativeMethods.SmYvirtualscreen);
        var width = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCxvirtualscreen) - 1);
        var height = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SmCyvirtualscreen) - 1);

        var normalisedX = (int)Math.Round((screenX - originX) * 65535.0 / width);
        var normalisedY = (int)Math.Round((screenY - originY) * 65535.0 / height);

        return (Math.Clamp(normalisedX, 0, 65535), Math.Clamp(normalisedY, 0, 65535));
    }

    private static void SendMouseMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam)
    {
        NativeMethods.SendMessage(windowHandle, message, wParam, lParam);
    }
}
