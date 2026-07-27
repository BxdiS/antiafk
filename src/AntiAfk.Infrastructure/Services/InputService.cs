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

    // How much longer than the scheduled delay a step may take before it is worth reporting. Well
    // clear of ordinary scheduler jitter, low enough to catch a debugger servicing an exception.
    private const long StepSlackMs = 150;

    private readonly IAppLogger _logger;

    public InputService(IWindowService windowService, IAppLogger logger)
    {
        _windowService = windowService;
        _logger = logger;
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
            // Stopwatch reads are a QueryPerformanceCounter and nothing else - no allocation, no
            // formatting, no chance of blocking. Everything they measure is reported after the
            // click is over.
            var elapsed = System.Diagnostics.Stopwatch.StartNew();

            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenX, screenY);
            var movedAt = elapsed.ElapsedMilliseconds;
            Thread.Sleep(300); // Time for the cursor to physically arrive before the press.
            NativeMethods.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            var pressedAt = elapsed.ElapsedMilliseconds;
            Thread.Sleep(100); // How long a person holds the button down.
            NativeMethods.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            var releasedAt = elapsed.ElapsedMilliseconds;
            Thread.Sleep(200); // Nothing repositions the cursor until the game has acted on this.

            ReportIfStretched(screenX, screenY, movedAt, pressedAt, releasedAt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Exception: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Reports a click whose steps took materially longer than they were told to sleep.
    ///
    /// The whole sequence depends on the move, the press and the release staying the scheduled
    /// distance apart. Anything that suspends this thread in between - a debugger servicing a
    /// caught exception or an OutputDebugString, a stalled machine - widens the gap between the
    /// cursor arriving and the button going down, which is the window in which the click can end up
    /// somewhere else. That used to be invisible and had to be guessed at; now it is a log line
    /// with a number in it.
    ///
    /// Called after the click has finished, never between its steps.
    /// </summary>
    private void ReportIfStretched(int screenX, int screenY, long movedAt, long pressedAt, long releasedAt)
    {
        var moveToPress = pressedAt - movedAt;
        var pressToRelease = releasedAt - pressedAt;

        if (moveToPress <= 300 + StepSlackMs && pressToRelease <= 100 + StepSlackMs)
        {
            return;
        }

        _logger.Warning(
            $"Click ({screenX},{screenY}) ran long: {moveToPress}ms from cursor move to press " +
            $"(expected ~300ms), {pressToRelease}ms held (expected ~100ms). Something suspended the " +
            "thread mid-click - a debugger servicing an exception is the usual cause, and the click " +
            "may not have landed where it was aimed.");
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

    private static void SendMouseMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam)
    {
        NativeMethods.SendMessage(windowHandle, message, wParam, lParam);
    }
}
