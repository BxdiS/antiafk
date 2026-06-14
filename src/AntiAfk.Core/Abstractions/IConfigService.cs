using AntiAfk.Core.Models;

namespace AntiAfk.Core.Abstractions;

public interface IConfigService
{
    AppConfig Current { get; }
    string ConfigFilePath { get; }
    void Save(AppConfig config);
    void Reload();
}
