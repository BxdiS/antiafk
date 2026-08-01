namespace AntiAfk.Core.Vision;

/// <summary>
/// A rectangle of screen pixels, copied once and then read many times.
///
/// Everything else in the app reads the screen one pixel at a time through IScreenCaptureService,
/// which is fine for a handful of probes. The spawn bar is not that: working out how many icons
/// are on screen means looking at a strip of roughly 1400x110, and a GDI round trip per pixel
/// would take longer than the click it is trying to place.
///
/// Coordinates are local to the captured rectangle. OriginX/OriginY record where it was taken
/// from, so a hit found in here can be turned back into the screen coordinate to click.
/// </summary>
public sealed class PixelGrid
{
    /// Three bytes per pixel, row-major: R, G, B.
    private readonly byte[] _rgb;

    public PixelGrid(int originX, int originY, int width, int height, byte[] rgb)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), $"A pixel grid needs a positive size, got {width}x{height}.");
        }

        var expected = width * height * 3;
        if (rgb.Length != expected)
        {
            throw new ArgumentException(
                $"Expected {expected} bytes for a {width}x{height} RGB grid, got {rgb.Length}.", nameof(rgb));
        }

        OriginX = originX;
        OriginY = originY;
        Width = width;
        Height = height;
        _rgb = rgb;
    }

    public int OriginX { get; }
    public int OriginY { get; }
    public int Width { get; }
    public int Height { get; }

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public (byte R, byte G, byte B) this[int x, int y]
    {
        get
        {
            if (!Contains(x, y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x), $"({x},{y}) is outside a {Width}x{Height} grid.");
            }

            var offset = (y * Width + x) * 3;
            return (_rgb[offset], _rgb[offset + 1], _rgb[offset + 2]);
        }
    }

    public int ToScreenX(int localX) => OriginX + localX;

    public int ToScreenY(int localY) => OriginY + localY;

    public int ToLocalX(int screenX) => screenX - OriginX;

    public int ToLocalY(int screenY) => screenY - OriginY;

    /// Rec. 601 luma. Only the ordering matters here - "is this pixel dark" - so the exact
    /// weighting is not important, but a single number per pixel is.
    public static int Luminance(byte r, byte g, byte b) => (r * 299 + g * 587 + b * 114) / 1000;
}
