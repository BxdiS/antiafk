using AntiAfk.Core.Models;

namespace AntiAfk.Core.Abstractions;

public interface IConfigService
{
    AppConfig Current { get; }
    void Save(AppConfig config);
}
