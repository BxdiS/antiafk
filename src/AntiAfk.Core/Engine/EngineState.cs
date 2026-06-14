namespace AntiAfk.Core.Engine;

public enum EnginePhase
{
    Idle = 0,
    WaitingForGame,
    Initializing,
    BackgroundCategoryClick,
    BackgroundCategoryWait,
    BackgroundAdClick,
    BackgroundAdWait,
    ActiveFocus,
    ExitAd,
    CloseMarketplace,
    CheckMap,
    WalkFirst,
    TurnFirst,
    WalkSecond,
    TurnSecond,
    StateRecovery,
    ReturnFocus,
    CycleSleep
}

public enum EngineStatus
{
    Stopped,
    Running,
    WaitingForGame,
    Error
}

public sealed class EngineProgress
{
    public EnginePhase Phase { get; set; } = EnginePhase.Idle;
    public int LastButtonIndex { get; set; } = -1;
    public bool IsInAd { get; set; }
    public double PendingWalkSeconds { get; set; }
    public double PendingTurnGapMean { get; set; }
    public DateTime? PhaseDeadlineUtc { get; set; }
    public int LastWindowWidth { get; set; }
    public int LastWindowHeight { get; set; }
}
