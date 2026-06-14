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

    public const string GameProcessName = "GTA5";
    public const string GameWindowTitlePrefix = "Majestic Multiplayer";
}
