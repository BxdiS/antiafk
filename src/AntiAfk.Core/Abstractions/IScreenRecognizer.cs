using AntiAfk.Core.Screens;

namespace AntiAfk.Core.Abstractions;

/// <summary>
/// Answers "what is on screen right now". The only component that reads pixels.
/// </summary>
public interface IScreenRecognizer
{
    /// <summary>
    /// The first screen in <see cref="ScreenCatalogue.InPriorityOrder"/> whose probe matches, or
    /// <see cref="GameScreen.Unknown"/>. Never throws: an unreadable display is Unknown, which is
    /// the truth - during a mode change or a load there genuinely is nothing to read.
    /// </summary>
    GameScreen Recognize();

    /// <summary>Evaluates a single probe, for the checks that are not a whole screen.</summary>
    bool Matches(PixelProbe probe);
}
