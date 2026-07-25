using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;

namespace AntiAfk.Infrastructure.Services;

public sealed class ConfigService : IConfigService
{
    private readonly object _sync = new();
    private AppConfig _current = AppConfig.CreateDefault();

    public AppConfig Current
    {
        get
        {
            lock (_sync) return _current;
        }
    }

    public void Save(AppConfig config)
    {
        lock (_sync) _current = config;
    }
}
