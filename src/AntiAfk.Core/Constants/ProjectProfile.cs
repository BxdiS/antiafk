namespace AntiAfk.Core.Constants;

public sealed class ProjectProfile
{
    public required string Id { get; init; }

    // Character-select screen indicator pixel and expected colour.
    public required (int X, int Y) CharSelectPixel { get; init; }
    public required byte CharSelectR { get; init; }
    public required byte CharSelectG { get; init; }
    public required byte CharSelectB { get; init; }
    public int CharSelectTolerance { get; init; } = 40;

    // Pre-start menu pixel ("click LMB to play") and expected colour.
    public required (int X, int Y) PreStartPixel { get; init; }
    public required byte PreStartR { get; init; }
    public required byte PreStartG { get; init; }
    public required byte PreStartB { get; init; }
    public int PreStartTolerance { get; init; } = 40;

    // In-game HUD pixel and expected colour.
    public required (int X, int Y) HudPixel { get; init; }
    public required byte HudR { get; init; }
    public required byte HudG { get; init; }
    public required byte HudB { get; init; }
    public int HudTolerance { get; init; } = 40;

    // Character slot positions (select and confirm).
    public required (int X, int Y) Character1 { get; init; }
    public required (int X, int Y) Character1Confirm { get; init; }
    public required (int X, int Y) Character2 { get; init; }
    public required (int X, int Y) Character2Confirm { get; init; }
    public required (int X, int Y) Character3 { get; init; }
    public required (int X, int Y) Character3Confirm { get; init; }

    // Character 3 availability probe — if this colour matches, the slot is locked/unavailable.
    public required (int X, int Y) Character3Probe { get; init; }
    public required uint Character3ProbeColor { get; init; }
    public int Character3ProbeTolerance { get; init; } = 30;

    // Character 2 creation probe (RO only) — if this colour matches, character 2 is not created.
    // Null on Majestic where no such check exists.
    public (int X, int Y)? Character2Probe { get; init; }
    public uint Character2ProbeColor { get; init; }
    public int Character2ProbeTolerance { get; init; } = 30;

    // ESC/pause menu detection pixel and expected colour. The game's pause menu has a coloured
    // accent at a known position — checking it catches accidental ESC presses that leave the menu
    // sitting open. Majestic shows pink, Russia Online shows white.
    public required (int X, int Y) MapMenuPixel { get; init; }
    public required byte MapMenuR { get; init; }
    public required byte MapMenuG { get; init; }
    public required byte MapMenuB { get; init; }
    public int MapMenuTolerance { get; init; } = 30;

    // Launcher project button — clicked before the login button.
    public required (int X, int Y) ProjectButton { get; init; }

    // Marketplace icon inside the tablet, clicked after the tablet is opened.
    public (int X, int Y) MarketplaceIcon { get; init; } = (GameConstants.BaseIconX, GameConstants.BaseIconY);

    // Spawn bar layout. Majestic uses circular dark discs; Russia Online uses a rectangular dark
    // strip. Both hold white SVG glyphs and are centred on the screen.
    public int SpawnBarCenterX { get; init; } = GameConstants.BaseSpawnBarCenterX;
    public int SpawnBarRowY { get; init; } = GameConstants.BaseSpawnBarRowY;
    public int SpawnBarPitch { get; init; } = GameConstants.BaseSpawnIconPitch;
    public int SpawnBarDiameter { get; init; } = GameConstants.BaseSpawnIconDiameter;
    public int SpawnBarGlyphBox { get; init; } = GameConstants.BaseSpawnGlyphBox;
    public int SpawnBarMaxIcons { get; init; } = GameConstants.MaxSpawnIcons;
    public bool SpawnBarCircularBackground { get; init; } = true;

    // Detection thresholds — null keeps SpawnBarLayout's defaults (tuned for Majestic). RO's
    // rectangular strip is subtler than Majestic's discs, so it uses looser values.
    public int? SpawnBarDiscMaxLuminance { get; init; }
    public double? SpawnBarMinDiscRatio { get; init; }
    public double? SpawnBarMinSlotScore { get; init; }

    // Glyph whiteness ramp. Majestic's glyphs are pure white; RO draws them as light grey and
    // needs lower ramp values, otherwise none of its pixels count as glyph.
    public int? SpawnBarGlyphWhiteRampLow { get; init; }
    public int? SpawnBarGlyphWhiteRampHigh { get; init; }

    // Bar alignment. False (default): icons centred around SpawnBarCenterX (Majestic). True: icons
    // packed rightwards from SpawnBarCenterX, so slot 0 is at SpawnBarCenterX regardless of count.
    public bool SpawnBarLeftAligned { get; init; }

    // Glyph-share thresholds. Higher values reject positions with only a few stray bright pixels
    // (map noise between icons) that would otherwise cross the initial score gate.
    public double? SpawnBarMinGlyphRatio { get; init; }
    public double? SpawnBarTargetGlyphRatio { get; init; }

    // Fallback spawn click when the detector cannot read the bar at all.
    public (int X, int Y) DefaultSpawn { get; init; } = (1053, 964);

    public static ProjectProfile Majestic { get; } = new()
    {
        Id = "majestic",
        CharSelectPixel = GameConstants.BaseCharSelectPixel,
        CharSelectR = GameConstants.CharSelectR,
        CharSelectG = GameConstants.CharSelectG,
        CharSelectB = GameConstants.CharSelectB,
        CharSelectTolerance = GameConstants.CharSelectTolerance,
        PreStartPixel = GameConstants.BasePreStartPixel,
        PreStartR = GameConstants.PreStartR,
        PreStartG = GameConstants.PreStartG,
        PreStartB = GameConstants.PreStartB,
        PreStartTolerance = GameConstants.PreStartTolerance,
        HudPixel = GameConstants.BaseHudPixel,
        HudR = 0xFF,
        HudG = 0x00,
        HudB = 0x7F,
        Character1 = (594, 933),
        Character1Confirm = (593, 993),
        Character2 = (982, 929),
        Character2Confirm = (959, 993),
        Character3 = (1333, 927),
        Character3Confirm = (1323, 993),
        Character3Probe = (1226, 1000),
        Character3ProbeColor = 0xe81c5a,
        MapMenuPixel = GameConstants.BaseMapPixel,
        MapMenuR = 0xE0,
        MapMenuG = 0x14,
        MapMenuB = 0x6E,
        ProjectButton = GameConstants.ProjectMajestic
    };

    public static ProjectProfile RussiaOnline { get; } = new()
    {
        Id = "russia_online",
        CharSelectPixel = (1787, 67),
        CharSelectR = 0xC8,
        CharSelectG = 0x3D,
        CharSelectB = 0x3D,
        PreStartPixel = (496, 309),
        PreStartR = 0xC8,
        PreStartG = 0x3D,
        PreStartB = 0x3D,
        HudPixel = (1845, 36),
        HudR = 0xC8,
        HudG = 0x3D,
        HudB = 0x3D,
        Character1 = (534, 870),
        Character1Confirm = (534, 942),
        Character2 = (970, 870),
        Character2Confirm = (970, 942),
        Character3 = (1362, 870),
        Character3Confirm = (1362, 942),
        Character3Probe = (1350, 945),
        Character3ProbeColor = 0xc83d3d,
        Character2Probe = (955, 860),
        Character2ProbeColor = 0x8f8f8f,
        MapMenuPixel = (403, 220),
        MapMenuR = 0xFF,
        MapMenuG = 0xFF,
        MapMenuB = 0xFF,
        MapMenuTolerance = 40,
        ProjectButton = GameConstants.ProjectRussiaOnline,
        MarketplaceIcon = (1026, 174),
        // The RO bar is anchored at the left, not centred. Icons pack rightwards from x=935 at
        // pitch 50, so a 2-icon bar sits at 935/985 and a 3-icon bar at 935/985/1035 - measured
        // by dumping glyph clusters on ro-spawn.png through GenerateSpawnIcons.
        SpawnBarCenterX = 935,
        SpawnBarRowY = 967,
        SpawnBarPitch = 50,
        SpawnBarDiameter = 44,
        SpawnBarGlyphBox = 22,
        SpawnBarMaxIcons = 5,
        SpawnBarCircularBackground = false,
        SpawnBarLeftAligned = true,
        // The RO strip is a translucent panel on top of the map, noticeably lighter than
        // Majestic's discs. Loose enough to accept the strip as background while flanks stay out.
        SpawnBarDiscMaxLuminance = 140,
        SpawnBarMinDiscRatio = 0.55,
        SpawnBarMinSlotScore = 0.35,
        // RO glyphs are drawn light grey (~140), not white. Lower ramp so their pixels register.
        SpawnBarGlyphWhiteRampLow = 90,
        SpawnBarGlyphWhiteRampHigh = 170,
        // Map noise between icons registers 5-9% glyph pixels; real icons register 25-40%. The
        // gate keeps the two apart. Target follows so a real icon still maxes out at strength 1.
        SpawnBarMinGlyphRatio = 0.15,
        SpawnBarTargetGlyphRatio = 0.25,
        DefaultSpawn = (935, 967)
    };

    public static ProjectProfile ForProject(string project) =>
        string.Equals(project, "russia_online", StringComparison.OrdinalIgnoreCase)
            ? RussiaOnline
            : Majestic;
}
