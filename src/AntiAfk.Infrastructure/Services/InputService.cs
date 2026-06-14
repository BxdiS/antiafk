using AntiAfk.Core.Abstractions;
using AntiAfk.Infrastructure.Win32;

namespace AntiAfk.Infrastructure.Services;

public sealed class InputService : IInputService
{
    private readonly IWindowService _windowService;
    private readonly Random _random = new();

    public InputService(IWindowService windowService)
    {
        _windowService = windowService;
    }

    public void SendKey(ushort virtualKey, double durationSeconds)
    {
        var scanCode = (byte)NativeMethods.MapVirtualKey(virtualKey, 0);
        NativeMethods.keybd_event((byte)virtualKey, scanCode, 0, UIntPtr.Zero);
        Thread.Sleep(TimeSpan.FromSeconds(durationSeconds));
        NativeMethods.keybd_event((byte)virtualKey, scanCode, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
    }

    public void SendKeyToGame(IntPtr gameHandle, ushort virtualKey, double durationSeconds)
    {
        _windowService.ForceForeground(gameHandle);
        Thread.Sleep(150);
        SendKey(virtualKey, durationSeconds);
    }

    public void MoveAndClickBackground(IntPtr windowHandle, int clientX, int clientY)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        var startX = _random.Next(100, Math.Max(101, screen.Width - 100));
        var startY = _random.Next(100, Math.Max(101, screen.Height - 100));
        var steps = _random.Next(15, 26);

        for (var i = 0; i < steps; i++)
        {
            var t = Math.Sin((i / (double)(steps - 1)) * Math.PI / 2);
            var currentX = (int)(startX + (clientX - startX) * t);
            var currentY = (int)(startY + (clientY - startY) * t);
            NativeMethods.PostMessage(windowHandle, NativeMethods.WmMousemove, IntPtr.Zero, NativeMethods.MakeLParam(currentX, currentY));
            Thread.Sleep(10);
        }

        var finalX = clientX + _random.Next(-4, 5);
        var finalY = clientY + _random.Next(-4, 5);
        var lParam = NativeMethods.MakeLParam(finalX, finalY);

        NativeMethods.PostMessage(windowHandle, NativeMethods.WmMousemove, IntPtr.Zero, lParam);
        Thread.Sleep(50);
        NativeMethods.PostMessage(windowHandle, NativeMethods.WmLbuttondown, (IntPtr)NativeMethods.MkLbutton, lParam);
        Thread.Sleep(TimeSpan.FromMilliseconds(_random.Next(70, 151)));
        NativeMethods.PostMessage(windowHandle, NativeMethods.WmLbuttonup, IntPtr.Zero, lParam);
    }

    public void ClickScreen(int screenX, int screenY)
    {
        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenX, screenY);
        Thread.Sleep(30);
        NativeMethods.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        NativeMethods.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    public void ClickScreenOnGame(IntPtr gameHandle, int screenX, int screenY)
    {
        _windowService.ForceForeground(gameHandle);
        Thread.Sleep(150);
        ClickScreen(screenX, screenY);
    }
}
