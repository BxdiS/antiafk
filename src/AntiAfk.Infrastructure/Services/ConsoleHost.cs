using System.Runtime.InteropServices;
using AntiAfk.Core.Constants;

namespace AntiAfk.Infrastructure.Services;

public static class ConsoleHost
{
    private const int SwRestore = 9;
    private const int CtrlCloseEvent = 2;
    private const int MaxSessionLines = 5000;

    private static readonly object Sync = new();
    private static readonly List<(string Line, ConsoleColor? Color)> SessionLines = new();
    private static readonly ConsoleCtrlHandlerDelegate CloseHandler = OnConsoleControlEvent;
    private static IntPtr _consoleWindow = IntPtr.Zero;
    private static bool _closeHandlerInstalled;

    private delegate bool ConsoleCtrlHandlerDelegate(int ctrlType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleTitle(string lpConsoleTitle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerDelegate handler, bool add);

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
            if (_consoleWindow == IntPtr.Zero)
            {
                return;
            }

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

        InstallCloseHandler();
        ConfigureConsoleStreams();

        SetConsoleTitle($"{AppBranding.DisplayName} — logs");
        _consoleWindow = GetConsoleWindow();

        WriteToConsole($"{AppBranding.DisplayName} console logging.", ConsoleColor.Gray);
        WriteToConsole("Close this window to hide logs. AntiAFK keeps running in the tray.", ConsoleColor.Gray);
        WriteToConsole(string.Empty, null);

        foreach (var (line, color) in SessionLines)
        {
            WriteToConsole(line, color);
        }
    }

    private static void InstallCloseHandler()
    {
        if (_closeHandlerInstalled)
        {
            return;
        }

        if (SetConsoleCtrlHandler(CloseHandler, true))
        {
            _closeHandlerInstalled = true;
        }
    }

    private static void ConfigureConsoleStreams()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var output = Console.OpenStandardOutput();
        Console.SetOut(new StreamWriter(output, System.Text.Encoding.UTF8) { AutoFlush = true });
    }

    private static bool OnConsoleControlEvent(int ctrlType)
    {
        if (ctrlType != CtrlCloseEvent)
        {
            return false;
        }

        lock (Sync)
        {
            FreeConsole();
            _consoleWindow = IntPtr.Zero;
        }

        return true;
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
