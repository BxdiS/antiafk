using System.IO;
using System.Windows;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Models;
using AntiAfk.Infrastructure.Localization;
using AntiAfk.Infrastructure.Services;
using Microsoft.Win32;

namespace AntiAfk.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly IConfigService _configService;
    private readonly LocalizationService _localization;
    private readonly AppConfig _workingCopy;

    public SettingsWindow(IConfigService configService, LocalizationService localization)
    {
        InitializeComponent();
        _configService = configService;
        _localization = localization;
        _workingCopy = CloneConfig(configService.Current);

        LanguageCombo.ItemsSource = localization.SupportedLanguages;
        LanguageCombo.SelectedItem = _workingCopy.Language;
        ApplyTexts();
        LoadValues();
    }

    private void ApplyTexts()
    {
        Title = AppBranding.DisplayName;
        LanguageLabel.Text = _localization.Get("settings.language");
        LauncherPathLabel.Text = _localization.Get("settings.launcher_path");
        SaveButton.Content = _localization.Get("settings.save");
        CancelButton.Content = _localization.Get("settings.cancel");
        BrowseButton.Content = _localization.Get("settings.browse");
        CreditsText.Text = _localization.Get("settings.credits");
    }

    private void LoadValues()
    {
        LauncherPathText.Text = LauncherPathResolver.Resolve(_workingCopy.LauncherPath)
            ?? LauncherPathResolver.DefaultLauncherPath;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _workingCopy.Language = LanguageCombo.SelectedItem?.ToString() ?? "ru";
        _workingCopy.LauncherPath = LauncherPathText.Text.Trim();

        _configService.Save(_workingCopy);
        _localization.SetLanguage(_workingCopy.Language);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executable (*.exe)|*.exe",
            FileName = "Launcher.exe",
            InitialDirectory = Path.GetDirectoryName(LauncherPathResolver.DefaultLauncherPath)
        };

        if (dialog.ShowDialog() == true)
        {
            LauncherPathText.Text = dialog.FileName;
        }
    }

    private static AppConfig CloneConfig(AppConfig source) => new()
    {
        Language = source.Language,
        LauncherPath = source.LauncherPath,
        Timings = source.Timings,
        Update = source.Update
    };
}
