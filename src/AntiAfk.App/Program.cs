using System.Threading;
using AntiAfk.App.Services;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Engine;
using AntiAfk.Infrastructure.Localization;
using AntiAfk.Infrastructure.Services;

namespace AntiAfk.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ShellIntegration.Register();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to register shell integration: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Both of these are built before the single-instance check so the "already running" message
        // comes out in the right language, and so everything the config loader has to say is
        // already in the buffer the log console shows.
        var logger = new MemoryLogger();
        var config = ConfigFile.Load(logger);

        using var mutex = new Mutex(true, AppBranding.MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                CreateLocalization(config.Config.Language).Get("notify.already_running"),
                AppBranding.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            var context = CreateContext(logger, config);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Application failed to start: {ex.Message}\n\n{ex.StackTrace}",
                "Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // The single-instance check runs before the tray context exists, so it builds its own
    // localization rather than showing an English string the translation table already has a key for.
    private static LocalizationService CreateLocalization(string language)
    {
        var localization = new LocalizationService();
        localization.SetLanguage(language);
        return localization;
    }

    private static TrayApplicationContext CreateContext(IAppLogger logger, LoadedConfig loadedConfig)
    {
        try
        {
            var logConsole = new LogConsoleService();

            try
            {
                var configService = new ConfigService(loadedConfig, logger);
                var localization = new LocalizationService();
                localization.SetLanguage(configService.Current.Language);

                var runtime = new EngineRuntime();
                var windowService = new WindowService();
                var inputService = new InputService(windowService);
                var screenCapture = new ScreenCaptureService();
                var stateDetector = new StateDetector(
                    screenCapture,
                    inputService,
                    logger,
                    runtime,
                    () => configService.Current.Timings);
                var gameLauncher = new GameLauncherService(configService, logger);
                var autoLoginService = new AutoLoginService(
                    logger, screenCapture, inputService, windowService, configService);
                var progressStore = new EngineProgressStore();

                var engine = new AntiAfkEngine(
                    windowService,
                    inputService,
                    stateDetector,
                    gameLauncher,
                    configService,
                    logger,
                    runtime,
                    autoLoginService);

                var engineHost = new EngineHostService(engine, progressStore, windowService, logger, localization);
                var updateService = new GitHubUpdateService(configService, logger);

                InitializeUpdateServiceAsync(updateService, logger).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        logger.Error($"Failed to initialize update service: {task.Exception?.InnerException?.Message}");
                    }
                });

                logger.Info($"{AppBranding.DisplayName} started.");

                // Logged up front because every coordinate below depends on it: a display that is
                // not at 100% scale is the first thing to check when clicks land in the wrong place.
                DisplayDiagnostics.LogDisplayLayout(logger);

                return new TrayApplicationContext(engineHost, updateService, localization, configService, logger, logConsole);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize services: {ex.Message}", ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to create application context:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            throw;
        }
    }

    private static async Task InitializeUpdateServiceAsync(IUpdateService updateService, IAppLogger logger)
    {
        try
        {
            await updateService.InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.Error($"Update service initialization error: {ex.Message}", ex);
        }
    }
}
