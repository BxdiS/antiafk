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

    /// <summary>
    /// Main window of the first running process with this name, or <see cref="IntPtr.Zero"/> if the
    /// process is not running or has not created its window yet.
    /// </summary>
    IntPtr FindMainWindowByProcess(string processName);

    /// <summary>Window title, for logging which window a click is about to go to.</summary>
    string GetWindowTitle(IntPtr handle);
}
