using System.Diagnostics;
using System.Text.RegularExpressions;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Infrastructure.Win32;

namespace AntiAfk.Infrastructure.Services;

public sealed class WindowService : IWindowService
{
    private static readonly Regex MajesticTitlePattern = new(
        @"^Majestic Multiplayer\s*\(.+\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public GameWindowInfo? FindGameWindow()
    {
        var byTitle = FindByMajesticTitle();
        if (byTitle is not null)
        {
            return byTitle;
        }

        return FindByGtaProcess();
    }

    private static GameWindowInfo? FindByMajesticTitle()
    {
        GameWindowInfo? bestMatch = null;
        var bestArea = 0L;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            var title = NativeMethods.GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title) || !MajesticTitlePattern.IsMatch(title))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var area = (long)width * height;
            if (area <= bestArea)
            {
                return true;
            }

            bestArea = area;
            bestMatch = CreateWindowInfo(hWnd, title, rect);
            return true;
        }, IntPtr.Zero);

        return bestMatch;
    }

    private static GameWindowInfo? FindByGtaProcess()
    {
        var processIds = Process.GetProcessesByName(GameConstants.GameProcessName)
            .Select(process => process.Id)
            .ToHashSet();

        if (processIds.Count == 0)
        {
            return null;
        }

        GameWindowInfo? bestMatch = null;
        var bestArea = 0L;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
            if (!processIds.Contains((int)processId))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width < 200 || height < 200)
            {
                return true;
            }

            var area = (long)width * height;
            if (area <= bestArea)
            {
                return true;
            }

            var title = NativeMethods.GetWindowTitle(hWnd);
            bestArea = area;
            bestMatch = CreateWindowInfo(hWnd, string.IsNullOrWhiteSpace(title) ? GameConstants.GameProcessName : title, rect);
            return true;
        }, IntPtr.Zero);

        return bestMatch;
    }

    private static GameWindowInfo CreateWindowInfo(IntPtr handle, string title, NativeMethods.Rect rect) =>
        new(handle, title, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    public bool IsWindowValid(IntPtr handle) => handle != IntPtr.Zero && NativeMethods.IsWindow(handle);

    public IntPtr GetForegroundWindow() => NativeMethods.GetForegroundWindow();

    public void ForceForeground(IntPtr handle)
    {
        try
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            NativeMethods.SetForegroundWindow(handle);
        }
        catch
        {
            NativeMethods.keybd_event(0x12, 0, 0, UIntPtr.Zero);
            NativeMethods.SetForegroundWindow(handle);
            NativeMethods.keybd_event(0x12, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
        }
    }

    public (int Width, int Height) GetScreenSize()
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        return (bounds.Width, bounds.Height);
    }
}
