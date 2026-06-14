using System.Text.Json;
using AntiAfk.Core.Engine;

namespace AntiAfk.Infrastructure.Services;

public sealed class EngineProgressStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public EngineProgressStore(string? filePath = null)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AntiAfk");
        Directory.CreateDirectory(directory);
        _filePath = filePath ?? Path.Combine(directory, "engine_state.json");
    }

    public void Save(EngineProgress progress)
    {
        var json = JsonSerializer.Serialize(progress, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public EngineProgress LoadOrDefault()
    {
        if (!File.Exists(_filePath))
        {
            return new EngineProgress();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<EngineProgress>(json, JsonOptions) ?? new EngineProgress();
        }
        catch
        {
            return new EngineProgress();
        }
    }
}
