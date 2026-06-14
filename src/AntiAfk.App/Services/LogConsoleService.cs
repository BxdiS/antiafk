namespace AntiAfk.App.Services;

public sealed class LogConsoleService
{
    private Logging.LogConsoleWindow? _window;

    public bool IsOpen => _window is { IsLoaded: true };

    public void Show(string logFilePath)
    {
        if (_window is { IsLoaded: true })
        {
            _window.Activate();
            if (_window.WindowState == System.Windows.WindowState.Minimized)
            {
                _window.WindowState = System.Windows.WindowState.Normal;
            }

            return;
        }

        _window = new Logging.LogConsoleWindow(logFilePath);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    public void Close()
    {
        if (_window is null)
        {
            return;
        }

        _window.Close();
        _window = null;
    }
}
