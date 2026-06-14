using System.Text.Json;
using System.Text.Json.Serialization;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;

namespace AntiAfk.Infrastructure.Services;

public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configFilePath;
    private AppConfig _current;

    public ConfigService(string? configFilePath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "AntiAfk");
        Directory.CreateDirectory(directory);
        _configFilePath = configFilePath ?? Path.Combine(directory, "config.json");
        _current = LoadOrCreate();
    }

    public AppConfig Current => _current;
    public string ConfigFilePath => _configFilePath;

    public void Save(AppConfig config)
    {
        _current = config;
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configFilePath, json);
    }

    public void Reload()
    {
        _current = LoadOrCreate();
    }

    private AppConfig LoadOrCreate()
    {
        if (!File.Exists(_configFilePath))
        {
            var defaults = AppConfig.CreateDefault();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_configFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? AppConfig.CreateDefault();
        }
        catch
        {
            return AppConfig.CreateDefault();
        }
    }
}
