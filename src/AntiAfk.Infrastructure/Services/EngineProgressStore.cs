using AntiAfk.Core.Engine;

namespace AntiAfk.Infrastructure.Services;

public sealed class EngineProgressStore
{
    private readonly object _sync = new();
    private EngineProgress _current = new();

    public void Save(EngineProgress progress)
    {
        lock (_sync) _current = progress;
    }

    public EngineProgress LoadOrDefault()
    {
        lock (_sync) return _current;
    }
}
