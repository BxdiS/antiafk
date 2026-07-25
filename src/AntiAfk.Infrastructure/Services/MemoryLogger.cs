using AntiAfk.Core.Abstractions;

namespace AntiAfk.Infrastructure.Services;

public sealed class MemoryLogger : IAppLogger
{
    private const int MaxLines = 5000;

    private readonly object _sync = new();
    private readonly LinkedList<string> _lines = new();

    public event Action<string>? LineLogged;

    public IReadOnlyList<string> Buffer
    {
        get
        {
            lock (_sync)
            {
                return _lines.ToArray();
            }
        }
    }

    public MemoryLogger()
    {
        Write("INFO", $"--- Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
    }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message);
        if (exception is not null)
        {
            Write("ERROR", exception.ToString());
        }
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        lock (_sync)
        {
            _lines.AddLast(line);
            while (_lines.Count > MaxLines)
            {
                _lines.RemoveFirst();
            }
        }

        try
        {
            LineLogged?.Invoke(line);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LineLogged handler threw: {ex.Message}");
        }
    }
}
