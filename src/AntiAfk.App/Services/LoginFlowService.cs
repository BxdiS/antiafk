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
        var previousScreen = (GameScreen?)null;
        var attemptsOnScreen = 0;
        var launcherLoginClicked = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    _logger.Warning(
                        $"Login flow: gave up after {OverallTimeout.TotalMinutes:F0} minutes. " +
                        $"Last screen seen: {previousScreen?.ToString() ?? "none"}.");
                    return;
                }

                var screen = _recognizer.Recognize();

                if (screen == previousScreen)
                {
                    attemptsOnScreen++;
                }
                else
                {
                    previousScreen = screen;
                    attemptsOnScreen = 1;
                }

                if (screen != GameScreen.Unknown && attemptsOnScreen > MaxAttemptsPerScreen)
                {
                    _logger.Warning(
                        $"Login flow: {screen} did not change after {MaxAttemptsPerScreen} attempts. " +
                        "Stopping rather than clicking into it again.");
                    return;
                }

                switch (screen)
                {
                    case GameScreen.InGame:
                        _logger.Info("Login flow: in game. Done.");
                        return;

                    // Past login already - the engine's own cycle owns these screens, not this flow.
                    case GameScreen.Marketplace:
                    case GameScreen.MarketplaceWarning:
                    case GameScreen.MapOpen:
                        _logger.Info($"Login flow: {screen} is already past login. Nothing to do.");
                        return;

                    case GameScreen.CharacterSelect:
                        await _actions.ChooseCharacterAsync(ResolveCharacterSlot(characterSlot), cancellationToken);

                        // The spawn screen has no probe of its own yet, so it cannot be waited for -
                        // this is the one blind step left. It follows the character confirm
                        // immediately, and if it misses, the loop comes back round to whatever is
                        // actually on screen rather than carrying on regardless.
                        await _actions.ChooseSpawnAsync(spawnSlot, cancellationToken);
                        continue;

                    // Connected, but the character tiles are not up yet. Explicitly nothing to do:
                    // acting on this screen is precisely the bug this rewrite removes.
                    case GameScreen.ConnectingToServer:
                        break;

                    case GameScreen.Unknown:
                        // The launcher is a window, not a screen signature, so it is checked here
                        // rather than in the catalogue. Clicked once - a second press would land on
                        // whatever replaced the button.
                        if (!launcherLoginClicked && LauncherWindowIsUp())
                        {
                            await _actions.ClickLauncherLoginAsync(cancellationToken);
                            launcherLoginClicked = true;
                            continue;
                        }

                        break;
                }

                await Task.Delay(PollInterval, cancellationToken);
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
