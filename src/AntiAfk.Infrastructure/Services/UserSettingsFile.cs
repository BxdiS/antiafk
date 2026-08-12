using System.Text.Json;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;
using AntiAfk.Infrastructure.Localization;

namespace AntiAfk.Infrastructure.Services;

/// <summary>
/// When the user saves settings from the window for the first time, creates AntiAFK.json with
/// the current full configuration.
///
/// AntiAFK.json is normally never written — that design keeps a hand-written config exactly as
/// its author left it, comments and all. But if the file does not exist yet, the first Save
/// creates it populated with everything: language, launcher path, timings, spawn priority, update
/// config. This gives the user a complete working file they can then edit by hand.
///
/// If AntiAFK.json already exists (the user hand-wrote it or we created it before), Save does
/// nothing — we do not overwrite a user's config with what the window changed.
/// </summary>
public static class UserSettingsFile
{
    public const string FileName = "AntiAFK.json";

    public static string FullPath => Path.Combine(AppContext.BaseDirectory, FileName);

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// On first Save after the settings window opens, writes the current full configuration to
    /// AntiAFK.json if that file does not exist yet.
    ///
    /// The return value tells whether a file was created. True means we wrote it; false means
    /// either the file already existed or we could not write it.
    /// </summary>
    public static bool SaveIfNotExists(AppConfig config, IAppLogger logger)
    {
        var path = FullPath;
        if (File.Exists(path))
        {
            logger.Info($"{FileName} already exists. Settings window Save does not overwrite it.");
            return false;
        }

        string json;
        try
        {
            json = JsonSerializer.Serialize(config, WriteOptions);
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not serialize full config for {FileName}: {ex.Message}. File will not be created.");
            return false;
        }

        try
        {
            File.WriteAllText(path, json);
            logger.Info($"First save created {FileName} with the current configuration.");
            return true;
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not write {FileName} ({ex.Message}). File will not be created.");
            return false;
        }
    }
}
