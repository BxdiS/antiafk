using AntiAfk.Core.Constants;

namespace AntiAfk.Core.Vision;

/// <summary>
/// A SpawnBarSpec scaled onto a real game window: pixel positions in screen coordinates, the
/// window's own scale so 1080p coordinates can be turned into clicks, and every detection
/// threshold copied off the spec so the detector reads a single object.
///
/// Everything project-specific lives on SpawnBarSpec; this record only carries the scaling and
/// the derived strip rectangle the capture reads.
/// </summary>
public sealed record SpawnBarLayout
{
    public required int CenterX { get; init; }
    public required int RowY { get; init; }
    public required int Pitch { get; init; }
    public required int Diameter { get; init; }
    public required int GlyphBox { get; init; }
    public required int MinIcons { get; init; }
    public required int MaxIcons { get; init; }
    public required bool CircularBackground { get; init; }
    public required bool LeftAligned { get; init; }

    public required int DiscMaxLuminance { get; init; }
    public required double MinDiscRatio { get; init; }
    public required double MinSlotScore { get; init; }
    public required int GlyphWhiteRampLow { get; init; }
    public required int GlyphWhiteRampHigh { get; init; }
    public required double MinGlyphRatio { get; init; }
    public required double TargetGlyphRatio { get; init; }

    /// Top-left of the game window on screen, and how its size compares with the reference
    /// resolution — so a 1080p coordinate can be turned into a click on this window.
    public required int WindowLeft { get; init; }
    public required int WindowTop { get; init; }
    public required double ScaleX { get; init; }
    public required double ScaleY { get; init; }

    /// Where a point measured at 1920×1080 lands on this window.
    public (int X, int Y) ToScreen(int baseX, int baseY) =>
        (WindowLeft + (int)Math.Round(baseX * ScaleX), WindowTop + (int)Math.Round(baseY * ScaleY));

    /// How far above and below the row the captured strip reaches past the icons. Covers the row
    /// being a few pixels off, which the detector searches for rather than assuming.
    public int RowSearchMargin => Math.Max(8, Diameter / 8);

    public int StripLeft =>
        LeftAligned
            ? CenterX - Pitch
            : CenterX - (MaxIcons * Pitch) / 2 - Pitch / 2;

    public int StripTop => RowY - Diameter / 2 - RowSearchMargin;

    public int StripWidth => MaxIcons * Pitch + Pitch;

    public int StripHeight => Diameter + RowSearchMargin * 2;

    /// The Majestic spec at 1920×1080, window at (0,0). Kept for GenerateSpawnIcons, which
    /// operates on screenshots that are already at reference resolution.
    public static SpawnBarLayout Base { get; } = ForWindow(new SpawnBarSpec(), 0, 0, 0, 0);

    /// <summary>
    /// The Majestic bar on a game window of a given size and position. Retained for callers that
    /// only ever need the default project — the tool's default entry point does.
    /// </summary>
    public static SpawnBarLayout ForWindow(int windowLeft, int windowTop, int windowWidth, int windowHeight) =>
        ForWindow(new SpawnBarSpec(), windowLeft, windowTop, windowWidth, windowHeight);

    /// <summary>
    /// A spec scaled onto a game window. When the window size is unknown (zero) the spec's
    /// reference values are used as-is, so a screenshot at the reference resolution just works.
    /// </summary>
    public static SpawnBarLayout ForWindow(SpawnBarSpec spec, int windowLeft, int windowTop, int windowWidth, int windowHeight)
    {
        var hasWindow = windowWidth > 0 && windowHeight > 0;
        var scaleX = hasWindow ? windowWidth / (double)GameConstants.BaseWidth : 1;
        var scaleY = hasWindow ? windowHeight / (double)GameConstants.BaseHeight : 1;
        var iconScale = hasWindow ? Math.Min(scaleX, scaleY) : 1;
        var offsetX = hasWindow ? windowLeft : 0;
        var offsetY = hasWindow ? windowTop : 0;

        return new SpawnBarLayout
        {
            CenterX = offsetX + (int)Math.Round(spec.CenterX * scaleX),
            RowY = offsetY + (int)Math.Round(spec.RowY * scaleY),
            Pitch = Math.Max(1, (int)Math.Round(spec.Pitch * iconScale)),
            Diameter = Math.Max(1, (int)Math.Round(spec.Diameter * iconScale)),
            GlyphBox = Math.Max(1, (int)Math.Round(spec.GlyphBox * iconScale)),
            MinIcons = spec.MinIcons,
            MaxIcons = spec.MaxIcons,
            CircularBackground = spec.CircularBackground,
            LeftAligned = spec.LeftAligned,
            DiscMaxLuminance = spec.DiscMaxLuminance,
            MinDiscRatio = spec.MinDiscRatio,
            MinSlotScore = spec.MinSlotScore,
            GlyphWhiteRampLow = spec.GlyphWhiteRampLow,
            GlyphWhiteRampHigh = spec.GlyphWhiteRampHigh,
            MinGlyphRatio = spec.MinGlyphRatio,
            TargetGlyphRatio = spec.TargetGlyphRatio,
            WindowLeft = offsetX,
            WindowTop = offsetY,
            ScaleX = scaleX,
            ScaleY = scaleY
        };
    }

    /// Screen X of slot <paramref name="index"/> when the bar holds <paramref name="count"/> icons.
    /// Centred: an even count straddles the centre and an odd one sits on it. Left-aligned: slot 0
    /// sits at CenterX and slots grow to the right regardless of count.
    public int SlotCenterX(int index, int count) =>
        LeftAligned
            ? CenterX + index * Pitch
            : CenterX + (int)Math.Round((index - (count - 1) / 2.0) * Pitch);
}
