using AntiAfk.App.Services;
using AntiAfk.App.Settings;
using AntiAfk.App.Tray;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Engine;
using AntiAfk.Core.Updates;
using AntiAfk.Infrastructure.Localization;

namespace AntiAfk.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly EngineHostService _engineHost;
    private readonly IUpdateService _updateService;
    private readonly LocalizationService _localization;
    private readonly IConfigService _configService;
    private readonly IAppLogger _logger;
    private readonly LogConsoleService _logConsole;

    private readonly ToolStripMenuItem _startStopItem;
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _openLogItem;
    private readonly ToolStripMenuItem _aboutItem;
    private readonly ToolStripMenuItem _exitItem;

    private SettingsForm? _settingsWindow;
    private EngineStatus _engineStatus = EngineStatus.Stopped;
    private UpdateAvailability _updateAvailability = UpdateAvailability.None;

    private readonly SynchronizationContext _uiContext;

    public TrayApplicationContext(
        EngineHostService engineHost,
        IUpdateService updateService,
        LocalizationService localization,
        IConfigService configService,
        IAppLogger logger,
        LogConsoleService logConsole)
    {
        _engineHost = engineHost;
        _updateService = updateService;
        _localization = localization;
        _configService = configService;
        _logger = logger;
        _logConsole = logConsole;
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _localization.SetLanguage(_configService.Current.Language);

        _startStopItem = new ToolStripMenuItem();
        _updateItem = new ToolStripMenuItem();
        _settingsItem = new ToolStripMenuItem();
        _openLogItem = new ToolStripMenuItem();
        _aboutItem = new ToolStripMenuItem();
        _exitItem = new ToolStripMenuItem();

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = TrayIconFactory.CreateStoppedIcon(),
            ContextMenuStrip = BuildMenu()
        };

        _engineHost.StatusChanged += status => _uiContext.Post(_ =>
        {
            _engineStatus = status;
            UpdateTrayVisuals();
        }, null);

        _engineHost.UserNotificationRequested += message => _uiContext.Post(_ =>
        {
            _notifyIcon.BalloonTipTitle = AppBranding.DisplayName;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(3000);
        }, null);

        _updateService.AvailabilityChanged += availability => _uiContext.Post(_ =>
        {
            _updateAvailability = availability;
            UpdateTrayVisuals();
        }, null);

        RefreshTexts();
        UpdateTrayVisuals();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        _startStopItem.Click += async (_, _) => await SafeAsync(ToggleEngineAsync);
        _updateItem.Click += async (_, _) => await SafeAsync(ApplyUpdateAsync);
        _settingsItem.Click += (_, _) => Safe(OpenSettings);
        _openLogItem.Click += (_, _) => Safe(OpenLog);
        _aboutItem.Click += (_, _) => Safe(ShowAbout);
        _exitItem.Click += async (_, _) => await SafeAsync(ExitAsync);

        menu.Items.Add(_startStopItem);
        menu.Items.Add(_updateItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_openLogItem);
        menu.Items.Add(_aboutItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);
        return menu;
    }

    private async Task ToggleEngineAsync()
    {
        if (_engineHost.IsRunning)
        {
            await _engineHost.StopAsync();
            return;
        }

        if (string.IsNullOrEmpty(_configService.Current.Project))
        {
            var chosen = PromptForProject();
            if (chosen is null)
                return;

            _configService.Current.Project = chosen;

            var displayName = chosen == "russia_online"
                ? "Russia Online"
                : "Majestic RP";

            _notifyIcon.BalloonTipTitle = AppBranding.DisplayName;
            _notifyIcon.BalloonTipText = _localization.Get("notify.project_chosen")
                .Replace("{project}", displayName);
            _notifyIcon.ShowBalloonTip(3000);
        }

        _engineHost.Start();
    }

    private string? PromptForProject()
    {
        using var form = new Form
        {
            Text = AppBranding.DisplayName,
            Width = 340,
            Height = 180,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        try
        {
            form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        var label = new Label
        {
            Text = _localization.Get("project.choose"),
            AutoSize = true,
            Location = new Point(16, 16)
        };

        var combo = new ComboBox
        {
            Location = new Point(16, 44),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        combo.Items.AddRange(["Majestic RP", "Russia Online"]);
        combo.SelectedIndex = 0;

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(196, 90),
            Width = 100,
            DialogResult = DialogResult.OK
        };

        form.Controls.AddRange([label, combo, ok]);
        form.AcceptButton = ok;

        if (form.ShowDialog() != DialogResult.OK)
            return null;

        return combo.SelectedIndex == 1 ? "russia_online" : "majestic";
    }

    private async Task ApplyUpdateAsync()
    {
        if (!_updateService.CanApply)
        {
            return;
        }

        if (_engineHost.IsRunning)
        {
            await _engineHost.StopAsync();
        }

        await _updateService.ApplyUpdateAsync();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsDisposed: false })
        {
            _settingsWindow.Activate();
            return;
        }

        void OnSettingsSaved(string message)
        {
            _notifyIcon.BalloonTipTitle = AppBranding.DisplayName;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(3000);
        }

        _settingsWindow = new SettingsForm(_configService, _localization, OnSettingsSaved);
        _settingsWindow.FormClosed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            _localization.Get("settings.credits"),
            AppBranding.DisplayName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenLog() => _logConsole.Show(_logger);

    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.Error("Unhandled UI action error.", ex);
        }
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.Error("Unhandled UI action error.", ex);
        }
    }

    private async Task ExitAsync()
    {
        if (_engineHost.IsRunning)
        {
            await _engineHost.StopAsync();
        }

        // Hiding it takes the icon out of the tray straight away. Disposing it is left to
        // Dispose(bool), which Application.Run calls on the context once the loop exits.
        _notifyIcon.Visible = false;
        _engineHost.Dispose();
        _updateService.Dispose();
        _logConsole.Close();
        (_logger as IDisposable)?.Dispose();
        ExitThread();
    }

    private void UpdateTrayVisuals()
    {
        _notifyIcon.Icon = GetTrayIcon();
        _notifyIcon.Text = GetTrayTooltip();

        _startStopItem.Text = _engineHost.IsRunning
            ? _localization.Get("tray.stop")
            : _localization.Get("tray.start");

        _updateItem.Visible = _updateAvailability != UpdateAvailability.None;
        _updateItem.Enabled = _updateService.CanApply;
        _updateItem.Text = _updateAvailability switch
        {
            UpdateAvailability.Downloading => _localization.Get("tray.update_downloading"),
            UpdateAvailability.Ready => _localization.Get("tray.update"),
            _ => _localization.Get("tray.update_pending")
        };

        RefreshTexts();
    }

    private Icon GetTrayIcon()
    {
        if (_updateAvailability != UpdateAvailability.None)
        {
            return TrayIconFactory.CreateUpdateIcon();
        }

        return _engineStatus switch
        {
            EngineStatus.Running => TrayIconFactory.CreateRunningIcon(),
            EngineStatus.WaitingForGame => TrayIconFactory.CreateWaitingIcon(),
            EngineStatus.Error => TrayIconFactory.CreateStoppedIcon(),
            _ => TrayIconFactory.CreateStoppedIcon()
        };
    }

    private string GetTrayTooltip()
    {
        if (_updateAvailability == UpdateAvailability.Ready)
        {
            return _localization.Get("tray.update_ready");
        }

        if (_updateAvailability == UpdateAvailability.Downloading)
        {
            return _localization.Get("tray.update_downloading");
        }

        if (_updateAvailability == UpdateAvailability.Available)
        {
            return _localization.Get("tray.update_pending");
        }

        return _engineStatus switch
        {
            EngineStatus.Running => _localization.Get("tray.running"),
            EngineStatus.WaitingForGame => _localization.Get("tray.waiting_game"),
            EngineStatus.Error => _localization.Get("tray.error"),
            _ => _localization.Get("tray.stopped")
        };
    }

    private void RefreshTexts()
    {
        _settingsItem.Text = _localization.Get("tray.settings");
        _openLogItem.Text = _localization.Get("tray.open_log");
        _aboutItem.Text = _localization.Get("tray.about");
        _exitItem.Text = _localization.Get("tray.exit");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
