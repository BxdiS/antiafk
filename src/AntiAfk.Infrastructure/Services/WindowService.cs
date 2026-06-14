using System.Diagnostics;
using System.Text.RegularExpressions;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Infrastructure.Win32;

namespace AntiAfk.Infrastructure.Services;

public sealed class WindowService : IWindowService
{
    private static readonly int CurrentProcessId = Environment.ProcessId;

    private static readonly HashSet<string> ExcludedWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "DV2ControlHost",
        "Windows.UI.Core.CoreWindow",
        "ForegroundStaging",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow",
        "#32768",
        "DropDown",
        "Progman",
        "WorkerW"
    };

    private static readonly HashSet<string> AllowedUntitledClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Chrome_WidgetWin_1",
        "MozillaWindowClass",
        "ApplicationFrameWindow",
        "OpWindow",
        "CASCADIA_HOSTING_WINDOW_CLASS"
    };

    // Window title format required by the GTA V multiplayer client (version in parentheses).
    private static readonly Regex GameTitlePattern = new(
        @"^Majestic Multiplayer\s*\(.+\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public GameWindowInfo? FindGameWindow()
    {
        var byTitle = FindByGameTitle();
        if (byTitle is not null)
        {
            return byTitle;
        }

        return FindByGtaProcess();
    }

    private static GameWindowInfo? FindByGameTitle()
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
            if (string.IsNullOrWhiteSpace(title) || !GameTitlePattern.IsMatch(title))
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
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle))
        {
            return;
        }

        if (NativeMethods.GetForegroundWindow() == handle)
        {
            return;
        }

        NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);

        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var targetThread = NativeMethods.GetWindowThreadProcessId(handle, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        var attachedForeground = false;
        var attachedTarget = false;

        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attachedForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            }

            if (targetThread != 0 && targetThread != currentThread)
            {
                attachedTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
            }

            NativeMethods.BringWindowToTop(handle);
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HwndTopmost,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpShowwindow);
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HwndNotopmost,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpShowwindow);
            NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            if (attachedTarget)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }

            if (attachedForeground)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        if (NativeMethods.GetForegroundWindow() != handle)
        {
            NativeMethods.keybd_event(0x12, 0, 0, UIntPtr.Zero);
            NativeMethods.SetForegroundWindow(handle);
            NativeMethods.keybd_event(0x12, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
        }
    }

    public UserWindowInfo? CaptureUserWindow(IntPtr gameHandle)
    {
        for (var handle = NativeMethods.GetForegroundWindow();
             handle != IntPtr.Zero;
             handle = NativeMethods.GetWindow(handle, NativeMethods.GwHwndPrev))
        {
            if (TryCreateUserWindowInfo(handle, gameHandle, out var info))
            {
                return info;
            }
        }

        UserWindowInfo? best = null;
        var bestArea = 0L;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!TryCreateUserWindowInfo(hWnd, gameHandle, out var info))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
            var hasTitle = string.IsNullOrWhiteSpace(info.Title) ? 0 : 1;
            var score = hasTitle * 10_000_000_000L + area;
            if (score <= bestArea)
            {
                return true;
            }

            bestArea = score;
            best = info;
            return true;
        }, IntPtr.Zero);

        return best;
    }

    public bool TryRestoreUserWindow(UserWindowInfo? userWindow, IntPtr gameHandle)
    {
        if (userWindow is null)
        {
            return false;
        }

        if (TryCreateUserWindowInfo(userWindow.Handle, gameHandle, out var current) &&
            current.Handle == userWindow.Handle)
        {
            ForceForeground(userWindow.Handle);
            return true;
        }

        if (string.IsNullOrWhiteSpace(userWindow.Title))
        {
            return false;
        }

        UserWindowInfo? match = null;
        var bestArea = 0L;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!TryCreateUserWindowInfo(hWnd, gameHandle, out var info))
            {
                return true;
            }

            if (!string.Equals(info.Title, userWindow.Title, StringComparison.Ordinal))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
            if (area <= bestArea)
            {
                return true;
            }

            bestArea = area;
            match = info;
            return true;
        }, IntPtr.Zero);

        if (match is null)
        {
            return false;
        }

        ForceForeground(match.Handle);
        return true;
    }

    private static bool TryCreateUserWindowInfo(IntPtr handle, IntPtr gameHandle, out UserWindowInfo info)
    {
        info = new UserWindowInfo(IntPtr.Zero, string.Empty);
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle) || !NativeMethods.IsWindowVisible(handle))
        {
            return false;
        }

        if (gameHandle != IntPtr.Zero && handle == gameHandle)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        if ((int)processId == CurrentProcessId)
        {
            return false;
        }

        if (processId != 0)
        {
            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (string.Equals(process.ProcessName, GameConstants.GameProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch
            {
                // Process may exit between enumeration and lookup.
            }
        }

        var className = NativeMethods.GetWindowClassName(handle);
        if (ExcludedWindowClasses.Contains(className))
        {
            return false;
        }

        var title = NativeMethods.GetWindowTitle(handle);
        if (string.IsNullOrWhiteSpace(title) && !AllowedUntitledClasses.Contains(className))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(handle, out var rect))
        {
            return false;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width < 200 || height < 120)
        {
            return false;
        }

        info = new UserWindowInfo(handle, title);
        return true;
    }

    public (int Width, int Height) GetScreenSize()
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        return (bounds.Width, bounds.Height);
    }
}
