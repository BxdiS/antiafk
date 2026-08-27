using AntiAfk.Core.Constants;
using AntiAfk.Core.Models;

namespace AntiAfk.Core.Engine;

public sealed class EngineRuntime
{
    public ScaledCoordinates? Coordinates { get; set; }
    public IntPtr GameHandle { get; set; }
    public ProjectProfile Profile { get; set; } = ProjectProfile.Majestic;
}
