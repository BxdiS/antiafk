using AntiAfk.Core.Abstractions;

namespace AntiAfk.Infrastructure.Services;

public sealed class ConsoleLogger : IAppLogger
{
    public string LogFilePath => string.Empty;

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

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        var color = level switch
        {
            "WARN" => ConsoleColor.Yellow,
            "ERROR" => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
        ConsoleHost.WriteLine(line, color);
    }
}
