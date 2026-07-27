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

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <summary>
    /// States the build and whether a debugger is attached, at the top of every log.
    ///
    /// The two behave differently in a way that matters here and is otherwise invisible: a Debug
    /// build keeps its Debug.WriteLine calls, and under a debugger both those and every caught
    /// exception suspend the process while the debugger services them. The click sequence is built
    /// out of fixed delays between a cursor move and a button press, so those suspensions stretch
    /// the one gap that has to stay tight, and clicks land in the wrong place. The release build
    /// has neither. Working that out from behaviour alone cost several rounds; it is one line.
    /// </summary>
    private static void LogBuildEnvironment(IAppLogger logger)
    {
        var debuggerAttached = System.Diagnostics.Debugger.IsAttached;
        logger.Info($"Build: {BuildConfiguration}. Debugger attached: {debuggerAttached}.");

        if (BuildConfiguration == "Debug" && debuggerAttached)
        {
            logger.Warning(
                "Running a Debug build under a debugger. Every caught exception and every " +
                "Debug.WriteLine suspends this process while the debugger handles it, which " +
                "stretches the fixed delays the click sequence depends on. If clicks land in the " +
                "wrong place, reproduce without the debugger (Ctrl+F5, or run the built exe " +
                "directly) before looking anywhere else.");
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
                var inputService = new InputService(windowService, logger);
                var screenCapture = new ScreenCaptureService();
                var stateDetector = new StateDetector(
                    screenCapture,
                    inputService,
                    logger,
                    runtime,
                    () => configService.Current.Timings);
                var gameLauncher = new GameLauncherService(configService, logger);
                var autoLoginService = new AutoLoginService(logger, screenCapture, inputService, windowService);
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
                LogBuildEnvironment(logger);

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
