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

        using var mutex = new Mutex(true, AppBranding.MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                $"{AppBranding.DisplayName} is already running.",
                AppBranding.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            var context = CreateContext();
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

    private static TrayApplicationContext CreateContext()
    {
        try
        {
            var memoryLogger = new MemoryLogger();
            var logConsole = new LogConsoleService();
            var logger = memoryLogger;

            try
            {
                var configService = new ConfigService();
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
                var progressStore = new EngineProgressStore();

                var engine = new AntiAfkEngine(
                    windowService,
                    inputService,
                    stateDetector,
                    gameLauncher,
                    configService,
                    logger,
                    runtime);

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
