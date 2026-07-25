using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;

namespace AntiAfk.Infrastructure.Services;

public sealed class ConfigService : IConfigService
{
    private AppConfig _current = AppConfig.CreateDefault();

    public AppConfig Current => _current;

    public void Save(AppConfig config)
    {
        _current = config;
    }
}
