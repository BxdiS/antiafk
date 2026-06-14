using AntiAfk.Core.Abstractions;

namespace AntiAfk.Infrastructure.Services;

public sealed class FileLogger : IAppLogger, IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;

    public FileLogger(string? logDirectory = null)
    {
        var directory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AntiAfk",
            "logs");
        Directory.CreateDirectory(directory);
        LogFilePath = Path.Combine(directory, $"antiafk-{DateTime.Now:yyyy-MM-dd}.log");
        _writer = new StreamWriter(new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public string LogFilePath { get; }

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
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (_sync)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
