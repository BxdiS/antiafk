using AntiAfk.Core.Constants;

namespace AntiAfk.Core.Screens;

/// <summary>
/// Which pixel identifies which screen. The single place any of these values is written down.
///
/// They were previously spread across AutoLoginService.Coords, GameConstants and inline literals in
/// StateDetector, with the same pixel described differently in two of them - one comment called the
/// server-connected indicator a "character-select indicator", and clicks were fired off the back of
/// it while the character tiles were still loading. One table means a screen cannot be described two
/// ways at once.
///
/// Coordinates are raw screen pixels measured at 1920x1080 with the game fullscreen from (0,0).
/// They are deliberately not scaled: the login flow starts before the game window exists, so there
/// is nothing to scale against at that point. On another resolution these read the wrong pixels,
/// which is why the resolution is logged at startup.
/// </summary>
public static class ScreenCatalogue
{
    /// Resolution every coordinate here was measured at.
    public const int MeasuredWidth = GameConstants.BaseWidth;
    public const int MeasuredHeight = GameConstants.BaseHeight;

    public static readonly PixelProbe ConnectingToServer = PixelProbe.Exact(
        "server-connected indicator", 634, 216, 0xff007e, 40);

    public static readonly PixelProbe CharacterSelect = PixelProbe.Exact(
        "character-select indicator",
        GameConstants.BaseCharSelectPixel.X,
        GameConstants.BaseCharSelectPixel.Y,
        ((uint)GameConstants.CharSelectR << 16) | ((uint)GameConstants.CharSelectG << 8) | GameConstants.CharSelectB,
        GameConstants.CharSelectTolerance);

    public static readonly PixelProbe InGame = PixelProbe.Range(
        "in-game HUD",
        GameConstants.BaseHudPixel.X,
        GameConstants.BaseHudPixel.Y,
        (r, g, b) => r >= 200 && g <= 60 && b is >= 80 and <= 170);

    public static readonly PixelProbe Marketplace = PixelProbe.Range(
        "marketplace active",
        GameConstants.BaseMpPixel.X,
        GameConstants.BaseMpPixel.Y,
        (r, g, b) => r is >= 40 and <= 85 && g is >= 110 and <= 160 && b is >= 190 and <= 245);

    public static readonly PixelProbe MarketplaceWarning = PixelProbe.Range(
        "marketplace with overlay",
        GameConstants.BaseMpPixel.X,
        GameConstants.BaseMpPixel.Y,
        (r, g, b) => r is >= 15 and <= 50 && g is >= 45 and <= 90 && b is >= 85 and <= 130);

    public static readonly PixelProbe MapOpen = PixelProbe.Range(
        "map menu",
        GameConstants.BaseMapPixel.X,
        GameConstants.BaseMapPixel.Y,
        (r, g, b) => r > 200 && g < 40 && b is >= 80 and <= 140);

    /// Lit only when the third character slot has been bought.
    public static readonly PixelProbe ThirdCharacterAvailable = PixelProbe.Exact(
        "third character slot", 1226, 1000, 0xe81c5a, 30);

    /// <summary>
    /// Evaluation order, and it matters.
    ///
    /// CharacterSelect must be tested before InGame: the two share the same pink accent colour, so
    /// the HUD probe reports a false positive on the character-select screen. MarketplaceWarning
    /// before Marketplace, because the overlay sits on top of an otherwise active marketplace.
    /// ConnectingToServer last of the pre-game screens, since it is the weakest signal.
    /// </summary>
    public static readonly IReadOnlyList<(GameScreen Screen, PixelProbe Probe)> InPriorityOrder =
    [
        (GameScreen.CharacterSelect, CharacterSelect),
        (GameScreen.MapOpen, MapOpen),
        (GameScreen.MarketplaceWarning, MarketplaceWarning),
        (GameScreen.Marketplace, Marketplace),
        (GameScreen.InGame, InGame),
        (GameScreen.ConnectingToServer, ConnectingToServer)
    ];
}
