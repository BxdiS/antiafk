namespace AntiAfk.Core.Screens;

/// <summary>
/// Every screen the bot can recognise from what is on the display.
///
/// This exists so nothing has to remember "how did we get here". The old login code held two
/// hardcoded scripts - one for starting at the launcher, one for starting already in game - and
/// they drifted apart: they waited on different pixels, one of them raised the game window and the
/// other did not, and a click was fired on a screen that had only been assumed to be up. Which
/// script had run, rather than what was actually on screen, decided what happened next.
///
/// Recognising the screen instead removes the concept of a path entirely. There is one loop: look
/// at the display, do the one thing that screen calls for, look again.
/// </summary>
public enum GameScreen
{
    /// Nothing recognised. Usually a transition, a load, or the game not running yet.
    Unknown,

    /// Connected to the server; the screen shown immediately before character select.
    /// Not clickable - the character tiles are not up yet.
    ConnectingToServer,

    /// Character tiles are on screen and can be clicked.
    CharacterSelect,

    /// In the world, HUD present.
    InGame,

    /// Marketplace open and interactive.
    Marketplace,

    /// Marketplace open with a notification overlay on top of it, which has to be dismissed first.
    MarketplaceWarning,

    /// Full-screen map is open.
    MapOpen
}
