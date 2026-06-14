using System.Runtime.InteropServices;
using AntiAfk.Core.Constants;

namespace AntiAfk.Infrastructure.Services;

public static class ConsoleHost
{
    private const int SwRestore = 9;
    private const int MaxSessionLines = 5000;

    private static readonly object Sync = new();
    private static readonly List<(string Line, ConsoleColor? Color)> SessionLines = new();
    private static IntPtr _consoleWindow = IntPtr.Zero;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleTitle(string lpConsoleTitle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    public static bool IsOpen
    {
        get
        {
            lock (Sync)
            {
                return _consoleWindow != IntPtr.Zero && IsWindow(_consoleWindow);
            }
        }
    }

    public static void Show()
    {
        lock (Sync)
        {
            EnsureAttached();
            ShowWindow(_consoleWindow, SwRestore);
            SetForegroundWindow(_consoleWindow);
        }
    }

    public static void WriteLine(string line, ConsoleColor? color = null)
    {
        lock (Sync)
        {
            SessionLines.Add((line, color));
            if (SessionLines.Count > MaxSessionLines)
            {
                SessionLines.RemoveRange(0, SessionLines.Count - MaxSessionLines);
            }

            if (_consoleWindow != IntPtr.Zero && IsWindow(_consoleWindow))
            {
                WriteToConsole(line, color);
            }
        }
    }

    private static void EnsureAttached()
    {
        if (_consoleWindow != IntPtr.Zero && IsWindow(_consoleWindow))
        {
            return;
        }

        if (!AllocConsole())
        {
            return;
        }

        SetConsoleTitle($"{AppBranding.DisplayName} — logs");
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        _consoleWindow = GetConsoleWindow();

        WriteToConsole($"{AppBranding.DisplayName} console logging.", ConsoleColor.Gray);
        WriteToConsole("Logs are also saved to the session file in AppData.", ConsoleColor.Gray);
        WriteToConsole(string.Empty, null);

        foreach (var (line, color) in SessionLines)
        {
            WriteToConsole(line, color);
        }
    }

    private static void WriteToConsole(string line, ConsoleColor? color)
    {
        var previousColor = Console.ForegroundColor;
        if (color.HasValue)
        {
            Console.ForegroundColor = color.Value;
        }

        Console.WriteLine(line);

        if (color.HasValue)
        {
            Console.ForegroundColor = previousColor;
        }
    }
}
