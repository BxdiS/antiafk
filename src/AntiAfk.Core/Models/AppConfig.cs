namespace AntiAfk.Core.Models;

public sealed class AppConfig
{
    public string Language { get; set; } = "ru";
    public string LauncherPath { get; set; } = string.Empty;
    public TimingSettings Timings { get; set; } = TimingSettings.CreateDefault();
    public UpdateSettings Update { get; set; } = new();

    public static AppConfig CreateDefault() => new();
}

public sealed class UpdateSettings
{
    public bool Enabled { get; set; } = true;
    public string GitHubOwner { get; set; } = "BxdiS";
    public string GitHubRepo { get; set; } = "antiafk-majestic";
    public int CheckIntervalHours { get; set; } = 6;
}

public sealed class TimingSettings
{
    public RandomRange BackgroundClickDelay { get; set; } = new(25, 30);
    public RandomRange CycleSleepDelay { get; set; } = new(180, 360);
    public RandomRange WalkDuration { get; set; } = new(1.5, 2.5);
    public RandomRange TurnKeyDuration { get; set; } = new(0.08, 0.18);
    public RandomRange TurnGapJitter { get; set; } = new(0.5, 0.5);
    public double TurnGapMeanFirst { get; set; } = 5.0;
    public double TurnGapMeanSecond { get; set; } = 4.0;
    public double FocusSwitchDelay { get; set; } = 0.8;
    public double InitFocusDelay { get; set; } = 1.0;
    public double MarketplaceOpenDelay { get; set; } = 4.5;
    public double TabletOpenDelay { get; set; } = 1.5;
    public double EscDelay { get; set; } = 0.5;
    public double WarningClickDelay { get; set; } = 1.0;
    public double MapCloseDelay { get; set; } = 1.0;
    public double PostTurnDelay { get; set; } = 0.5;
    public double PostWalkDelay { get; set; } = 0.2;

    public static TimingSettings CreateDefault() => new();
}

public sealed class RandomRange
{
    public double Min { get; set; }
    public double Max { get; set; }

    public RandomRange()
    {
    }

    public RandomRange(double min, double max)
    {
        Min = min;
        Max = max;
    }

    public double Sample(Random random)
    {
        if (Math.Abs(Min - Max) < 0.0001)
        {
            return Min;
        }

        return random.NextDouble() * (Max - Min) + Min;
    }
}
