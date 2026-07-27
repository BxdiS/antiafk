using AntiAfk.Core.Abstractions;
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

    private readonly IWindowService _windowService;
    private readonly Random _random = new();

    public InputService(IWindowService windowService)
    {
        _windowService = windowService;
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

    // Nothing may be inserted between the cursor move and the two button events - see the note on
    // Debug.WriteLine below. Keep this body free of anything that can block or yield.
    public void ClickScreen(int screenX, int screenY)
    {
        try
        {
            // Deliberately no Debug.WriteLine in here.
            //
            // There used to be six of them, one before each step. Debug.WriteLine is
            // [Conditional("DEBUG")], so it compiles away entirely in Release but is very much
            // present in a Debug build - which is the one difference between the release exe that
            // clicks correctly and a build run out of Visual Studio that does not.
            //
            // Each call is an OutputDebugString. With a debugger attached that raises
            // DBG_PRINTEXCEPTION_C, and the debugger services it by suspending the process, reading
            // the string and resuming. Two of those sat directly in front of mouse_event, so every
            // click carried an unbounded, VS-dependent stall between "cursor is on the target" and
            // "button goes down" - the window in which the game, or a real mouse, can move the
            // pointer somewhere else. The fixed 300/100/200 ms below are only meaningful if nothing
            // else is inserted between them.
            //
            // If this ever needs tracing again, log it through IAppLogger before or after the click,
            // never between the steps.
            MoveCursorAbsolute(screenX, screenY);
            Thread.Sleep(300); // Time for the cursor to physically arrive before the press.
            SendButton(NativeMethods.MouseeventfLeftdown);
            Thread.Sleep(100); // How long a person holds the button down.
            SendButton(NativeMethods.MouseeventfLeftup);
            Thread.Sleep(200); // Nothing repositions the cursor until the game has acted on this.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Exception: {ex.Message}");
            throw;
        }
    }

    public void ClickScreenOnGame(IntPtr gameHandle, int screenX, int screenY)
    {
        _windowService.ForceForeground(gameHandle);
        // 300 ms after focus change: on lower-end machines the game window needs longer to
        // regain input focus than the previous 150 ms allowed, so the first click/keystroke
        // sometimes landed on the previously focused window instead.
        Thread.Sleep(300);
        ClickScreen(screenX, screenY);
    }

    /// <summary>
    /// Moves the cursor by injecting a movement event, not by setting the cursor position.
    ///
    /// This is the difference between a first run and a restart, and it is not in the click logic -
    /// it is in whether the game can see the movement at all.
    ///
    /// Cursor.Position is SetCursorPos. It relocates the system pointer and produces nothing in the
    /// input stream. GTA V reads the mouse through raw input and keeps its own pointer, advanced by
    /// movement deltas; a SetCursorPos produces no delta, so the game's pointer does not follow.
    /// The button event that comes next is then applied wherever the game still thinks the pointer
    /// is, not where we put the system cursor.
    ///
    /// Whether the two happen to agree depends on when the game last synchronised its pointer to
    /// the system cursor, which it does on window activation. That is the whole reason the same
    /// code behaved one way on a cold start and another way on a restart from character select -
    /// nothing about the sequence differed, only whether the game's pointer had been synced by
    /// something else beforehand.
    ///
    /// An injected absolute move is delivered through the same path as the click, so the game sees
    /// real movement and its pointer tracks it, in both cases identically.
    /// </summary>
    private static void MoveCursorAbsolute(int screenX, int screenY)
    {
        var (normalisedX, normalisedY) = ToVirtualDesktopAbsolute(screenX, screenY);

        Send(new NativeMethods.MouseInput
        {
            dx = normalisedX,
            dy = normalisedY,
            dwFlags = NativeMethods.MouseeventfMove
                | NativeMethods.MouseeventfAbsolute
                | NativeMethods.MouseeventfVirtualdesk
        });
    }

    // Button state change only, applied where the cursor now is.
    private static void SendButton(uint buttonFlag) =>
        Send(new NativeMethods.MouseInput { dwFlags = buttonFlag });

    private static void Send(NativeMethods.MouseInput mouseInput)
    {
        var input = new NativeMethods.Input
        {
            type = NativeMethods.InputMouse,
            mi = mouseInput
        };

        NativeMethods.SendInput(1, [input], System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>());
    }

    /// <summary>
    /// Absolute mouse input is normalised to 0..65535 across the whole virtual desktop, so a second
    /// monitor is handled by subtracting the virtual origin rather than assuming (0,0). That origin
    /// is genuinely negative on this setup - the secondary display sits at (1920,-685) - and
    /// assuming zero would put every click on the wrong part of the desktop.
    /// </summary>
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
