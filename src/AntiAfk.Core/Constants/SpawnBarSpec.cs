namespace AntiAfk.Core.Constants;

/// <summary>
/// Every setting the spawn-bar detector needs for one project, at the reference resolution
/// (1920×1080 fullscreen, game window at (0,0)). SpawnBarLayout scales this to whatever window
/// the game is actually running in.
///
/// The point of collecting these in one place: adding a third project is one new SpawnBarSpec
/// instance, not fifteen scattered fields on ProjectProfile plus fifteen matching arguments to
/// SpawnBarLayout.ForWindow. Defaults match Majestic — a project like Russia Online overrides
/// only what actually differs.
/// </summary>
public sealed record SpawnBarSpec
{
    // Layout at 1920×1080.
    public int CenterX { get; init; } = GameConstants.BaseSpawnBarCenterX;
    public int RowY { get; init; } = GameConstants.BaseSpawnBarRowY;
    public int Pitch { get; init; } = GameConstants.BaseSpawnIconPitch;
    public int Diameter { get; init; } = GameConstants.BaseSpawnIconDiameter;
    public int GlyphBox { get; init; } = GameConstants.BaseSpawnGlyphBox;
    public int MaxIcons { get; init; } = GameConstants.MaxSpawnIcons;

    /// True: icons are drawn on individual circular dark discs (Majestic). False: icons sit on a
    /// continuous rectangular dark strip (Russia Online).
    public bool CircularBackground { get; init; } = true;

    /// True: slot 0 is at CenterX and slots grow rightwards; the bar has no left flank. False:
    /// slots are centred symmetrically around CenterX.
    public bool LeftAligned { get; init; }

    // Background darkness gates.
    public int DiscMaxLuminance { get; init; } = 110;
    public double MinDiscRatio { get; init; } = 0.70;

    // Glyph whiteness ramp. Match SpawnIconSignature.DefaultWhiteRampLow/High — kept as literals
    // here to keep Constants free of a dependency on Vision.
    public int GlyphWhiteRampLow { get; init; } = 120;
    public int GlyphWhiteRampHigh { get; init; } = 230;

    // Glyph share of the box: below MinGlyphRatio the slot is noise; TargetGlyphRatio is the
    // share at which the slot scores at full glyph strength.
    public double MinGlyphRatio { get; init; } = 0.015;
    public double TargetGlyphRatio { get; init; } = 0.06;

    /// Slot score a fit has to reach, and that flanks must stay below.
    public double MinSlotScore { get; init; } = 0.55;

    /// Fixed-position spawn click when the detector cannot read the bar at all.
    public (int X, int Y) DefaultSpawn { get; init; } = (1053, 964);
}
