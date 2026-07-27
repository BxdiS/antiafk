namespace AntiAfk.Core.Abstractions;

public interface IInputService
{
    void SendKey(ushort virtualKey, double durationSeconds);
    void SendKeyToGame(IntPtr gameHandle, ushort virtualKey, double durationSeconds);
    void MoveAndClickBackground(IntPtr windowHandle, int clientX, int clientY);
    void ClickScreen(int screenX, int screenY);
    void ClickScreenOnGame(IntPtr gameHandle, int screenX, int screenY);
}
