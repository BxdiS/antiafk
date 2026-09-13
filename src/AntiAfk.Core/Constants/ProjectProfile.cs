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

    // Spawn bar: layout, detection thresholds, and fallback click, in one place. Defaults match
    // Majestic; each project overrides only what actually differs.
    public SpawnBarSpec SpawnBar { get; init; } = new();

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
        // pitch 50 — 2 icons at 935/985, 3 at 935/985/1035 — measured on ro-spawn.png through
        // GenerateSpawnIcons. The strip is a translucent panel over the map, subtler than
        // Majestic's discs, and glyphs are drawn light grey (~140), not white, so background
        // and glyph gates both loosen. MinGlyphRatio filters the 5-9% map noise between icons
        // out; real icons show 25-48%.
        SpawnBar = new SpawnBarSpec
        {
            CenterX = 935,
            RowY = 967,
            Pitch = 50,
            Diameter = 44,
            GlyphBox = 22,
            // A RO player can own anywhere from 1 to ~7 spawn points; the bar packs whatever
            // they have. Detector tries every count in this range and picks the fit that matches.
            MinIcons = 1,
            MaxIcons = 7,
            CircularBackground = false,
            LeftAligned = true,
            DiscMaxLuminance = 140,
            MinDiscRatio = 0.55,
            MinSlotScore = 0.35,
            GlyphWhiteRampLow = 90,
            GlyphWhiteRampHigh = 170,
            MinGlyphRatio = 0.15,
            TargetGlyphRatio = 0.25,
            DefaultSpawn = (935, 967)
        }
    };

    public static ProjectProfile ForProject(string project) =>
        string.Equals(project, "russia_online", StringComparison.OrdinalIgnoreCase)
            ? RussiaOnline
            : Majestic;
}
