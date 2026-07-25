using AntiAfk.Core.Engine;

namespace AntiAfk.Infrastructure.Services;

public sealed class EngineProgressStore
{
    private EngineProgress _current = new();

    public void Save(EngineProgress progress)
    {
        _current = progress;
    }

    public EngineProgress LoadOrDefault() => _current;
}
