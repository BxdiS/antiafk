namespace AntiAfk.Core.Abstractions;

/// <summary>
/// Screen reads that report failure by returning false rather than by throwing.
///
/// Failing to read the screen is an ordinary, expected outcome here: the engine polls once a second
/// while the game starts, and through that window the desktop is repeatedly unreadable - GTA5
/// switching to exclusive fullscreen, a display mode change, a locked session. Every caller already
/// treated a throw as "no reading" and wrapped the call in try/catch.
///
/// Expressing that as a return value is not just tidier, it removes a real cost. A caught exception
/// still notifies an attached debugger, which suspends the process to service it - the same stall
/// OutputDebugString causes, and the reason clicks behaved differently under Visual Studio than in
/// the release build. A poll that throws once a second puts that stall right beside the input path.
/// </summary>
public interface IScreenCaptureService
{
    /// <returns>false when the point is off every monitor, or the screen could not be read.</returns>
    bool TryGetPixelColor(int screenX, int screenY, out (byte R, byte G, byte B) color);

    /// <returns>
    /// false when the region is not fully inside a single monitor, or the screen could not be read.
    /// <paramref name="containsColor"/> is only meaningful when this returns true.
    /// </returns>
    bool TryRegionContainsColor(
        int x1,
        int y1,
        int x2,
        int y2,
        Func<byte, byte, byte, bool> predicate,
        out bool containsColor);

    /// <summary>Why the most recent Try call failed. Empty when it succeeded.</summary>
    string LastFailureReason { get; }
}
