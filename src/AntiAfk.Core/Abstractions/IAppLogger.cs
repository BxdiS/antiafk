namespace AntiAfk.Core.Abstractions;

public interface IAppLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
    IReadOnlyList<string> Buffer { get; }
    event Action<string>? LineLogged;
}
