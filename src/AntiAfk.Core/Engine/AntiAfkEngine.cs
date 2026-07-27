using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Engine;
using AntiAfk.Core.Models;
using AntiAfk.Core.Services;

namespace AntiAfk.Core.Engine;

public sealed class AntiAfkEngine
{
    private readonly IWindowService _windowService;
    private readonly IInputService _inputService;
    private readonly IStateDetector _stateDetector;
    private readonly IGameLauncher _gameLauncher;
    private readonly IConfigService _configService;
    private readonly IAppLogger _logger;
    private readonly EngineRuntime _runtime;
    private readonly IAutoLoginService? _autoLoginService;
    private readonly Random _random = new();

    /// How long startup will wait for the game to reach character select or the world. Covers a
    /// cold GTA5 start and a slow server, which is what the launcher path has to sit through.
    private static readonly TimeSpan PlayableStateTimeout = TimeSpan.FromMinutes(6);

    /// Clicks spent trying to dismiss the pre-game menu before concluding the button is elsewhere.
    private const int MaxPreStartClicks = 3;

    private IntPtr _gameHandle;
    private UserWindowInfo? _userWindow;
    private UserWindowInfo? _pendingUserWindow;
    private ScaledCoordinates? _coordinates;
    private EngineProgress _progress = new();
    private string _gameTitle = string.Empty;
    private bool _startupRecoveryPending = true;

    public event Action<EngineStatus>? StatusChanged;
    public event Action<string>? UserNotificationRequested;

    public EngineStatus Status { get; private set; } = EngineStatus.Stopped;
    public EngineProgress Progress => _progress;

    public AntiAfkEngine(
        IWindowService windowService,
        IInputService inputService,
        IStateDetector stateDetector,
        IGameLauncher gameLauncher,
        IConfigService configService,
        IAppLogger logger,
        EngineRuntime runtime,
        IAutoLoginService? autoLoginService = null)
    {
        _windowService = windowService;
        _inputService = inputService;
        _stateDetector = stateDetector;
        _gameLauncher = gameLauncher;
        _configService = configService;
        _logger = logger;
        _runtime = runtime;
        _autoLoginService = autoLoginService;
    }

    public void LoadProgress(EngineProgress progress)
    {
        _progress = progress;
        _startupRecoveryPending = true;
    }

    public void SetPendingUserWindow(UserWindowInfo? userWindow)
    {
        _pendingUserWindow = userWindow;
    }

    public EngineProgress CreateProgressSnapshot() => new()
    {
        Phase = _progress.Phase,
        LastButtonIndex = _progress.LastButtonIndex,
        IsInAd = _progress.IsInAd,
        PendingWalkSeconds = _progress.PendingWalkSeconds,
        PendingTurnGapMean = _progress.PendingTurnGapMean,
        PhaseDeadlineUtc = _progress.PhaseDeadlineUtc,
        LastWindowWidth = _progress.LastWindowWidth,
        LastWindowHeight = _progress.LastWindowHeight
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        SetStatus(EngineStatus.Running);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await EnsureGameWindowAsync(cancellationToken))
                {
                    continue;
                }

                if (_startupRecoveryPending)
                {
                    await RunStartupRecoveryAsync(cancellationToken);
                    _startupRecoveryPending = false;
                }

                await RunCycleAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Engine stopped by user.");
        }
        catch (Exception ex)
        {
            _logger.Error("Engine crashed.", ex);
            SetStatus(EngineStatus.Error);
            throw;
        }
        finally
        {
            if (Status != EngineStatus.Error)
            {
                SetStatus(EngineStatus.Stopped);
            }
        }
    }

    private async Task<bool> EnsureGameWindowAsync(CancellationToken cancellationToken)
    {
        if (_gameHandle != IntPtr.Zero && _windowService.IsWindowValid(_gameHandle))
        {
            return true;
        }

        var game = _windowService.FindGameWindow();
        if (game is not null)
        {
            BindGameWindow(game);
            return true;
        }

        SetStatus(EngineStatus.WaitingForGame);
        _progress.Phase = EnginePhase.WaitingForGame;
        _logger.Warning("Game window not found. Launching game launcher...");

        var launched = await _gameLauncher.TryLaunchAsync(cancellationToken);
        if (!launched)
        {
            _logger.Warning("Failed to launch game launcher.");
            await DelaySeconds(10, cancellationToken);
            return false;
        }

        // Click the launcher's login button, and nothing more. This is the only step unique to
        // starting from the launcher.
        //
        // This used to run the whole login here - character selection included - before the game
        // window had been found, bound, measured or focused. That is exactly why starting from the
        // launcher behaved differently from starting with the game already running: the other path
        // gets all of that from RunStartupRecoveryAsync first. Now both paths fall through to the
        // same place and the sequence is identical from here on.
        if (_autoLoginService is not null)
        {
            _logger.Info("Clicking the launcher login button...");
            try
            {
                await _autoLoginService.StartLauncherLoginAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("Launcher login failed", ex);
            }
        }
        else
        {
            _logger.Warning("AutoLoginService is not configured - login button will not be clicked automatically");
        }

        for (var attempt = 0; attempt < 60 && !cancellationToken.IsCancellationRequested; attempt++)
        {
            game = _windowService.FindGameWindow();
            if (game is not null)
            {
                BindGameWindow(game);
                SetStatus(EngineStatus.Running);
                return true;
            }

            await DelaySeconds(5, cancellationToken);
        }

        await DelaySeconds(5, cancellationToken);
        return false;
    }

    /// <summary>
    /// Runs the auto-login sequence if the game is sitting on the character-select screen.
    /// Returns true when a login was attempted, so the caller can re-check the outcome.
    /// </summary>
    private async Task<bool> RunAutoLoginIfAtCharacterSelectAsync(string context, CancellationToken cancellationToken)
    {
        if (_autoLoginService is null || !_stateDetector.IsAtCharacterSelect())
        {
            return false;
        }

        _logger.Info($"{context}: character-select screen detected. Running auto-login...");
        try
        {
            await _autoLoginService.AutoLoginAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("Auto-login sequence failed", ex);
        }

        return true;
    }

    private void BindGameWindow(GameWindowInfo game)
    {
        _gameHandle = game.Handle;
        _gameTitle = game.Title;
        _runtime.GameHandle = game.Handle;
        _logger.Info($"Connected to game window: {_gameTitle} ({game.Width}x{game.Height})");
        ApplyScaling(game);
    }

    private async Task RunStartupRecoveryAsync(CancellationToken cancellationToken)
    {
        _logger.Info("Startup: focusing game window and preparing marketplace...");

        RememberUserWindow();
        _windowService.ForceForeground(_gameHandle);
        await DelaySeconds(_configService.Current.Timings.InitFocusDelay, cancellationToken);

        // The game window exists well before the game is ready - coming from the launcher it turns
        // up while the client is still connecting. Wait until the screen is one we can act on
        // rather than acting on a loading screen, which is what pushed the game into the tablet
        // instead of selecting a character.
        await WaitForPlayableStateAsync(cancellationToken);

        // The game may already be running but still sitting on the character-select screen
        // (e.g. the user logged in manually, or started the app mid-flow). In that case the
        // launcher path in EnsureGameWindowAsync was skipped, so auto-login has not run yet.
        // Finish logging in before any marketplace handling.
        await RunAutoLoginIfAtCharacterSelectAsync("Startup", cancellationToken);

        _stateDetector.SmartStateRecovery();
        RestoreUserWindow("Startup");
        NormalizeBackgroundPhaseAfterRecovery();

        _logger.Info($"Engine ready. Resuming from phase: {_progress.Phase}");
    }

    /// <summary>
    /// Waits until the game is either at character select or in the world.
    ///
    /// Returns at once in the common restart case, where it is already one of the two. It only
    /// actually waits when the game is still starting, which is precisely the case the launcher
    /// path used to get wrong by clicking anyway.
    /// </summary>
    private async Task WaitForPlayableStateAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + PlayableStateTimeout;
        var announced = false;
        var preStartClicks = 0;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            // Character select first: it shares its accent colour with the HUD pixel, so the
            // in-game test reports a false positive on it.
            if (_stateDetector.IsAtCharacterSelect())
            {
                _logger.Info("Startup: character-select screen is up.");
                return;
            }

            if (_stateDetector.IsInGame())
            {
                _logger.Info("Startup: already in the world.");
                return;
            }

            // The menu between connecting and character select does not advance on its own - it
            // waits for a click. Waiting for character select without dismissing it first is
            // waiting for something that will never happen.
            if (_stateDetector.IsAtPreStartMenu() && _coordinates is not null)
            {
                if (preStartClicks >= MaxPreStartClicks)
                {
                    _logger.Warning(
                        $"Startup: pre-game menu is still up after {MaxPreStartClicks} clicks at " +
                        $"({_coordinates.CenterX},{_coordinates.CenterY}). The button is probably somewhere else.");
                    return;
                }

                preStartClicks++;
                _logger.Info(
                    $"Startup: pre-game menu detected. Clicking to continue " +
                    $"({_coordinates.CenterX},{_coordinates.CenterY}), attempt {preStartClicks}...");
                _inputService.ClickScreenOnGame(_gameHandle, _coordinates.CenterX, _coordinates.CenterY);

                // Let the click take effect before deciding whether the menu is still there.
                await DelaySeconds(3, cancellationToken);
                continue;
            }

            if (!announced)
            {
                announced = true;
                _logger.Info("Startup: game is still loading. Waiting for character select or the HUD...");
            }

            await DelaySeconds(1, cancellationToken);
        }

        _logger.Warning(
            $"Startup: neither character select nor the HUD appeared within {PlayableStateTimeout.TotalMinutes:F0} " +
            "minutes. Continuing anyway.");
    }

    private void NormalizeBackgroundPhaseAfterRecovery()
    {
        if (_progress.Phase is EnginePhase.Idle or EnginePhase.WaitingForGame or EnginePhase.Initializing)
        {
            _progress.Phase = EnginePhase.BackgroundCategoryClick;
            _progress.PhaseDeadlineUtc = null;
            return;
        }

        if (_progress.Phase is EnginePhase.BackgroundCategoryWait or EnginePhase.BackgroundAdClick or EnginePhase.BackgroundAdWait)
        {
            _logger.Info($"Background phase {_progress.Phase} reset to category click after marketplace recovery.");
            _progress.Phase = EnginePhase.BackgroundCategoryClick;
            _progress.PhaseDeadlineUtc = null;
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var timings = _configService.Current.Timings;

        while (ShouldResumeActivePhase(_progress.Phase) && !cancellationToken.IsCancellationRequested)
        {
            await ExecuteCurrentPhaseAsync(cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_windowService.IsWindowValid(_gameHandle))
            {
                _gameHandle = IntPtr.Zero;
                _runtime.GameHandle = IntPtr.Zero;
                _progress.Phase = EnginePhase.WaitingForGame;
                _startupRecoveryPending = true;
                return;
            }

            CheckResolutionChanged();

            if (_progress.Phase == EnginePhase.BackgroundCategoryClick)
            {
                if (_coordinates == null)
                {
                    _logger.Error("Coordinates not initialized for background click");
                    await DelaySeconds(1, cancellationToken);
                    continue;
                }

                var available = Enumerable.Range(0, _coordinates.Buttons.Count)
                    .Where(i => i != _progress.LastButtonIndex)
                    .ToArray();
                var index = available[_random.Next(available.Length)];
                _progress.LastButtonIndex = index;
                var button = _coordinates.Buttons[index];
                _logger.Info($"[Background] Category click #{index + 1} at ({button.X}, {button.Y})");
                _inputService.MoveAndClickBackground(_gameHandle, button.X, button.Y);
                _progress.Phase = EnginePhase.BackgroundCategoryWait;
                SchedulePhaseDelay(timings.BackgroundClickDelay, "ad click");
            }
            else if (_progress.Phase == EnginePhase.BackgroundCategoryWait)
            {
                if (IsPhaseDeadlineReached())
                {
                    _progress.Phase = EnginePhase.BackgroundAdClick;
                }
                else
                {
                    await DelaySeconds(0.5, cancellationToken);
                    continue;
                }
            }
            else if (_progress.Phase == EnginePhase.BackgroundAdClick)
            {
                if (_coordinates == null)
                {
                    _logger.Error("Coordinates not initialized for ad click");
                    await DelaySeconds(1, cancellationToken);
                    continue;
                }

                var adX = _random.Next(_coordinates.AdZoneX1, _coordinates.AdZoneX2 + 1);
                var adY = _random.Next(_coordinates.AdZoneY1, _coordinates.AdZoneY2 + 1);
                _logger.Info($"[Background] Ad click at ({adX}, {adY})");
                _inputService.MoveAndClickBackground(_gameHandle, adX, adY);
                _progress.IsInAd = true;
                _progress.Phase = EnginePhase.BackgroundAdWait;
                SchedulePhaseDelay(timings.BackgroundClickDelay, "active cycle");
            }
            else
            {
                await ExecuteCurrentPhaseAsync(cancellationToken);
            }
        }
    }

    private async Task ExecuteCurrentPhaseAsync(CancellationToken cancellationToken)
    {
        var timings = _configService.Current.Timings;

        switch (_progress.Phase)
        {
            case EnginePhase.BackgroundAdWait:
                if (IsPhaseDeadlineReached())
                {
                    _progress.Phase = EnginePhase.ActiveFocus;
                }
                else
                {
                    await DelaySeconds(0.5, cancellationToken);
                }
                break;

            case EnginePhase.ActiveFocus:
                _logger.Info("Taking focus for active actions...");
                RememberUserWindow();
                _windowService.ForceForeground(_gameHandle);
                await DelaySeconds(timings.FocusSwitchDelay, cancellationToken);
                _progress.Phase = EnginePhase.ExitAd;
                break;

            case EnginePhase.ExitAd:
                if (_progress.IsInAd)
                {
                    _logger.Info("Leaving ad view (ESC)...");
                    _inputService.SendKeyToGame(_gameHandle, NativeKeys.Escape, 0.1);
                    await DelaySeconds(timings.EscDelay, cancellationToken);
                    _progress.IsInAd = false;
                }
                _progress.Phase = EnginePhase.CloseMarketplace;
                break;

            case EnginePhase.CloseMarketplace:
                _logger.Info("Closing marketplace (ESC x2)...");
                _inputService.SendKeyToGame(_gameHandle, NativeKeys.Escape, 0.1);
                await DelaySeconds(timings.EscDelay, cancellationToken);
                _inputService.SendKeyToGame(_gameHandle, NativeKeys.Escape, 0.1);
                await DelaySeconds(1.0, cancellationToken);
                _progress.Phase = EnginePhase.CheckMap;
                break;

            case EnginePhase.CheckMap:
                _stateDetector.CheckAndCloseMap();
                _progress.PendingWalkSeconds = timings.WalkDuration.Sample(_random);
                _progress.Phase = EnginePhase.WalkFirst;
                break;

            case EnginePhase.WalkFirst:
                _logger.Info($"Walking forward {_progress.PendingWalkSeconds:F2}s (first pass)");
                _inputService.SendKeyToGame(_gameHandle, NativeKeys.W, _progress.PendingWalkSeconds);
                await DelaySeconds(timings.PostWalkDelay, cancellationToken);
                _progress.PendingTurnGapMean = timings.TurnGapMeanFirst;
                _progress.Phase = EnginePhase.TurnFirst;
                break;

            case EnginePhase.TurnFirst:
                PerformTurnSequence(_progress.PendingTurnGapMean);
                _progress.PendingWalkSeconds = timings.WalkDuration.Sample(_random);
                _progress.Phase = EnginePhase.WalkSecond;
                break;

            case EnginePhase.WalkSecond:
                _logger.Info($"Walking forward {_progress.PendingWalkSeconds:F2}s (second pass)");
                _inputService.SendKeyToGame(_gameHandle, NativeKeys.W, _progress.PendingWalkSeconds);
                await DelaySeconds(timings.PostWalkDelay, cancellationToken);
                _progress.PendingTurnGapMean = timings.TurnGapMeanSecond;
                _progress.Phase = EnginePhase.TurnSecond;
                break;

            case EnginePhase.TurnSecond:
                PerformTurnSequence(_progress.PendingTurnGapMean);
                _logger.Info("Second turn completed — not walking forward.");
                await DelaySeconds(timings.PostTurnDelay, cancellationToken);
                _progress.Phase = EnginePhase.StateRecovery;
                break;

            case EnginePhase.StateRecovery:
                _windowService.ForceForeground(_gameHandle);
                await DelaySeconds(0.3, cancellationToken);

                // A server disconnect drops the player back to character select without the game
                // window ever closing, so EnsureGameWindowAsync never notices and startup recovery
                // never re-arms. Log back in here, otherwise the cycle keeps firing marketplace
                // clicks into the character-select screen.
                if (await RunAutoLoginIfAtCharacterSelectAsync("Cycle", cancellationToken))
                {
                    if (_stateDetector.IsAtCharacterSelect())
                    {
                        _logger.Warning("Still at character select after auto-login. Skipping marketplace recovery this cycle.");
                        _progress.Phase = EnginePhase.ReturnFocus;
                        break;
                    }

                    // Auto-login returns the moment the HUD pixel appears, while the world is still
                    // settling. Recovery starts by clicking, and a click sent now lands on whatever
                    // is still transitioning rather than on the target - the same reason every other
                    // action in this engine is separated by a cooldown.
                    await DelaySeconds(timings.InitFocusDelay, cancellationToken);
                }

                _stateDetector.SmartStateRecovery();
                _progress.Phase = EnginePhase.ReturnFocus;
                break;

            case EnginePhase.ReturnFocus:
                RestoreUserWindow("Cycle");
                var sleepSeconds = timings.CycleSleepDelay.Sample(_random);
                _logger.Info($"Cycle complete. Sleeping {sleepSeconds / 60:F2} min.");
                _progress.Phase = EnginePhase.CycleSleep;
                _progress.PhaseDeadlineUtc = DateTime.UtcNow.AddSeconds(sleepSeconds);
                break;

            case EnginePhase.CycleSleep:
                if (IsPhaseDeadlineReached())
                {
                    _progress.Phase = EnginePhase.BackgroundCategoryClick;
                }
                else
                {
                    await DelaySeconds(1.0, cancellationToken);
                }
                break;
        }
    }

    private void RememberUserWindow()
    {
        if (_pendingUserWindow is not null)
        {
            _userWindow = _pendingUserWindow;
            _pendingUserWindow = null;
            _logger.Info($"Saved user window: {DescribeUserWindow(_userWindow)}");
            return;
        }

        var captured = _windowService.CaptureUserWindow(_gameHandle);
        if (captured is null)
        {
            _logger.Warning("Could not detect a user window to restore later.");
            return;
        }

        _userWindow = captured;
        _logger.Info($"Saved user window: {DescribeUserWindow(_userWindow)}");
    }

    private void RestoreUserWindow(string context)
    {
        if (_userWindow is null)
        {
            _logger.Warning($"{context}: no saved user window to restore.");
            return;
        }

        if (_windowService.TryRestoreUserWindow(_userWindow, _gameHandle))
        {
            _logger.Info($"{context}: returned focus to {DescribeUserWindow(_userWindow)}.");
            return;
        }

        _logger.Warning($"{context}: could not restore focus to {DescribeUserWindow(_userWindow)}.");
    }

    private static string DescribeUserWindow(UserWindowInfo? window)
    {
        if (window is null)
        {
            return "(none)";
        }

        return string.IsNullOrWhiteSpace(window.Title) ? "(untitled app)" : $"\"{window.Title}\"";
    }

    private static bool ShouldResumeActivePhase(EnginePhase phase) => phase switch
    {
        EnginePhase.ActiveFocus or EnginePhase.ExitAd or EnginePhase.CloseMarketplace
            or EnginePhase.CheckMap or EnginePhase.WalkFirst or EnginePhase.TurnFirst
            or EnginePhase.WalkSecond or EnginePhase.TurnSecond or EnginePhase.StateRecovery
            or EnginePhase.ReturnFocus or EnginePhase.CycleSleep => true,
        _ => false
    };

    private bool IsPhaseDeadlineReached() =>
        _progress.PhaseDeadlineUtc is null || DateTime.UtcNow >= _progress.PhaseDeadlineUtc.Value;

    private void SchedulePhaseDelay(RandomRange delayRange, string nextStep)
    {
        var delaySeconds = delayRange.Sample(_random);
        _progress.PhaseDeadlineUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
        _logger.Info($"[Background] Waiting {delaySeconds:F0}s before {nextStep}...");
    }

    private void PerformTurnSequence(double gapMean)
    {
        var timings = _configService.Current.Timings;
        var durA = timings.TurnKeyDuration.Sample(_random);
        var durS = timings.TurnKeyDuration.Sample(_random);
        var durC = timings.TurnKeyDuration.Sample(_random);
        var jitter = timings.TurnGapJitter.Min;
        var gap1 = _random.NextDouble() * (jitter * 2) + Math.Max(0.01, gapMean - jitter);
        var gap2 = _random.NextDouble() * (jitter * 2) + Math.Max(0.01, gapMean - jitter);

        _logger.Info($"Turn sequence: A({durA:F2}s) -> {gap1:F2}s -> S({durS:F2}s) -> {gap2:F2}s -> C({durC:F2}s)");
        _inputService.SendKeyToGame(_gameHandle, NativeKeys.A, durA);
        Thread.Sleep(TimeSpan.FromSeconds(gap1));
        _inputService.SendKeyToGame(_gameHandle, NativeKeys.S, durS);
        Thread.Sleep(TimeSpan.FromSeconds(gap2));
        _inputService.SendKeyToGame(_gameHandle, NativeKeys.C, durC);
        Thread.Sleep(TimeSpan.FromSeconds(timings.TurnKeyDuration.Sample(_random)));
    }

    private void ApplyScaling(GameWindowInfo game)
    {
        var screen = _windowService.GetScreenSize();
        _coordinates = CoordinateScaler.Apply(game.Left, game.Top, game.Width, game.Height, screen.Width, screen.Height);
        _runtime.Coordinates = _coordinates;
        _progress.LastWindowWidth = game.Width;
        _progress.LastWindowHeight = game.Height;

        _logger.Info($"Scaling applied: window={game.Width}x{game.Height} @({game.Left},{game.Top})");
    }

    private void CheckResolutionChanged()
    {
        var game = _windowService.FindGameWindow();
        if (game is null || game.Handle != _gameHandle)
        {
            return;
        }

        if (game.Width == _progress.LastWindowWidth && game.Height == _progress.LastWindowHeight)
        {
            return;
        }

        _logger.Warning($"Window size changed from {_progress.LastWindowWidth}x{_progress.LastWindowHeight} to {game.Width}x{game.Height}.");
        UserNotificationRequested?.Invoke("resolution_changed");
        ApplyScaling(game);
    }

    private void SetStatus(EngineStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    private static async Task DelaySeconds(double seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }
}

public static class NativeKeys
{
    public const ushort W = 0x57;
    public const ushort A = 0x41;
    public const ushort S = 0x53;
    public const ushort C = 0x43;
    public const ushort Escape = 0x1B;
    public const ushort Down = 0x28;
}
