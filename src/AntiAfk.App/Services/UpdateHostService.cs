using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Updates;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace AntiAfk.App.Services;

public sealed class UpdateHostService : IUpdateService
{
    private readonly IConfigService _configService;
    private readonly IAppLogger _logger;
    private readonly object _sync = new();

    private UpdateManager? _manager;
    private System.Threading.Timer? _timer;
    private bool _isChecking;

    public event Action<UpdateAvailability>? AvailabilityChanged;

    public UpdateAvailability Availability { get; private set; } = UpdateAvailability.None;
    public bool IsSupported { get; private set; }
    public bool CanApply => Availability == UpdateAvailability.Ready && _manager?.UpdatePendingRestart is not null;

    public UpdateHostService(IConfigService configService, IAppLogger logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_configService.Current.Update.Enabled)
        {
            _logger.Info("Updates are disabled in config.");
            return;
        }

        try
        {
            var settings = _configService.Current.Update;
            var repoUrl = $"https://github.com/{settings.GitHubOwner}/{settings.GitHubRepo}";
            _logger.Info($"Update source: {repoUrl}");

            var source = new GithubSource(repoUrl, null, prerelease: false);
            _manager = new UpdateManager(source);

            if (!_manager.IsInstalled)
            {
                _logger.Warning("Updates work only after installing via Setup.exe (Velopack). dotnet run / copied exe cannot auto-update.");
                return;
            }

            IsSupported = true;
            _logger.Info($"Installed version: {_manager.CurrentVersion}");

            if (_manager.UpdatePendingRestart is not null)
            {
                SetAvailability(UpdateAvailability.Ready);
            }

            await CheckAndDownloadAsync(cancellationToken);
            StartPeriodicCheck();
        }
        catch (NotInstalledException)
        {
            _logger.Warning("Updates work only after installing via Setup.exe (Velopack).");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to initialize updates.", ex);
        }
    }

    public Task ApplyUpdateAsync()
    {
        if (_manager is null)
        {
            return Task.CompletedTask;
        }

        var pending = _manager.UpdatePendingRestart;
        if (pending is null)
        {
            _logger.Warning("No downloaded update to apply.");
            return Task.CompletedTask;
        }

        _logger.Info($"Applying update {pending.Version}...");
        _manager.ApplyUpdatesAndRestart(pending);
        return Task.CompletedTask;
    }

    private void StartPeriodicCheck()
    {
        var hours = Math.Max(1, _configService.Current.Update.CheckIntervalHours);
        _timer = new System.Threading.Timer(
            _ => _ = CheckAndDownloadAsync(CancellationToken.None),
            null,
            TimeSpan.FromHours(hours),
            TimeSpan.FromHours(hours));
    }

    private async Task CheckAndDownloadAsync(CancellationToken cancellationToken)
    {
        if (_manager is null || !IsSupported)
        {
            return;
        }

        lock (_sync)
        {
            if (_isChecking)
            {
                return;
            }

            _isChecking = true;
        }

        try
        {
            if (_manager.UpdatePendingRestart is not null)
            {
                SetAvailability(UpdateAvailability.Ready);
                return;
            }

            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                _logger.Info($"No update available. Current version: {_manager.CurrentVersion}");
                SetAvailability(UpdateAvailability.None);
                return;
            }

            _logger.Info($"Update available: {update.TargetFullRelease.Version}");
            SetAvailability(UpdateAvailability.Available);
            SetAvailability(UpdateAvailability.Downloading);

            await _manager.DownloadUpdatesAsync(update, progress =>
            {
                if (progress % 25 == 0)
                {
                    _logger.Info($"Downloading update: {progress}%");
                }
            }, cancellationToken);

            SetAvailability(UpdateAvailability.Ready);
            _logger.Info($"Update {update.TargetFullRelease.Version} downloaded and ready to apply.");
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            _logger.Error("Update check failed.", ex);
            if (_manager.UpdatePendingRestart is null)
            {
                SetAvailability(UpdateAvailability.None);
            }
        }
        finally
        {
            lock (_sync)
            {
                _isChecking = false;
            }
        }
    }

    private void SetAvailability(UpdateAvailability availability)
    {
        if (Availability == availability)
        {
            return;
        }

        Availability = availability;
        AvailabilityChanged?.Invoke(availability);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
