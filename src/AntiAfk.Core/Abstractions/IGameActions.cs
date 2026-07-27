namespace AntiAfk.Core.Abstractions;

/// <summary>
/// Every discrete thing the bot can do to the game, one method each.
///
/// These know how to perform an action and nothing else: no waiting for a screen, no deciding
/// whether the action is appropriate, no knowledge of what ran before. Deciding is
/// <see cref="IScreenRecognizer"/>'s job and sequencing is the flow's; keeping the three apart is
/// what stops "which path did we come in by" from being able to change the outcome.
///
/// Each method leaves the game a moment to react before returning, so a caller cannot accidentally
/// fire two actions into one frame.
/// </summary>
public interface IGameActions
{
    /// Clicks the launcher's login button, raising the launcher window first.
    Task ClickLauncherLoginAsync(CancellationToken cancellationToken);

    /// Clicks a character tile, then the confirm button that appears under it.
    Task ChooseCharacterAsync(int slot, CancellationToken cancellationToken);

    /// Clicks a spawn point.
    Task ChooseSpawnAsync(int slot, CancellationToken cancellationToken);

    /// Opens the in-game tablet (Down arrow).
    Task OpenTabletAsync(CancellationToken cancellationToken);

    /// Clicks the middle of the screen - wakes the tablet UI once it is open.
    Task ClickScreenCentreAsync(CancellationToken cancellationToken);

    /// Clicks the marketplace icon on the tablet.
    Task OpenMarketplaceAsync(CancellationToken cancellationToken);

    /// Dismisses the warehouse notification overlay.
    Task DismissWarningAsync(CancellationToken cancellationToken);

    /// Sends Escape to the game.
    Task PressEscapeAsync(CancellationToken cancellationToken);

    /// True when the third character slot has been purchased and can be selected.
    bool IsThirdCharacterAvailable();
}
