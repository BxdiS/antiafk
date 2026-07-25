using AntiAfk.Core.Abstractions;

namespace AntiAfk.App.Services;

public sealed class LogConsoleService
{
    private Logging.LogConsoleForm? _window;

    public bool IsOpen => _window is { IsDisposed: false };

    public void Show(IAppLogger logger)
    {
        try
        {
            if (_window is { IsDisposed: false })
            {
                try
                {
                    _window.Activate();
                    if (_window.WindowState == FormWindowState.Minimized)
                    {
                        _window.WindowState = FormWindowState.Normal;
                    }
                }
                catch
                {
                    _window = null;
                }

                return;
            }

            _window = new Logging.LogConsoleForm(logger);
            _window.FormClosed += (_, _) => _window = null;
            _window.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogConsoleService.Show error: {ex.Message}");
            _window = null;
        }
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
