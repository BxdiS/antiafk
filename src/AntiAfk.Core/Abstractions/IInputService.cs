namespace AntiAfk.Core.Abstractions;

public interface IInputService
{
    void SendKey(ushort virtualKey, double durationSeconds);
    void SendKeyToGame(IntPtr gameHandle, ushort virtualKey, double durationSeconds);
    void MoveAndClickBackground(IntPtr windowHandle, int clientX, int clientY);
    void ClickScreen(int screenX, int screenY);
    void ClickScreenOnGame(IntPtr gameHandle, int screenX, int screenY);

    /// <summary>
    /// Brings <paramref name="windowHandle"/> to the front, waits for it to take input focus, then
    /// clicks. Use this rather than <see cref="ClickScreen"/> whenever the click is meant for a
    /// specific window - otherwise the press goes to whatever is on top at that moment.
    /// </summary>
    void ClickScreenOnWindow(IntPtr windowHandle, int screenX, int screenY);
}
