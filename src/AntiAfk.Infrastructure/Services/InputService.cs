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
        Thread.Sleep(150);
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
            Thread.Sleep(10);
        }

        var finalX = clientX + _random.Next(-4, 5);
        var finalY = clientY + _random.Next(-4, 5);
        var lParam = NativeMethods.MakeLParam(finalX, finalY);

        SendMouseMessage(windowHandle, NativeMethods.WmMousemove, IntPtr.Zero, lParam);
        Thread.Sleep(50);
        SendMouseMessage(windowHandle, NativeMethods.WmLbuttondown, (IntPtr)NativeMethods.MkLbutton, lParam);
        Thread.Sleep(TimeSpan.FromMilliseconds(_random.Next(70, 151)));
        SendMouseMessage(windowHandle, NativeMethods.WmLbuttonup, IntPtr.Zero, lParam);
    }

    public void ClickScreen(int screenX, int screenY)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Setting cursor position to ({screenX}, {screenY})");
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenX, screenY);

            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Cursor set, waiting 300ms for mouse to settle");
            Thread.Sleep(300); // Increased from 30ms to ensure cursor has time to physically move

            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Sending MOUSEEVENTF_LEFTDOWN");
            NativeMethods.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);

            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Waiting 100ms between down and up");
            Thread.Sleep(100); // Increased from 80ms for more natural click duration

            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Sending MOUSEEVENTF_LEFTUP");
            NativeMethods.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);

            System.Diagnostics.Debug.WriteLine($"[InputService.ClickScreen] Click complete, waiting 200ms before next action");
            Thread.Sleep(200); // Added delay after click completes to prevent rapid re-positioning

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
        Thread.Sleep(150);
        ClickScreen(screenX, screenY);
    }

    private static void SendMouseMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam)
    {
        NativeMethods.SendMessage(windowHandle, message, wParam, lParam);
    }
}
