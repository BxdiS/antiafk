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
        while (!cancellationToken.IsCancellationRequested)
        {
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
                _logger.Error("Worker crashed. Auto-restarting in 3 seconds...", ex);
                UserNotificationRequested?.Invoke(_localization.Get("notify.engine_restarted"));
                _progressStore.Save(_engine.CreateProgressSnapshot());
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                _engine.LoadProgress(_progressStore.LoadOrDefault());
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
