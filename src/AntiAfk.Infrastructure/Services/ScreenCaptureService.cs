using AntiAfk.Core.Abstractions;
using System.Drawing;

namespace AntiAfk.Infrastructure.Services;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    public (byte R, byte G, byte B) GetPixelColor(int screenX, int screenY)
    {
        // Validate coordinates are within screen bounds
        var totalWidth = 0;
        var totalHeight = 0;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            totalWidth = Math.Max(totalWidth, screen.Bounds.Right);
            totalHeight = Math.Max(totalHeight, screen.Bounds.Bottom);
        }

        if (screenX < 0 || screenY < 0 || screenX >= totalWidth || screenY >= totalHeight)
        {
            throw new ArgumentOutOfRangeException($"Coordinates ({screenX}, {screenY}) are outside screen bounds (0,0) to ({totalWidth},{totalHeight})");
        }

        try
        {
            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(screenX, screenY, 0, 0, new Size(1, 1));
            var color = bitmap.GetPixel(0, 0);
            return (color.R, color.G, color.B);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to capture pixel at ({screenX}, {screenY}): {ex.Message}", ex);
        }
    }

    public bool RegionContainsColor(int x1, int y1, int x2, int y2, Func<byte, byte, byte, bool> predicate)
    {
        var totalWidth = 0;
        var totalHeight = 0;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            totalWidth = Math.Max(totalWidth, screen.Bounds.Right);
            totalHeight = Math.Max(totalHeight, screen.Bounds.Bottom);
        }

        if (x1 < 0 || y1 < 0 || x2 > totalWidth || y2 > totalHeight)
        {
            throw new ArgumentOutOfRangeException($"Region ({x1},{y1})-({x2},{y2}) is outside screen bounds (0,0) to ({totalWidth},{totalHeight})");
        }

        var width = Math.Max(1, x2 - x1);
        var height = Math.Max(1, y2 - y1);

        try
        {
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
        catch (Exception ex) when (ex is not ArgumentOutOfRangeException)
        {
            throw new InvalidOperationException($"Failed to capture region ({x1},{y1})-({x2},{y2}): {ex.Message}", ex);
        }
    }
}
