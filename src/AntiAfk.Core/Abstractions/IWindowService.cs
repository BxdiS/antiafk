namespace AntiAfk.Core.Abstractions;

public sealed record GameWindowInfo(IntPtr Handle, string Title, int Left, int Top, int Width, int Height);

public sealed record UserWindowInfo(IntPtr Handle, string Title);

public interface IWindowService
{
    GameWindowInfo? FindGameWindow();
    bool IsWindowValid(IntPtr handle);
    IntPtr GetForegroundWindow();
    void ForceForeground(IntPtr handle);
    UserWindowInfo? CaptureUserWindow(IntPtr gameHandle);
    bool TryRestoreUserWindow(UserWindowInfo? userWindow, IntPtr gameHandle);
    /// <summary>
    /// Bounds of the monitor <paramref name="windowHandle"/> sits on. Pass <see cref="IntPtr.Zero"/>
    /// for the primary monitor. Coordinate scaling must use the display the game is actually on -
    /// on a multi-monitor setup the primary one is frequently not it.
    /// </summary>
    (int Width, int Height) GetScreenSize(IntPtr windowHandle);
}
