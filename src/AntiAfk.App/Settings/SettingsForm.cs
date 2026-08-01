using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Models;
using AntiAfk.Infrastructure.Localization;
using AntiAfk.Infrastructure.Services;

namespace AntiAfk.App.Settings;

public sealed class SettingsForm : Form
{
    private readonly IConfigService _configService;
    private readonly LocalizationService _localization;
    private readonly AppConfig _workingCopy;

    private readonly Label _languageLabel;
    private readonly ComboBox _languageCombo;
    private readonly Label _launcherPathLabel;
    private readonly TextBox _launcherPathText;
    private readonly Button _browseButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Label _creditsText;

    public SettingsForm(IConfigService configService, LocalizationService localization)
    {
        _configService = configService;
        _localization = localization;
        _workingCopy = CloneConfig(configService.Current);

        Text = AppBranding.DisplayName;
        Width = 560;
        Height = 290;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // ignored - icon is cosmetic only
        }

        _languageLabel = new Label { AutoSize = true, Location = new Point(16, 16) };
        _languageCombo = new ComboBox
        {
            Location = new Point(16, 36),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _languageCombo.Items.AddRange([.. localization.SupportedLanguages]);
        var languageIndex = _languageCombo.Items.IndexOf(_workingCopy.Language);
        _languageCombo.SelectedIndex = languageIndex >= 0 ? languageIndex : 0;

        _launcherPathLabel = new Label { AutoSize = true, Location = new Point(16, 76) };
        _launcherPathText = new TextBox { Location = new Point(16, 96), Width = 420 };
        _browseButton = new Button { Location = new Point(444, 95), Width = 90 };
        _browseButton.Click += BrowseButton_Click;

        _cancelButton = new Button { Location = new Point(354, 140), Width = 100 };
        _cancelButton.Click += (_, _) => Close();

        _saveButton = new Button { Location = new Point(444, 140), Width = 90 };
        _saveButton.Click += SaveButton_Click;

        _creditsText = new Label
        {
            AutoSize = false,
            Location = new Point(16, 190),
            Width = 500,
            Height = 40,
            ForeColor = Color.FromArgb(0x6B, 0x72, 0x80),
            Font = new Font(Font.FontFamily, 8f)
        };

        Controls.AddRange([
            _languageLabel, _languageCombo,
            _launcherPathLabel, _launcherPathText, _browseButton,
            _cancelButton, _saveButton,
            _creditsText
        ]);

        ApplyTexts();
        LoadValues();
    }

    private void ApplyTexts()
    {
        _languageLabel.Text = _localization.Get("settings.language");
        _launcherPathLabel.Text = _localization.Get("settings.launcher_path");
        _saveButton.Text = _localization.Get("settings.save");
        _cancelButton.Text = _localization.Get("settings.cancel");
        _browseButton.Text = _localization.Get("settings.browse");
        _creditsText.Text = _localization.Get("settings.credits");
    }

    private void LoadValues()
    {
        _launcherPathText.Text = LauncherPathResolver.Resolve(_workingCopy.LauncherPath)
            ?? LauncherPathResolver.DefaultLauncherPath;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        _workingCopy.Language = _languageCombo.SelectedItem?.ToString() ?? "ru";
        _workingCopy.LauncherPath = _launcherPathText.Text.Trim();

        _configService.Save(_workingCopy);
        _localization.SetLanguage(_workingCopy.Language);
        Close();
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Executable (*.exe)|*.exe",
            FileName = "Launcher.exe",
            InitialDirectory = Path.GetDirectoryName(LauncherPathResolver.DefaultLauncherPath)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _launcherPathText.Text = dialog.FileName;
        }
    }

    private static AppConfig CloneConfig(AppConfig source) => new()
    {
        Language = source.Language,
        LauncherPath = source.LauncherPath,
        Timings = new TimingSettings
        {
            BackgroundClickDelay = new RandomRange(source.Timings.BackgroundClickDelay.Min, source.Timings.BackgroundClickDelay.Max),
            CycleSleepDelay = new RandomRange(source.Timings.CycleSleepDelay.Min, source.Timings.CycleSleepDelay.Max),
            WalkDuration = new RandomRange(source.Timings.WalkDuration.Min, source.Timings.WalkDuration.Max),
            TurnKeyDuration = new RandomRange(source.Timings.TurnKeyDuration.Min, source.Timings.TurnKeyDuration.Max),
            TurnGapJitter = new RandomRange(source.Timings.TurnGapJitter.Min, source.Timings.TurnGapJitter.Max),
            TurnGapMeanFirst = source.Timings.TurnGapMeanFirst,
            TurnGapMeanSecond = source.Timings.TurnGapMeanSecond,
            FocusSwitchDelay = source.Timings.FocusSwitchDelay,
            InitFocusDelay = source.Timings.InitFocusDelay,
            MarketplaceOpenDelay = source.Timings.MarketplaceOpenDelay,
            TabletOpenDelay = source.Timings.TabletOpenDelay,
            EscDelay = source.Timings.EscDelay,
            WarningClickDelay = source.Timings.WarningClickDelay,
            MapCloseDelay = source.Timings.MapCloseDelay,
            PostTurnDelay = source.Timings.PostTurnDelay,
            PostWalkDelay = source.Timings.PostWalkDelay
        },
        Spawn = new SpawnSettings
        {
            Priority = [.. source.Spawn.Priority]
        },
        Update = new UpdateSettings
        {
            Enabled = source.Update.Enabled,
            GitHubOwner = source.Update.GitHubOwner,
            GitHubRepo = source.Update.GitHubRepo,
            CheckIntervalHours = source.Update.CheckIntervalHours
        }
    };
}
