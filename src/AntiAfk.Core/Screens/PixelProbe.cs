namespace AntiAfk.Core.Screens;

/// <summary>
/// One pixel test: where to look, and what counts as a match.
///
/// Two shapes cover everything the game needs. <see cref="Exact"/> is a target colour with a
/// tolerance, for the accent-coloured indicators. <see cref="Range"/> takes a predicate, for the
/// checks that were written as channel ranges rather than a colour.
///
/// Coordinates are in the 1920x1080 space every value in this project was measured at.
/// <see cref="ScreenCatalogue"/> documents what that means on other resolutions.
/// </summary>
public sealed class PixelProbe
{
    private readonly Func<byte, byte, byte, bool> _matches;

    private PixelProbe(string description, int x, int y, Func<byte, byte, byte, bool> matches)
    {
        Description = description;
        X = x;
        Y = y;
        _matches = matches;
    }

    /// Human-readable, so a log line can say what was looked for and not just where.
    public string Description { get; }

    public int X { get; }

    public int Y { get; }

    public bool Matches(byte r, byte g, byte b) => _matches(r, g, b);

    /// <param name="rgb">Target colour as 0xRRGGBB.</param>
    public static PixelProbe Exact(string description, int x, int y, uint rgb, int tolerance)
    {
        var target = rgb & 0xFFFFFF;
        var tr = (int)((target >> 16) & 0xFF);
        var tg = (int)((target >> 8) & 0xFF);
        var tb = (int)(target & 0xFF);

        return new PixelProbe(
            $"{description} ~#{target:x6} +/-{tolerance}",
            x,
            y,
            (r, g, b) => Math.Abs(r - tr) <= tolerance
                && Math.Abs(g - tg) <= tolerance
                && Math.Abs(b - tb) <= tolerance);
    }

    public static PixelProbe Range(string description, int x, int y, Func<byte, byte, byte, bool> matches) =>
        new(description, x, y, matches);
}
