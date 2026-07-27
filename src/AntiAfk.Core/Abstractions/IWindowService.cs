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
    (int Width, int Height) GetScreenSize();
}
