using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;

namespace AntiAfk.Infrastructure.Services;

public sealed class ConfigService : IConfigService
{
    private readonly object _sync = new();
    private readonly IAppLogger _logger;

    private AppConfig _current;

    /// <summary>
    /// Fired when the settings window saves, if AntiAFK.json was created. The string is a
    /// user-facing message to show on screen (e.g., a balloon tip).
    /// </summary>
    public event Action<string>? SettingsSaved;

    public ConfigService(LoadedConfig loaded, IAppLogger logger)
    {
        _current = loaded.Config;
        _logger = logger;
    }

    public AppConfig Current
    {
        get
        {
            lock (_sync) return _current;
        }
    }

    /// <summary>
    /// Replaces the running configuration and attempts to persist it.
    ///
    /// On first save (when AntiAFK.json does not exist), creates the file with the full current
    /// configuration. On subsequent saves, does nothing — we do not overwrite a user's config.
    /// AntiAFK.json is still never touched if it already exists — that design keeps a hand-written
    /// config exactly as its author left it.
    ///
    /// If the file is created, SettingsSaved is fired with a message to show the user.
    /// </summary>
    public void Save(AppConfig config)
    {
        lock (_sync) _current = config;

        var created = UserSettingsFile.SaveIfNotExists(config, _logger);
        if (created)
        {
            SettingsSaved?.Invoke($"Settings saved to {UserSettingsFile.FileName}");
        }
    }
}
