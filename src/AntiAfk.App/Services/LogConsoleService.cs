using AntiAfk.Core.Abstractions;

namespace AntiAfk.App.Services;

public sealed class LogConsoleService
{
    private Logging.LogConsoleForm? _window;

    public bool IsOpen => _window is { IsDisposed: false };

    public void Show(IAppLogger logger)
    {
        if (_window is { IsDisposed: false })
        {
            _window.Activate();
            if (_window.WindowState == FormWindowState.Minimized)
            {
                _window.WindowState = FormWindowState.Normal;
            }

            return;
        }

        _window = new Logging.LogConsoleForm(logger);
        _window.FormClosed += (_, _) => _window = null;
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
