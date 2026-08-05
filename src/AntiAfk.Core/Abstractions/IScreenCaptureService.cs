using AntiAfk.Core.Vision;

namespace AntiAfk.Core.Abstractions;

public interface IScreenCaptureService
{
    (byte R, byte G, byte B) GetPixelColor(int screenX, int screenY);
    bool RegionContainsColor(int x1, int y1, int x2, int y2, Func<byte, byte, byte, bool> predicate);

    /// <summary>
    /// Copies a rectangle of the screen out in one go, for callers that have to look at every
    /// pixel in it. GetPixelColor is a screen capture per call, so reading a region through it
    /// costs one capture per pixel.
    /// </summary>
    PixelGrid CaptureRegion(int screenX, int screenY, int width, int height);
}
