namespace AntiAfk.Core.Abstractions;

public interface IStateDetector
{
    bool CheckAndCloseWarning();
    bool CheckAndCloseMap();
    void SmartStateRecovery();
}
