using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Updates;

namespace AntiAfk.App.Services;

public sealed class GitHubUpdateService : IUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private readonly IConfigService _configService;
    private readonly IAppLogger _logger;
    private readonly object _sync = new();
    private static readonly Version CurrentVersion = GetCurrentVersionInternal();

    private System.Threading.Timer? _timer;
    private bool _isChecking;
    private string? _downloadedExePath;
    private string? _downloadedTempDir;

    public event Action<UpdateAvailability>? AvailabilityChanged;

    public UpdateAvailability Availability { get; private set; } = UpdateAvailability.None;
    public bool IsSupported { get; private set; } = true;
    public bool CanApply => Availability == UpdateAvailability.Ready && _downloadedExePath is not null;

    public GitHubUpdateService(IConfigService configService, IAppLogger logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_configService.Current.Update.Enabled)
        {
            _logger.Info("Updates are disabled in config.");
            IsSupported = false;
            return;
        }

        if (Environment.ProcessPath is null)
        {
            _logger.Warning("Could not determine the running executable path; updates disabled.");
            IsSupported = false;
            return;
        }

        _logger.Info($"Current version: {GetCurrentVersion()}");
        await CheckAndDownloadAsync(cancellationToken);
        StartPeriodicCheck();
    }

    public async Task ApplyUpdateAsync()
    {
        if (_downloadedExePath is null || Environment.ProcessPath is null)
        {
            _logger.Warning("No downloaded update to apply.");
            return;
        }

        var currentExePath = Environment.ProcessPath;
        string? tempDir;
        string? newExePath;
        lock (_sync)
        {
            tempDir = _downloadedTempDir;
            newExePath = _downloadedExePath;
        }

        _logger.Info($"Applying update, swapping into: {currentExePath}");

        try
        {
            var pid = Environment.ProcessId;
            var scriptPath = Path.Combine(Path.GetTempPath(), $"antiafk-update-{Guid.NewGuid():N}.bat");
            var script = $"""
                @echo off
                :wait
                tasklist /fi "PID eq {pid}" | find "{pid}" >nul
                if not errorlevel 1 (
                  ping -n 2 127.0.0.1 >nul
                  goto wait
                )
                move /y "{newExePath}" "{currentExePath}" >nul
                rmdir /s /q "{tempDir}" >nul 2>&1
                start "" "{currentExePath}"
                del "%~f0"
                """;
            await File.WriteAllTextAsync(scriptPath, script);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to launch update swap helper.", ex);
            return;
        }

        Environment.Exit(0);
    }

    private void StartPeriodicCheck()
    {
        var hours = Math.Max(1, _configService.Current.Update.CheckIntervalHours);
        _timer = new System.Threading.Timer(
            async _ =>
            {
                try
                {
                    await CheckAndDownloadAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.Error("Periodic update check failed.", ex);
                }
            },
            null,
            TimeSpan.FromHours(hours),
            TimeSpan.FromHours(hours));
    }

    private async Task CheckAndDownloadAsync(CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return;
        }

        lock (_sync)
        {
            if (_isChecking || Availability == UpdateAvailability.Ready)
            {
                return;
            }

            _isChecking = true;
        }

        try
        {
            var settings = _configService.Current.Update;
            var url = $"https://api.github.com/repos/{settings.GitHubOwner}/{settings.GitHubRepo}/releases/latest";
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning($"Update check failed: HTTP {(int)response.StatusCode}");
                return;
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync(stream, GitHubJsonContext.Default.GitHubRelease, cancellationToken);
            if (release is null || release.Draft || release.Prerelease)
            {
                SetAvailability(UpdateAvailability.None);
                return;
            }

            var remoteVersionText = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(remoteVersionText, out var remoteVersion))
            {
                return;
            }

            var currentVersion = GetCurrentVersion();
            if (remoteVersion <= currentVersion)
            {
                _logger.Info($"No update available. Current version: {currentVersion}");
                SetAvailability(UpdateAvailability.None);
                return;
            }

            var asset = release.Assets.Find(a =>
                string.Equals(a.Name, UpdateConstants.ExeAssetName, StringComparison.OrdinalIgnoreCase));
            if (asset is null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            {
                _logger.Warning($"Update {remoteVersion} found but no {UpdateConstants.ExeAssetName} asset attached.");
                return;
            }

            _logger.Info($"Update available: {remoteVersion}");
            SetAvailability(UpdateAvailability.Available);
            SetAvailability(UpdateAvailability.Downloading);

            var tempDir = Path.Combine(Path.GetTempPath(), $"antiafk-update-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var exePath = Path.Combine(tempDir, UpdateConstants.ExeAssetName);

            await using (var fileStream = File.Create(exePath))
            await using (var downloadStream = await HttpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken))
            {
                await downloadStream.CopyToAsync(fileStream, cancellationToken);
            }

            if (new FileInfo(exePath).Length == 0)
            {
                _logger.Error("Downloaded update file is empty.");
                Directory.Delete(tempDir, true);
                SetAvailability(UpdateAvailability.None);
                return;
            }

            if (!await VerifyDownloadedFileAsync(exePath, asset.BrowserDownloadUrl, cancellationToken))
            {
                _logger.Error($"Update {remoteVersion} discarded: integrity check did not pass.");
                Directory.Delete(tempDir, true);
                // Without this the tray would sit on "Downloading" forever, since the state is
                // only ever advanced past Downloading on the success path.
                SetAvailability(UpdateAvailability.None);
                return;
            }

            _downloadedExePath = exePath;
            _downloadedTempDir = tempDir;

            SetAvailability(UpdateAvailability.Ready);
            _logger.Info($"Update {remoteVersion} downloaded and ready to apply.");
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            _logger.Error("Update check failed.", ex);
        }
        finally
        {
            lock (_sync)
            {
                _isChecking = false;
            }
        }
    }

    private static Version GetCurrentVersion() => CurrentVersion;

    private static Version GetCurrentVersionInternal() =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppBranding.TechnicalId}-updater");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    // The downloaded file is about to overwrite the running executable and be started, so
    // "could not verify" must never be treated as "verified". Every failure path returns false
    // and the update is discarded.
    private async Task<bool> VerifyDownloadedFileAsync(string exePath, string downloadUrl, CancellationToken cancellationToken)
    {
        try
        {
            var sha256Path = downloadUrl + ".sha256";
            using var checksumResponse = await HttpClient.GetAsync(sha256Path, cancellationToken);
            if (!checksumResponse.IsSuccessStatusCode)
            {
                _logger.Error(
                    $"Checksum file unavailable (HTTP {(int)checksumResponse.StatusCode}). " +
                    "Rejecting the update: an unverifiable binary is treated as untrusted.");
                return false;
            }

            var checksumText = await checksumResponse.Content.ReadAsStringAsync(cancellationToken);
            var expectedHash = checksumText.Split(' ')[0].ToLower();

            using var sha256 = SHA256.Create();
            await using var stream = File.OpenRead(exePath);
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            var actualHash = BitConverter.ToString(hash).Replace("-", "").ToLower();

            if (expectedHash != actualHash)
            {
                _logger.Error($"Checksum mismatch: expected {expectedHash}, got {actualHash}");
                return false;
            }

            _logger.Info("Download verification passed.");
            return true;
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a verification failure - let the caller handle it as cancellation.
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("Download verification failed. Rejecting the update.", ex);
            return false;
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
