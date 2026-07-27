using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Screens;

namespace AntiAfk.Infrastructure.Services;

/// <summary>
/// Reads the probes in <see cref="ScreenCatalogue"/> and reports the screen.
///
/// Screen reads are wrapped once, here, rather than at every call site. Failing to read the display
/// is ordinary - it is not readable at all while a display mode is changing, which is most of the
/// window between launching the game and the character-select screen appearing - and the honest
/// answer in that case is <see cref="GameScreen.Unknown"/>, not an exception for the caller to
/// interpret. Five separate try/catch blocks used to do this, each slightly differently.
/// </summary>
public sealed class ScreenRecognizer : IScreenRecognizer
{
    private readonly IScreenCaptureService _screenCapture;
    private readonly IAppLogger _logger;

    private GameScreen _lastReported = GameScreen.Unknown;
    private string _lastReadFailure = string.Empty;

    public ScreenRecognizer(IScreenCaptureService screenCapture, IAppLogger logger)
    {
        _screenCapture = screenCapture;
        _logger = logger;
    }

    public GameScreen Recognize()
    {
        foreach (var (screen, probe) in ScreenCatalogue.InPriorityOrder)
        {
            if (Matches(probe))
            {
                ReportChange(screen);
                return screen;
            }
        }

        ReportChange(GameScreen.Unknown);
        return GameScreen.Unknown;
    }

    public bool Matches(PixelProbe probe)
    {
        if (!TryRead(probe.X, probe.Y, out var r, out var g, out var b))
        {
            return false;
        }

        return probe.Matches(r, g, b);
    }

    private bool TryRead(int x, int y, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;

        try
        {
            (r, g, b) = _screenCapture.GetPixelColor(x, y);
            _lastReadFailure = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            // Polled once a second, so a failing read must not spam the log. Only a change of
            // reason is worth a line.
            if (ex.Message != _lastReadFailure)
            {
                _lastReadFailure = ex.Message;
                _logger.Warning($"Screen read at ({x},{y}) failed: {ex.Message}");
            }

            return false;
        }
    }

    // The screen is polled continuously; only transitions are worth a log line.
    private void ReportChange(GameScreen screen)
    {
        if (screen == _lastReported)
        {
            return;
        }

        _lastReported = screen;
        _logger.Info($"Screen: {screen}");
    }
}
