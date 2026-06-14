using AntiAfk.Core.Updates;

namespace AntiAfk.Core.Abstractions;

public interface IUpdateService : IDisposable
{
    event Action<UpdateAvailability>? AvailabilityChanged;

    UpdateAvailability Availability { get; }
    bool IsSupported { get; }
    bool CanApply { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ApplyUpdateAsync();
}
