using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Engine;
using AntiAfk.Infrastructure.Services;

namespace AntiAfk.App.Services;

public sealed class EngineHostService : IDisposable
{
    private readonly AntiAfkEngine _engine;
    private readonly EngineProgressStore _progressStore;
    private readonly IWindowService _windowService;
    private readonly IAppLogger _logger;
    private readonly AntiAfk.Infrastructure.Localization.LocalizationService _localization;

    // A deterministic crash used to restart forever on a fixed 3s timer, filling the log with
    // identical stack traces. Back off between attempts and stop after a few failures in a row.
    private const int MaxConsecutiveFailures = 5;
    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    // A run that lasted this long was healthy, so a crash after it starts a fresh streak
    // instead of counting towards an older, unrelated one.
    private static readonly TimeSpan StableRunThreshold = TimeSpan.FromMinutes(5);

    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private bool _isRunning;

    public event Action<EngineStatus>? StatusChanged;
    public event Action<string>? UserNotificationRequested;

    public EngineHostService(
        AntiAfkEngine engine,
        EngineProgressStore progressStore,
        IWindowService windowService,
        IAppLogger logger,
        AntiAfk.Infrastructure.Localization.LocalizationService localization)
    {
        _engine = engine;
        _progressStore = progressStore;
        _windowService = windowService;
        _logger = logger;
        _localization = localization;

        _engine.StatusChanged += status => StatusChanged?.Invoke(status);
        _engine.UserNotificationRequested += key =>
        {
            UserNotificationRequested?.Invoke(_localization.Get($"notify.{key}"));
        };
    }

    public bool IsRunning => _isRunning;
    public EngineStatus Status => _engine.Status;

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        var progress = _progressStore.LoadOrDefault();
        if (progress.Phase == EnginePhase.Idle)
        {
            progress.Phase = EnginePhase.WaitingForGame;
        }

        _engine.LoadProgress(progress);
        _engine.SetPendingUserWindow(_windowService.CaptureUserWindow(IntPtr.Zero));

        // Each CancellationTokenSource owns a timer and a wait handle. Start/Stop cycles used to
        // leave the previous one to the finaliser, so they accumulated for the life of the process.
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _isRunning = true;
        _workerTask = Task.Run(() => RunWorkerAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _cts?.Cancel();

        if (_workerTask is not null)
        {
            try
            {
                await _workerTask;
            }
            catch
            {
                // ignored on stop
            }
        }

        _progressStore.Save(_engine.CreateProgressSnapshot());
        _isRunning = false;
        StatusChanged?.Invoke(EngineStatus.Stopped);
    }

    private async Task RunWorkerAsync(CancellationToken cancellationToken)
    {
        var consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var startedUtc = DateTime.UtcNow;

            try
            {
                await _engine.RunAsync(cancellationToken);
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (DateTime.UtcNow - startedUtc >= StableRunThreshold)
                {
                    consecutiveFailures = 0;
                }

                consecutiveFailures++;
                _progressStore.Save(_engine.CreateProgressSnapshot());

                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger.Error(
                        $"Worker crashed {consecutiveFailures} times in a row. Giving up - the failure looks " +
                        "permanent, so restarting again would only repeat it. Press Start to retry.",
                        ex);
                    UserNotificationRequested?.Invoke(_localization.Get("notify.engine_gave_up"));

                    // Clear this before raising the event: the tray reads IsRunning to label the
                    // start/stop item, and it must read "Start" once we have stopped retrying.
                    _isRunning = false;
                    StatusChanged?.Invoke(EngineStatus.Error);
                    return;
                }

                var retryDelay = ComputeRetryDelay(consecutiveFailures);
                _logger.Error(
                    $"Worker crashed (attempt {consecutiveFailures}/{MaxConsecutiveFailures}). " +
                    $"Auto-restarting in {retryDelay.TotalSeconds:F0} seconds...",
                    ex);
                UserNotificationRequested?.Invoke(_localization.Get("notify.engine_restarted"));

                try
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _engine.LoadProgress(_progressStore.LoadOrDefault());
            }
        }
    }

    private static TimeSpan ComputeRetryDelay(int consecutiveFailures)
    {
        var seconds = FirstRetryDelay.TotalSeconds * Math.Pow(2, consecutiveFailures - 1);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryDelay.TotalSeconds));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
