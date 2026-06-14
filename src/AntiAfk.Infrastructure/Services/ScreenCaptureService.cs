using AntiAfk.Core.Abstractions;

namespace AntiAfk.Infrastructure.Services;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    public (byte R, byte G, byte B) GetPixelColor(int screenX, int screenY)
    {
        using var bitmap = new System.Drawing.Bitmap(1, 1);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(screenX, screenY, 0, 0, new System.Drawing.Size(1, 1));
        var color = bitmap.GetPixel(0, 0);
        return (color.R, color.G, color.B);
    }

    public bool RegionContainsColor(int x1, int y1, int x2, int y2, Func<byte, byte, byte, bool> predicate)
    {
        var width = Math.Max(1, x2 - x1);
        var height = Math.Max(1, y2 - y1);

        using var bitmap = new System.Drawing.Bitmap(width, height);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x1, y1, 0, 0, new System.Drawing.Size(width, height));

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var color = bitmap.GetPixel(x, y);
                if (predicate(color.R, color.G, color.B))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
