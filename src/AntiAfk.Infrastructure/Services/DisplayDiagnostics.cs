using AntiAfk.Core.Abstractions;
using AntiAfk.Infrastructure.Win32;

namespace AntiAfk.Infrastructure.Services;

/// <summary>
/// Logs the display layout once at startup.
///
/// Everything this app does is expressed in physical screen pixels - Cursor.Position,
/// CopyFromScreen, GetWindowRect - so a display that is not at 100% scale, or a mixed-DPI setup,
/// is the first thing worth checking when clicks land in the wrong place. Without the PerMonitorV2
/// declaration in app.manifest Windows would silently virtualise those coordinates and the
/// mismatch would be invisible both to the app and to whoever reads the log.
/// </summary>
public static class DisplayDiagnostics
{
    private const int DefaultDpi = 96;

    public static void LogDisplayLayout(IAppLogger logger)
    {
        try
        {
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                var scalePercent = TryGetScalePercent(bounds);
                var scaleText = scalePercent is null ? "scale unknown" : $"{scalePercent}% scale";
                var primaryText = screen.Primary ? ", primary" : string.Empty;

                logger.Info(
                    $"Display {screen.DeviceName}: {bounds.Width}x{bounds.Height} " +
                    $"at ({bounds.X},{bounds.Y}), {scaleText}{primaryText}");

                if (scalePercent is not null && scalePercent != 100)
                {
                    logger.Warning(
                        $"Display {screen.DeviceName} is at {scalePercent}% scale. Screen coordinates are " +
                        "calibrated for 100%, so run the game on a 100% display if clicks miss.");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not enumerate displays: {ex.Message}");
        }
    }

    private static int? TryGetScalePercent(System.Drawing.Rectangle bounds)
    {
        try
        {
            var rect = new NativeMethods.Rect
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Right = bounds.Right,
                Bottom = bounds.Bottom
            };

            var monitor = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            // S_OK == 0; any failure just means we log "scale unknown" rather than a wrong number.
            if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out var dpiX, out _) != 0)
            {
                return null;
            }

            return (int)Math.Round(dpiX * 100.0 / DefaultDpi);
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }
}
