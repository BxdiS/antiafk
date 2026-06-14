using System.Diagnostics;
using AntiAfk.Core.Abstractions;

namespace AntiAfk.Infrastructure.Services;

public sealed class GameLauncherService : IGameLauncher
{
    private readonly IConfigService _configService;
    private readonly IAppLogger _logger;

    public GameLauncherService(IConfigService configService, IAppLogger logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public Task<bool> TryLaunchAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = LauncherPathResolver.Resolve(_configService.Current.LauncherPath);
        if (path is null)
        {
            _logger.Warning($"Game launcher not found. Expected: {LauncherPathResolver.DefaultLauncherPath}");
            return Task.FromResult(false);
        }

        return Task.FromResult(Launch(path));
    }

    private bool Launch(string path)
    {
        try
        {
            _logger.Info($"Starting launcher: {path}");
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to start launcher: {path}", ex);
            return false;
        }
    }
}
