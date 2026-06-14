namespace AntiAfk.Core.Models;

public sealed class ScaledCoordinates
{
    public required IReadOnlyList<(int X, int Y)> Buttons { get; init; }
    public int AdZoneX1 { get; init; }
    public int AdZoneY1 { get; init; }
    public int AdZoneX2 { get; init; }
    public int AdZoneY2 { get; init; }
    public int CenterX { get; init; }
    public int CenterY { get; init; }
    public int IconX { get; init; }
    public int IconY { get; init; }
    public int WarnBoxX1 { get; init; }
    public int WarnBoxY1 { get; init; }
    public int WarnBoxX2 { get; init; }
    public int WarnBoxY2 { get; init; }
    public int WarnClickX { get; init; }
    public int WarnClickY { get; init; }
    public int MapPixelX { get; init; }
    public int MapPixelY { get; init; }
    public int HudPixelX { get; init; }
    public int HudPixelY { get; init; }
    public int MpPixelX { get; init; }
    public int MpPixelY { get; init; }
}
