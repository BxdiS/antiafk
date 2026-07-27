namespace AntiAfk.Core.Abstractions;

public enum AutoLoginResult
{
    /// The sequence ran to the end and the world finished loading.
    Succeeded,

    /// A step threw, or the world never finished loading. The game is most likely still sitting
    /// on the character-select screen.
    Failed
}

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
    ///
    /// Does not throw on failure - the caller still has its own state recovery to run either way -
    /// so the return value is the only way to tell a completed login from a failed one.
    /// </summary>
    Task<AutoLoginResult> AutoLoginAsync(CancellationToken cancellationToken, int characterSlot = 1);
}
