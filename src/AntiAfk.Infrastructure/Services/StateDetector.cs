using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Engine;
using AntiAfk.Core.Models;

namespace AntiAfk.Infrastructure.Services;

public sealed class StateDetector : IStateDetector
{
    private readonly IScreenCaptureService _screenCapture;
    private readonly IInputService _inputService;
    private readonly IAppLogger _logger;
    private readonly EngineRuntime _runtime;
    private readonly Func<TimingSettings> _timingsProvider;

    public StateDetector(
        IScreenCaptureService screenCapture,
        IInputService inputService,
        IAppLogger logger,
        EngineRuntime runtime,
        Func<TimingSettings> timingsProvider)
    {
        _screenCapture = screenCapture;
        _inputService = inputService;
        _logger = logger;
        _runtime = runtime;
        _timingsProvider = timingsProvider;
    }

    public bool CheckAndCloseWarning()
    {
        var coords = _runtime.Coordinates ?? throw new InvalidOperationException("Coordinates are not initialized.");
        var gameHandle = RequireGameHandle();
        var found = _screenCapture.RegionContainsColor(
            coords.WarnBoxX1,
            coords.WarnBoxY1,
            coords.WarnBoxX2,
            coords.WarnBoxY2,
            static (r, g, b) => r > 180 && g < 100 && b < 100);

        if (!found)
        {
            return false;
        }

        _logger.Warning($"Warehouse notification detected. Clicking ({coords.WarnClickX}, {coords.WarnClickY})...");
        _inputService.ClickScreenOnGame(gameHandle, coords.WarnClickX, coords.WarnClickY);
        Thread.Sleep(TimeSpan.FromSeconds(_timingsProvider().WarningClickDelay));
        return true;
    }

    public bool CheckAndCloseMap()
    {
        var coords = _runtime.Coordinates ?? throw new InvalidOperationException("Coordinates are not initialized.");
        var gameHandle = RequireGameHandle();
        var (r, g, b) = _screenCapture.GetPixelColor(coords.MapPixelX, coords.MapPixelY);
        if (r > 200 && g < 40 && b is >= 80 and <= 140)
        {
            _logger.Warning("Map menu detected. Closing with ESC...");
            _inputService.SendKeyToGame(gameHandle, NativeKeys.Escape, 0.1);
            Thread.Sleep(TimeSpan.FromSeconds(_timingsProvider().MapCloseDelay));
            return true;
        }

        return false;
    }

    public void SmartStateRecovery()
    {
        var coords = _runtime.Coordinates ?? throw new InvalidOperationException("Coordinates are not initialized.");
        var timings = _timingsProvider();
        var gameHandle = RequireGameHandle();

        _logger.Info("Analyzing UI state...");

        var (rHud, gHud, bHud) = _screenCapture.GetPixelColor(coords.HudPixelX, coords.HudPixelY);
        var (rMp, gMp, bMp) = _screenCapture.GetPixelColor(coords.MpPixelX, coords.MpPixelY);

        if (rMp is >= 15 and <= 50 && gMp is >= 45 and <= 90 && bMp is >= 85 and <= 130)
        {
            _logger.Info("Status: Marketplace open but overlay present. Closing overlay...");
            CheckAndCloseWarning();
            return;
        }

        if (rMp is >= 40 and <= 85 && gMp is >= 110 and <= 160 && bMp is >= 190 and <= 245)
        {
            _logger.Info("Status: Marketplace active.");
            return;
        }

        if (rHud >= 200 && gHud <= 60 && bHud is >= 80 and <= 170)
        {
            _logger.Info("Status: In game. Opening tablet and marketplace...");
            OpenMarketplace(gameHandle, coords, timings);
            return;
        }

        _logger.Warning($"Status: Unknown (HUD: {rHud},{gHud},{bHud} | MP: {rMp},{gMp},{bMp}). Trying default open...");
        OpenMarketplace(gameHandle, coords, timings);
    }

    private void OpenMarketplace(IntPtr gameHandle, ScaledCoordinates coords, TimingSettings timings)
    {
        _logger.Info("Opening tablet (Down arrow)...");
        _inputService.SendKeyToGame(gameHandle, NativeKeys.Down, 0.1);
        Thread.Sleep(TimeSpan.FromSeconds(timings.TabletOpenDelay));
        _logger.Info($"Clicking center ({coords.CenterX}, {coords.CenterY})...");
        _inputService.ClickScreenOnGame(gameHandle, coords.CenterX, coords.CenterY);
        Thread.Sleep(TimeSpan.FromSeconds(1.0));
        _logger.Info($"Clicking marketplace icon ({coords.IconX}, {coords.IconY})...");
        _inputService.ClickScreenOnGame(gameHandle, coords.IconX, coords.IconY);
        Thread.Sleep(TimeSpan.FromSeconds(timings.MarketplaceOpenDelay));
        CheckAndCloseWarning();
    }

    private IntPtr RequireGameHandle()
    {
        if (_runtime.GameHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Game window handle is not initialized.");
        }

        return _runtime.GameHandle;
    }
}
