namespace AntiAfk.Core.Abstractions;

public sealed record GameWindowInfo(IntPtr Handle, string Title, int Left, int Top, int Width, int Height);

public sealed record LauncherWindowInfo(IntPtr Handle, string Title, int Left, int Top, int Width, int Height);

public sealed record UserWindowInfo(IntPtr Handle, string Title);

public interface IWindowService
{
    GameWindowInfo? FindGameWindow();

    /// <summary>
    /// The Majestic launcher window, or null when the launcher is not up.
    ///
    /// The launcher's login button is clicked at a fixed screen coordinate, so the click goes to
    /// whichever window happens to be on top at that point. Callers raise this window first.
    /// </summary>
    LauncherWindowInfo? FindLauncherWindow();

    bool IsWindowValid(IntPtr handle);
    IntPtr GetForegroundWindow();
    void ForceForeground(IntPtr handle);
    UserWindowInfo? CaptureUserWindow(IntPtr gameHandle);
    bool TryRestoreUserWindow(UserWindowInfo? userWindow, IntPtr gameHandle);
    (int Width, int Height) GetScreenSize();
}
