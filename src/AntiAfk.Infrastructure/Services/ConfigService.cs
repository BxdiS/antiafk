using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;

namespace AntiAfk.Infrastructure.Services;

public sealed class ConfigService : IConfigService
{
    private readonly object _sync = new();
    private readonly IAppLogger _logger;

    /// What AntiAFK.json held, or null when there is no file next to the executable. Held only to
    /// answer one question at Save time: is the user about to lose this change on the next start?
    private readonly AppConfig? _fromFile;

    private AppConfig _current;

    public ConfigService(LoadedConfig loaded, IAppLogger logger)
    {
        _current = loaded.Config;
        // A copy, not the same instance: the baseline has to stay what the file said for the life
        // of the process, and sharing one object with _current makes that true only for as long as
        // nobody edits a config in place.
        _fromFile = loaded.FromFile ? ConfigFile.Clone(loaded.Config) : null;
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
    /// Replaces the running configuration. In memory only - the app never writes AntiAFK.json, by
    /// design, so that a hand-written config stays exactly as its author wrote it.
    /// </summary>
    public void Save(AppConfig config)
    {
        lock (_sync) _current = config;
        WarnIfTheFileWillWinNextTime(config);
    }

    /// <summary>
    /// Says so when a change made in the settings window contradicts the config file.
    ///
    /// Without this the sequence is silent and looks like a bug: the file says en, the user picks ru
    /// and saves, the app switches to ru, and the next start is back in English with nothing in the
    /// log about why. Settings vanishing on restart is documented behaviour; settings vanishing
    /// because a file quietly outranks them is not something anyone should have to deduce.
    /// </summary>
    private void WarnIfTheFileWillWinNextTime(AppConfig saved)
    {
        if (_fromFile is null)
        {
            return;
        }

        var diverged = new List<string>();

        if (!string.Equals(saved.Language, _fromFile.Language, StringComparison.Ordinal))
        {
            diverged.Add($"language (\"{_fromFile.Language}\" in the file, \"{saved.Language}\" now)");
        }

        if (!string.Equals(saved.LauncherPath, _fromFile.LauncherPath, StringComparison.Ordinal))
        {
            diverged.Add($"launcherPath (\"{_fromFile.LauncherPath}\" in the file, \"{saved.LauncherPath}\" now)");
        }

        if (diverged.Count == 0)
        {
            return;
        }

        _logger.Warning(
            $"Settings changed for this session only: {string.Join("; ", diverged)}. " +
            $"The app never writes {ConfigFile.FileName}, so the file's values come back on the next start. " +
            "Edit the file to make this stick.");
    }
}
