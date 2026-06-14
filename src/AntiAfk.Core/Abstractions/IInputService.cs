namespace AntiAfk.Core.Abstractions;

public interface IInputService
{
    void SendKey(ushort virtualKey, double durationSeconds);
    void MoveAndClickBackground(IntPtr windowHandle, int clientX, int clientY);
    void ClickScreen(int screenX, int screenY);
}
