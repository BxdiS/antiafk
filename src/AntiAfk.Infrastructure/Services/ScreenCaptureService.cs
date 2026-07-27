using AntiAfk.Core.Abstractions;
using System.Drawing;
using System.Windows.Forms;

namespace AntiAfk.Infrastructure.Services;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    public string LastFailureReason { get; private set; } = string.Empty;

    public bool TryGetPixelColor(int screenX, int screenY, out (byte R, byte G, byte B) color)
    {
        color = default;

        if (!IsOnAnyScreen(screenX, screenY))
        {
            LastFailureReason = $"point ({screenX},{screenY}) is not on any monitor. Monitors: {DescribeScreens()}";
            return false;
        }

        try
        {
            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(screenX, screenY, 0, 0, new Size(1, 1));
            var pixel = bitmap.GetPixel(0, 0);
            color = (pixel.R, pixel.G, pixel.B);
            LastFailureReason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            // Genuinely routine: the desktop is not readable while the display mode is changing,
            // which is most of the time between launching the game and the character-select screen.
            LastFailureReason = $"could not read ({screenX},{screenY}): {ex.Message}";
            return false;
        }
    }

    public bool TryRegionContainsColor(
        int x1,
        int y1,
        int x2,
        int y2,
        Func<byte, byte, byte, bool> predicate,
        out bool containsColor)
    {
        containsColor = false;

        var width = Math.Max(1, x2 - x1);
        var height = Math.Max(1, y2 - y1);
        var region = new Rectangle(x1, y1, width, height);

        // The region has to sit inside a single monitor: spanning two monitors, or covering a gap
        // in a non-rectangular desktop, would silently capture black pixels for the uncovered part.
        if (!IsWithinSingleScreen(region))
        {
            LastFailureReason =
                $"region ({x1},{y1})-({x2},{y2}) is not fully inside one monitor. Monitors: {DescribeScreens()}";
            return false;
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
                        containsColor = true;
                        LastFailureReason = string.Empty;
                        return true;
                    }
                }
            }

            LastFailureReason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LastFailureReason = $"could not read region ({x1},{y1})-({x2},{y2}): {ex.Message}";
            return false;
        }
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
