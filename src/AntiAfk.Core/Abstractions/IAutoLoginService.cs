namespace AntiAfk.Core.Abstractions;

public interface IAutoLoginService
{
    Task AutoLoginAsync(CancellationToken cancellationToken, int characterSlot = 1, int spawnSlot = 1);
}
