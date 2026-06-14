namespace AntiAfk.Core.Abstractions;

public interface IScreenCaptureService
{
    (byte R, byte G, byte B) GetPixelColor(int screenX, int screenY);
    bool RegionContainsColor(int x1, int y1, int x2, int y2, Func<byte, byte, byte, bool> predicate);
}
