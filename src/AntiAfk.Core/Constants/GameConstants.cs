namespace AntiAfk.Core.Constants;

public static class GameConstants
{
    public const int BaseWidth = 1920;
    public const int BaseHeight = 1080;

    public static readonly (int X, int Y)[] BaseButtons =
    [
        (133, 133), (153, 183), (156, 227), (130, 273), (136, 318),
        (114, 365), (120, 410), (133, 452), (121, 496), (150, 544), (170, 590)
    ];

    public const int BaseAdZoneX1 = 314;
    public const int BaseAdZoneY1 = 125;
    public const int BaseAdZoneX2 = 1881;
    public const int BaseAdZoneY2 = 988;

    public const int BaseCenterX = 960;
    public const int BaseCenterY = 540;
    public const int BaseIconX = 1224;
    public const int BaseIconY = 167;

    public static readonly (int X1, int Y1, int X2, int Y2) BaseWarnBox = (770, 436, 781, 439);
    public static readonly (int X, int Y) BaseWarnClick = (1084, 630);
    public static readonly (int X, int Y) BaseMapPixel = (512, 120);
    public static readonly (int X, int Y) BaseHudPixel = (1888, 25);
    public static readonly (int X, int Y) BaseMpPixel = (1770, 34);

    // Character-select screen indicator. NOTE: this shares the same pink accent colour as the
    // in-game HUD pixel, so this check must run BEFORE the HUD check when classifying UI state.
    public static readonly (int X, int Y) BaseCharSelectPixel = (1870, 52);
    public const byte CharSelectR = 0xE8;
    public const byte CharSelectG = 0x1C;
    public const byte CharSelectB = 0x5A;
    public const int CharSelectTolerance = 40;

    // The menu shown once the client has connected and before character select. It sits there
    // waiting for a click to continue - nothing happens on its own. This pixel was previously
    // treated as a "server connected, safe to click characters" signal, which it is not: it means
    // this menu is up, and the character tiles only appear after it has been dismissed.
    public static readonly (int X, int Y) BasePreStartPixel = (634, 216);
    public const byte PreStartR = 0xFF;
    public const byte PreStartG = 0x00;
    public const byte PreStartB = 0x7E;
    public const int PreStartTolerance = 40;

    // Spawn-point bar, the row of round icons along the bottom of the map screen shown after a
    // character has been confirmed. How many icons a player gets depends on what they own, so the
    // only fixed things are the pitch, the row and the fact that the row is centred - which is
    // exactly what SpawnBarDetector fits against. Measured at 1920x1080 fullscreen.
    public const int BaseSpawnBarCenterX = BaseCenterX;
    public const int BaseSpawnBarRowY = 965;
    public const int BaseSpawnIconPitch = 96;
    public const int BaseSpawnIconDiameter = 72;

    /// Side of the square around an icon centre that the glyph fits inside, used for signatures.
    /// The glyphs measure up to 31x33 here, so this leaves a few pixels of margin on each side
    /// without wasting the signature grid on empty space.
    public const int BaseSpawnGlyphBox = 44;

    /// Most icons any player can have. The bar is centred, so this also sets how wide a strip of
    /// screen has to be captured to be sure the whole bar is in it.
    public const int MaxSpawnIcons = 12;

    // Project picker in the launcher, shown before the server list. Each project is a separate
    // game server sharing the same launcher. The X coordinate is the same for both; only Y differs.
    public const int BaseProjectButtonX = 460;
    public static readonly (int X, int Y) ProjectMajestic = (BaseProjectButtonX, 302);
    public static readonly (int X, int Y) ProjectRussiaOnline = (BaseProjectButtonX, 338);

    public static readonly string[] KnownProjects = ["majestic", "russia_online"];
    public const string DefaultProject = "majestic";

    public const string GameProcessName = "GTA5";

    // The launcher ships as "Majestic Launcher.exe", so the process name is the file name without
    // the extension. The title prefix is the fallback for installs pointed at a renamed exe via
    // the LauncherPath setting, where the process name no longer matches.
    public const string LauncherProcessName = "Majestic Launcher";
    public const string LauncherTitlePrefix = "Majestic Launcher";
}
