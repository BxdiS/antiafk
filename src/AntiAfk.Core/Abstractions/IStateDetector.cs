namespace AntiAfk.Core.Abstractions;

public interface IStateDetector
{
    bool CheckAndCloseWarning();
    bool CheckAndCloseMap();
    bool IsAtCharacterSelect();

    /// <summary>
    /// True when the in-game HUD is up, i.e. the world has loaded and the player is in it.
    /// Lets a caller tell "still loading" from "ready", instead of treating both as unknown.
    /// </summary>
    bool IsInGame();
    void SmartStateRecovery();
}
