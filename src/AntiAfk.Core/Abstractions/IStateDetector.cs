namespace AntiAfk.Core.Abstractions;

/// <summary>
/// Two kinds of member here, and the split is deliberate.
///
/// The waits are seconds long - the marketplace open sequence alone runs about ten - so they are
/// asynchronous and take a cancellation token, otherwise stopping the engine has to sit through the
/// rest of a recovery pass before it takes effect.
///
/// The screen tests read a single pixel and return. There is nothing to cancel and nothing to wait
/// for, so they stay synchronous; making them async would add ceremony to a call that is already
/// instant, and they are polled in a loop where that would show.
/// </summary>
public interface IStateDetector
{
    Task<bool> CheckAndCloseWarningAsync(CancellationToken cancellationToken);

    Task<bool> CheckAndCloseMapAsync(CancellationToken cancellationToken);

    Task SmartStateRecoveryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True on the menu shown after connecting and before character select. It waits for a click
    /// and does not advance on its own.
    /// </summary>
    bool IsAtPreStartMenu();

    bool IsAtCharacterSelect();

    /// <summary>
    /// True when the in-game HUD is up, i.e. the world has loaded and the player is in it. Lets a
    /// caller tell "still loading" from "ready", instead of treating both as unknown.
    /// </summary>
    bool IsInGame();
}
