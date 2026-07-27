using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Screens;

namespace AntiAfk.App.Services;

/// <summary>
/// Drives the login by looking at the screen, not by following a script.
///
/// This replaces AutoLoginService, which held two hardcoded sequences - one for starting at the
/// launcher, one for starting with the game already running - and chose between them once, up front.
/// That is where every login bug came from. The two sequences waited on different pixels: the
/// launcher one treated the server-connected indicator as permission to click, and that indicator
/// belongs to the screen *before* character select, so the clicks went into a screen that was still
/// loading. Only one of the two happened to raise the game window first, and only by accident,
/// because something unrelated did it earlier in that path. Which sequence had been chosen, rather
/// than what was on screen, decided the outcome.
///
/// There is one loop here and no sequences. Recognise the screen, perform the single action that
/// screen calls for, look again. Starting at the launcher and starting in the character-select
/// screen are not different paths - they are the same loop entered at different screens, so they
/// cannot drift apart, and neither can be fixed without fixing the other.
///
/// Adding a step means adding a screen to <see cref="ScreenCatalogue"/>, an action to
/// <see cref="IGameActions"/>, and one case below.
/// </summary>
public sealed class LoginFlowService : IAutoLoginService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// How often a screen we are only waiting on is mentioned, so the log does not go silent.
    private static readonly TimeSpan ProgressLogInterval = TimeSpan.FromSeconds(15);

    /// The whole login, launcher included, including a cold GTA5 start and a slow server.
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How many times an action may be performed on a screen that then does not change.
    ///
    /// Unknown is exempt: loading screens legitimately sit there for minutes and no action is taken
    /// on them anyway. This cap is for a screen we are acting on and getting nowhere with - clicking
    /// a character tile that never advances - where repeating forever would be worse than stopping.
    /// </summary>
    private const int MaxAttemptsPerScreen = 4;

    private readonly IScreenRecognizer _recognizer;
    private readonly IGameActions _actions;
    private readonly IWindowService _windowService;
    private readonly IAppLogger _logger;

    public LoginFlowService(
        IScreenRecognizer recognizer,
        IGameActions actions,
        IWindowService windowService,
        IAppLogger logger)
    {
        _recognizer = recognizer;
        _actions = actions;
        _windowService = windowService;
        _logger = logger;
    }

    public async Task AutoLoginAsync(CancellationToken cancellationToken, int characterSlot = 1, int spawnSlot = 1)
    {
        _logger.Info("Login flow: started.");
        LogScreenGeometry();

        var deadline = DateTime.UtcNow + OverallTimeout;
        var lastActedScreen = (GameScreen?)null;
        var actionsOnScreen = 0;
        var launcherLoginClicked = false;
        var lastProgressLog = DateTime.UtcNow;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    _logger.Warning(
                        $"Login flow: gave up after {OverallTimeout.TotalMinutes:F0} minutes without reaching the game.");
                    return;
                }

                var screen = _recognizer.Recognize();

                if (IsPastLogin(screen))
                {
                    _logger.Info($"Login flow: {screen}. Nothing left to do.");
                    return;
                }

                var acted = await TryActAsync(screen, characterSlot, spawnSlot, launcherLoginClicked, cancellationToken);
                if (acted && screen == GameScreen.Unknown)
                {
                    launcherLoginClicked = true;
                }

                if (!acted)
                {
                    // A screen we wait on, not one we act on. Waiting is not failing: connecting to
                    // the server and loading the world each sit here for minutes. The attempt cap
                    // below must never apply to these - counting polls instead of actions is what
                    // made this give up on ConnectingToServer after four seconds.
                    LogProgressOccasionally(screen, ref lastProgressLog);
                    await Task.Delay(PollInterval, cancellationToken);
                    continue;
                }

                if (screen == lastActedScreen)
                {
                    actionsOnScreen++;
                }
                else
                {
                    lastActedScreen = screen;
                    actionsOnScreen = 1;
                }

                if (actionsOnScreen > MaxAttemptsPerScreen)
                {
                    _logger.Warning(
                        $"Login flow: acted on {screen} {MaxAttemptsPerScreen} times and it never changed. " +
                        "Stopping rather than clicking into it again.");
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Login flow: cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            // Not rethrown: the engine awaits this and should still bind the game window and run its
            // own recovery even if login only partly succeeded.
            _logger.Error("Login flow failed.", ex);
        }
    }

    /// <summary>
    /// Performs the one action this screen calls for. Returns false when the screen is one we wait
    /// on rather than act on, which is the distinction the attempt cap depends on.
    /// </summary>
    private async Task<bool> TryActAsync(
        GameScreen screen,
        int characterSlot,
        int spawnSlot,
        bool launcherLoginClicked,
        CancellationToken cancellationToken)
    {
        switch (screen)
        {
            case GameScreen.CharacterSelect:
                await _actions.ChooseCharacterAsync(ResolveCharacterSlot(characterSlot), cancellationToken);

                // The spawn screen has no probe of its own yet, so it cannot be waited for - this is
                // the one blind step left. It follows the character confirm immediately, and if it
                // misses, the loop comes back round to whatever is actually on screen.
                await _actions.ChooseSpawnAsync(spawnSlot, cancellationToken);
                return true;

            case GameScreen.Unknown:
                // The launcher is a window, not a screen signature, so it is checked here rather
                // than in the catalogue. Clicked once: a second press would land on whatever
                // replaced the button.
                if (!launcherLoginClicked && LauncherWindowIsUp())
                {
                    await _actions.ClickLauncherLoginAsync(cancellationToken);
                    return true;
                }

                // Otherwise this is a load or a transition. Wait.
                return false;

            // Connected, but the character tiles are not up yet. Deliberately nothing: acting on
            // this screen is exactly the bug this rewrite exists to remove.
            case GameScreen.ConnectingToServer:
            default:
                return false;
        }
    }

    /// Screens that mean the login is over, one way or another.
    private static bool IsPastLogin(GameScreen screen) => screen switch
    {
        GameScreen.InGame or GameScreen.Marketplace or GameScreen.MarketplaceWarning or GameScreen.MapOpen => true,
        _ => false
    };

    // The flow can legitimately sit on one screen for minutes. Without this the log goes silent and
    // is indistinguishable from a hang - it was silent for 98 seconds in the reported run.
    private void LogProgressOccasionally(GameScreen screen, ref DateTime lastLog)
    {
        if (DateTime.UtcNow - lastLog < ProgressLogInterval)
        {
            return;
        }

        lastLog = DateTime.UtcNow;
        _logger.Info($"Login flow: still waiting on {screen}.");
    }

    private bool LauncherWindowIsUp() =>
        _windowService.FindMainWindowByProcess(GameConstants.LauncherProcessName) != IntPtr.Zero;

    private int ResolveCharacterSlot(int requested)
    {
        if (requested != 3 || _actions.IsThirdCharacterAvailable())
        {
            return requested;
        }

        _logger.Info("Login flow: third character slot is not purchased; using slot 1.");
        return 1;
    }

    // Every coordinate in the catalogue and the action list is a fixed 1080p value, so the most
    // useful thing a log can say when clicks go nowhere is what the screen actually is.
    private void LogScreenGeometry()
    {
        var (width, height) = _windowService.GetScreenSize();
        _logger.Info($"Login flow: primary screen is {width}x{height}.");

        if (width != ScreenCatalogue.MeasuredWidth || height != ScreenCatalogue.MeasuredHeight)
        {
            _logger.Warning(
                $"Login flow: all coordinates are measured for {ScreenCatalogue.MeasuredWidth}x" +
                $"{ScreenCatalogue.MeasuredHeight} fullscreen at (0,0) and are not scaled. " +
                "On this screen neither the probes nor the clicks will line up.");
        }
    }
}
