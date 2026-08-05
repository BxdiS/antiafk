using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Vision;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AntiAfk.Infrastructure.Services;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    public (byte R, byte G, byte B) GetPixelColor(int screenX, int screenY)
    {
        if (!IsOnAnyScreen(screenX, screenY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(screenX),
                $"Point ({screenX}, {screenY}) is not on any monitor. Monitors: {DescribeScreens()}");
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
        var width = Math.Max(1, x2 - x1);
        var height = Math.Max(1, y2 - y1);
        var region = new Rectangle(x1, y1, width, height);

        // The region has to sit inside a single monitor: spanning two monitors, or covering a gap
        // in a non-rectangular desktop, would silently capture black pixels for the uncovered part.
        if (!IsWithinSingleScreen(region))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x1),
                $"Region ({x1},{y1})-({x2},{y2}) is not fully contained in a single monitor. Monitors: {DescribeScreens()}");
        }

        try
        {
            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x1, y1, 0, 0, new Size(width, height));

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

    public PixelGrid CaptureRegion(int screenX, int screenY, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), $"Cannot capture a {width}x{height} region.");
        }

        var region = new Rectangle(screenX, screenY, width, height);

        // Same rule as RegionContainsColor: a capture that spans two monitors, or a gap between
        // them, comes back with black where nothing was covered, and black reads as a perfectly
        // good dark icon disc to whoever is looking at these pixels.
        if (!IsWithinSingleScreen(region))
        {
            throw new ArgumentOutOfRangeException(
                nameof(screenX),
                $"Region ({screenX},{screenY}) {width}x{height} is not fully contained in a single monitor. " +
                $"Monitors: {DescribeScreens()}");
        }

        try
        {
            using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(screenX, screenY, 0, 0, new Size(width, height));
            }

            return ToPixelGrid(bitmap, screenX, screenY);
        }
        catch (Exception ex) when (ex is not ArgumentOutOfRangeException)
        {
            throw new InvalidOperationException(
                $"Failed to capture region ({screenX},{screenY}) {width}x{height}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Copies a 24bpp bitmap into a PixelGrid. Goes through LockBits rather than GetPixel: the
    /// spawn bar strip is around 150k pixels, and GetPixel per pixel on that takes long enough to
    /// be visible in the login sequence.
    /// </summary>
    public static PixelGrid ToPixelGrid(Bitmap bitmap, int originX, int originY)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var rgb = new byte[width * height * 3];

        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            var row = new byte[data.Stride];

            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);

                for (var x = 0; x < width; x++)
                {
                    // Format24bppRgb is laid out B, G, R.
                    var source = x * 3;
                    var target = (y * width + x) * 3;
                    rgb[target] = row[source + 2];
                    rgb[target + 1] = row[source + 1];
                    rgb[target + 2] = row[source];
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return new PixelGrid(originX, originY, width, height, rgb);
    }

    // The virtual desktop is a union of monitor rectangles, not a single rectangle anchored at
    // (0,0): monitors can sit at negative offsets, and the union can leave uncovered gaps. Testing
    // against actual monitor bounds is the only way to tell a real coordinate from one that would
    // capture nothing.
    private static bool IsOnAnyScreen(int x, int y)
    {
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.Bounds.Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithinSingleScreen(Rectangle region)
    {
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.Bounds.Contains(region))
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeScreens() =>
        string.Join("; ", Screen.AllScreens.Select(s => s.Bounds.ToString()));
}
