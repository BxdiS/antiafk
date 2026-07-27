namespace AntiAfk.Core.Abstractions;

/// <summary>
/// The waits in here are seconds long - the marketplace open sequence alone runs about ten. They
/// are asynchronous and take a cancellation token so stopping the engine does not have to sit
/// through the rest of a recovery pass before it takes effect.
/// </summary>
public interface IStateDetector
{
    Task<bool> CheckAndCloseWarningAsync(CancellationToken cancellationToken);
    Task<bool> CheckAndCloseMapAsync(CancellationToken cancellationToken);

    /// Reads a single pixel and returns; no waiting, so it stays synchronous.
    bool IsAtCharacterSelect();

    Task SmartStateRecoveryAsync(CancellationToken cancellationToken);
}
