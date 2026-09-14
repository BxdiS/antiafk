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
    private readonly Action<string>? _onSettingsSaved;

    private readonly Label _projectLabel;
    private readonly ComboBox _projectCombo;
    private readonly Label _languageLabel;
    private readonly ComboBox _languageCombo;
    private readonly Label _launcherPathLabel;
    private readonly TextBox _launcherPathText;
    private readonly Button _browseButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Label _creditsText;

    public SettingsForm(IConfigService configService, LocalizationService localization, Action<string>? onSettingsSaved = null)
    {
        _configService = configService;
        _localization = localization;
        _workingCopy = CloneConfig(configService.Current);
        _onSettingsSaved = onSettingsSaved;

        Text = AppBranding.DisplayName;
        Width = 560;
        Height = 340;
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

        _projectLabel = new Label { AutoSize = true, Location = new Point(16, 16) };
        _projectCombo = new ComboBox
        {
            Location = new Point(16, 36),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _projectCombo.Items.AddRange(["Majestic RP", "Russia Online"]);
        var projectIndex = _workingCopy.Project == "russia_online" ? 1 : 0;
        _projectCombo.SelectedIndex = projectIndex;

        _languageLabel = new Label { AutoSize = true, Location = new Point(16, 72) };
        _languageCombo = new ComboBox
        {
            Location = new Point(16, 92),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _languageCombo.Items.AddRange([.. localization.SupportedLanguages]);
        var languageIndex = _languageCombo.Items.IndexOf(_workingCopy.Language);
        _languageCombo.SelectedIndex = languageIndex >= 0 ? languageIndex : 0;

        _launcherPathLabel = new Label { AutoSize = true, Location = new Point(16, 132) };
        _launcherPathText = new TextBox { Location = new Point(16, 152), Width = 420 };
        _browseButton = new Button { Location = new Point(444, 151), Width = 90 };
        _browseButton.Click += BrowseButton_Click;

        _cancelButton = new Button { Location = new Point(354, 196), Width = 100 };
        _cancelButton.Click += (_, _) => Close();

        _saveButton = new Button { Location = new Point(444, 196), Width = 90 };
        _saveButton.Click += SaveButton_Click;

        _creditsText = new Label
        {
            AutoSize = false,
            Location = new Point(16, 246),
            Width = 500,
            Height = 40,
            ForeColor = Color.FromArgb(0x6B, 0x72, 0x80),
            Font = new Font(Font.FontFamily, 8f)
        };

        Controls.AddRange([
            _projectLabel, _projectCombo,
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
        _projectLabel.Text = _localization.Get("settings.project");
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
        _workingCopy.Project = _projectCombo.SelectedIndex == 1 ? "russia_online" : "majestic";
        _workingCopy.Language = _languageCombo.SelectedItem?.ToString() ?? "ru";
        _workingCopy.LauncherPath = _launcherPathText.Text.Trim();

        // Subscribe to the event so we can notify the user if a file was created.
        Action<string>? handler = null;
        handler = message =>
        {
            _onSettingsSaved?.Invoke(message);
            _configService.SettingsSaved -= handler;
        };
        _configService.SettingsSaved += handler;

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

    // The window edits a copy so that Cancel really cancels, and Save hands the whole copy back -
    // which means everything the window does not show has to survive the round trip. This used to
    // be twenty-odd hand-copied fields, and every setting added anywhere else had to be remembered
    // here too; a forgotten one reset that setting to its default the moment the user pressed Save,
    // silently. ConfigFile.Clone copies whatever AppConfig currently has.
    private static AppConfig CloneConfig(AppConfig source) => ConfigFile.Clone(source);
}
