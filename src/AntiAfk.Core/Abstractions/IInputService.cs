namespace AntiAfk.Core.Abstractions;

public interface IInputService
{
    void SendKey(ushort virtualKey, double durationSeconds);
    void SendKeyToGame(IntPtr gameHandle, ushort virtualKey, double durationSeconds);
    void MoveAndClickBackground(IntPtr windowHandle, int clientX, int clientY);
    void ClickScreen(int screenX, int screenY);

    /// <summary>
    /// Raises <paramref name="windowHandle"/> to the foreground, then clicks the screen position.
    /// Screen clicks land on whatever is on top, so the target has to be on top first.
    /// </summary>
    void ClickScreenOnWindow(IntPtr windowHandle, int screenX, int screenY);
}
