using System.Threading;
using AntiAfk.App.Services;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Engine;
using AntiAfk.Infrastructure.Localization;
using AntiAfk.Infrastructure.Services;
using Velopack;

namespace AntiAfk.App;

internal static class Program
{
    private const string MutexName = "Global\\AntiAfk.SingleInstance";

    [STAThread]
    private static void Main()
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Anti-AFK is already running.",
                "Anti-AFK",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(CreateContext());
    }

    private static TrayApplicationContext CreateContext()
    {
        var logger = new FileLogger();
        var configService = new ConfigService();
        var localization = new LocalizationService();
        localization.SetLanguage(configService.Current.Language);

        var runtime = new EngineRuntime();
        var windowService = new WindowService();
        var inputService = new InputService();
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

        var engineHost = new EngineHostService(engine, progressStore, logger, localization);
        var updateService = new UpdateHostService(configService, logger);
        _ = updateService.InitializeAsync();

        logger.Info("Anti-AFK started.");

        return new TrayApplicationContext(engineHost, updateService, localization, configService, logger);
    }
}
