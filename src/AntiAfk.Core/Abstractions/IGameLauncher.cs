namespace AntiAfk.Core.Abstractions;

public interface IGameLauncher
{
    Task<bool> TryLaunchAsync(CancellationToken cancellationToken);
}
