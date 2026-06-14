using AntiAfk.Core.Abstractions;

namespace AntiAfk.Infrastructure.Services;

public sealed class CompositeLogger : IAppLogger, IDisposable
{
    private readonly IAppLogger[] _loggers;

    public CompositeLogger(params IAppLogger[] loggers)
    {
        _loggers = loggers;
    }

    public string LogFilePath => _loggers.FirstOrDefault(logger => !string.IsNullOrWhiteSpace(logger.LogFilePath))?.LogFilePath
        ?? string.Empty;

    public void Info(string message)
    {
        foreach (var logger in _loggers)
        {
            logger.Info(message);
        }
    }

    public void Warning(string message)
    {
        foreach (var logger in _loggers)
        {
            logger.Warning(message);
        }
    }

    public void Error(string message, Exception? exception = null)
    {
        foreach (var logger in _loggers)
        {
            logger.Error(message, exception);
        }
    }

    public void Dispose()
    {
        foreach (var logger in _loggers)
        {
            if (logger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
