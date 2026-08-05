using AntiAfk.Core.Constants;

namespace AntiAfk.Core.Vision;

/// <summary>
/// Where the spawn bar is and how big it is, in screen pixels.
///
/// The bar is a row of equally spaced round icons centred horizontally along the bottom of the
/// map screen. Only the spacing and the row are fixed - the number of icons is whatever the player
/// owns - so these are the numbers a detector needs to fit the icon count against the screen,
/// rather than a table of positions like the rest of the app uses.
///
/// Unlike the other login coordinates these are scaled to the game window, because scaling a
/// search region is free: nothing here is a click at a guessed position, so being approximately
/// right is enough to find the bar, and the click that follows lands on a detected icon.
/// </summary>
public sealed record SpawnBarLayout
{
    /// Screen X the row of icons is centred on.
    public required int CenterX { get; init; }

    /// Screen Y of the icon centres.
    public required int RowY { get; init; }

    /// Distance between the centres of two neighbouring icons.
    public required int Pitch { get; init; }

    /// Diameter of the dark disc behind a glyph.
    public required int Diameter { get; init; }

    /// Side of the square around an icon centre the glyph fits in.
    public required int GlyphBox { get; init; }

    /// Top-left of the game window on screen, and how its size compares with the resolution
    /// everything was measured at. Kept so a coordinate measured at 1080p can still be turned into
    /// a screen position on this window - see <see cref="ToScreen"/>.
    public required int WindowLeft { get; init; }
    public required int WindowTop { get; init; }
    public required double ScaleX { get; init; }
    public required double ScaleY { get; init; }

    /// Where a point measured at 1920x1080 lands on this window.
    public (int X, int Y) ToScreen(int baseX, int baseY) =>
        (WindowLeft + (int)Math.Round(baseX * ScaleX), WindowTop + (int)Math.Round(baseY * ScaleY));

    /// How far above and below the row the captured strip reaches past the discs. Covers the row
    /// being a few pixels off, which the detector searches for rather than assuming.
    public int RowSearchMargin => Math.Max(8, Diameter / 8);

    public int StripLeft => CenterX - (GameConstants.MaxSpawnIcons * Pitch) / 2 - Pitch / 2;

    public int StripTop => RowY - Diameter / 2 - RowSearchMargin;

    public int StripWidth => GameConstants.MaxSpawnIcons * Pitch + Pitch;

    public int StripHeight => Diameter + RowSearchMargin * 2;

    /// The bar as measured: 1920x1080, fullscreen, top-left of the game window at (0,0).
    public static SpawnBarLayout Base { get; } = new()
    {
        CenterX = GameConstants.BaseSpawnBarCenterX,
        RowY = GameConstants.BaseSpawnBarRowY,
        Pitch = GameConstants.BaseSpawnIconPitch,
        Diameter = GameConstants.BaseSpawnIconDiameter,
        GlyphBox = GameConstants.BaseSpawnGlyphBox,
        WindowLeft = 0,
        WindowTop = 0,
        ScaleX = 1,
        ScaleY = 1
    };

    /// <summary>
    /// The same bar on a game window of a given size and position. Scales the way CoordinateScaler
    /// does - proportionally on each axis, then offset by the window's top-left corner.
    /// </summary>
    public static SpawnBarLayout ForWindow(int windowLeft, int windowTop, int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0)
        {
            return Base;
        }

        var scaleX = windowWidth / (double)GameConstants.BaseWidth;
        var scaleY = windowHeight / (double)GameConstants.BaseHeight;

        // The icons are round, so their size scales with the smaller of the two axes - a window
        // that is not 16:9 letterboxes the game rather than stretching the UI into ellipses.
        var iconScale = Math.Min(scaleX, scaleY);

        return new SpawnBarLayout
        {
            CenterX = windowLeft + (int)Math.Round(GameConstants.BaseSpawnBarCenterX * scaleX),
            RowY = windowTop + (int)Math.Round(GameConstants.BaseSpawnBarRowY * scaleY),
            Pitch = Math.Max(1, (int)Math.Round(GameConstants.BaseSpawnIconPitch * iconScale)),
            Diameter = Math.Max(1, (int)Math.Round(GameConstants.BaseSpawnIconDiameter * iconScale)),
            GlyphBox = Math.Max(1, (int)Math.Round(GameConstants.BaseSpawnGlyphBox * iconScale)),
            WindowLeft = windowLeft,
            WindowTop = windowTop,
            ScaleX = scaleX,
            ScaleY = scaleY
        };
    }

    /// Screen X of slot <paramref name="index"/> when the bar holds <paramref name="count"/> icons.
    /// The row is centred, so an even count straddles the centre and an odd one sits on it.
    public int SlotCenterX(int index, int count) =>
        CenterX + (int)Math.Round((index - (count - 1) / 2.0) * Pitch);
}
