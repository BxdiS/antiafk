using System.Diagnostics;
using System.IO;
using AntiAfk.App.Services;
using AntiAfk.App.Settings;
using AntiAfk.App.Tray;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Engine;
using AntiAfk.Infrastructure.Localization;

namespace AntiAfk.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly EngineHostService _engineHost;
    private readonly LocalizationService _localization;
    private readonly IConfigService _configService;
    private readonly IAppLogger _logger;

    private readonly ToolStripMenuItem _startStopItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _openLogItem;
    private readonly ToolStripMenuItem _exitItem;

    private SettingsWindow? _settingsWindow;

    private readonly SynchronizationContext _uiContext;

    public TrayApplicationContext(
        EngineHostService engineHost,
        LocalizationService localization,
        IConfigService configService,
        IAppLogger logger)
    {
        _engineHost = engineHost;
        _localization = localization;
        _configService = configService;
        _logger = logger;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _localization.SetLanguage(_configService.Current.Language);

        _startStopItem = new ToolStripMenuItem();
        _settingsItem = new ToolStripMenuItem();
        _openLogItem = new ToolStripMenuItem();
        _exitItem = new ToolStripMenuItem();

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = TrayIconFactory.CreateStoppedIcon(),
            ContextMenuStrip = BuildMenu()
        };

        _engineHost.StatusChanged += status => _uiContext.Post(_ => UpdateTrayVisuals(status), null);
        _engineHost.UserNotificationRequested += message => _uiContext.Post(_ =>
        {
            _notifyIcon.BalloonTipTitle = "Anti-AFK";
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(3000);
        }, null);

        RefreshTexts();
        UpdateTrayVisuals(EngineStatus.Stopped);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        _startStopItem.Click += async (_, _) => await ToggleEngineAsync();
        _settingsItem.Click += (_, _) => OpenSettings();
        _openLogItem.Click += (_, _) => OpenLog();
        _exitItem.Click += async (_, _) => await ExitAsync();

        menu.Items.Add(_startStopItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_openLogItem);
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

        _engineHost.Start();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_configService, _localization);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OpenLog()
    {
        try
        {
            if (!File.Exists(_logger.LogFilePath))
            {
                File.WriteAllText(_logger.LogFilePath, string.Empty);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _logger.LogFilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open log file.", ex);
        }
    }

    private async Task ExitAsync()
    {
        if (_engineHost.IsRunning)
        {
            await _engineHost.StopAsync();
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _engineHost.Dispose();
        ExitThread();
    }

    private void UpdateTrayVisuals(EngineStatus status)
    {
        _notifyIcon.Icon = status switch
        {
            EngineStatus.Running => TrayIconFactory.CreateRunningIcon(),
            EngineStatus.WaitingForGame => TrayIconFactory.CreateWaitingIcon(),
            EngineStatus.Error => TrayIconFactory.CreateStoppedIcon(),
            _ => TrayIconFactory.CreateStoppedIcon()
        };

        _notifyIcon.Text = status switch
        {
            EngineStatus.Running => _localization.Get("tray.running"),
            EngineStatus.WaitingForGame => _localization.Get("tray.waiting_game"),
            EngineStatus.Error => _localization.Get("tray.error"),
            _ => _localization.Get("tray.stopped")
        };

        _startStopItem.Text = _engineHost.IsRunning
            ? _localization.Get("tray.stop")
            : _localization.Get("tray.start");

        RefreshTexts();
    }

    private void RefreshTexts()
    {
        _settingsItem.Text = _localization.Get("tray.settings");
        _openLogItem.Text = _localization.Get("tray.open_log");
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
