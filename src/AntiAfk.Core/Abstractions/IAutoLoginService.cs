namespace AntiAfk.Core.Abstractions;

public interface IAutoLoginService
{
    /// <summary>
    /// Waits for the launcher UI and clicks its login button.
    ///
    /// The only step specific to starting from the launcher. Everything after it - finding and
    /// binding the game window, taking focus, waiting for a screen that can be acted on, selecting
    /// a character - is shared with the case where the game is already running, so the two cannot
    /// behave differently.
    /// </summary>
    Task StartLauncherLoginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Selects a character and a spawn point, then waits for the world to load. The
    /// character-select screen is assumed to be on screen already; the caller checks that.
    /// </summary>
    Task AutoLoginAsync(CancellationToken cancellationToken, int characterSlot = 1, int spawnSlot = 1);
}
