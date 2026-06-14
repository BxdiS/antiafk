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
    private readonly Random _random = new();

    private IntPtr _gameHandle;
    private UserWindowInfo? _userWindow;
    private UserWindowInfo? _pendingUserWindow;
    private ScaledCoordinates _coordinates = null!;
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
        EngineRuntime runtime)
    {
        _windowService = windowService;
        _inputService = inputService;
        _stateDetector = stateDetector;
        _gameLauncher = gameLauncher;
        _configService = configService;
        _logger = logger;
        _runtime = runtime;
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

        _stateDetector.SmartStateRecovery();
        RestoreUserWindow("Startup");
        NormalizeBackgroundPhaseAfterRecovery();

        _logger.Info($"Engine ready. Resuming from phase: {_progress.Phase}");
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
