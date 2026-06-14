namespace AntiAfk.Infrastructure.Updates;

/// <summary>
/// Placeholder for Velopack-based GitHub auto-updates.
/// See README for the planned seamless update flow.
/// </summary>
public sealed class UpdateService
{
    public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
