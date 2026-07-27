using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
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

        bool found;
        try
        {
            found = _screenCapture.RegionContainsColor(
                coords.WarnBoxX1,
                coords.WarnBoxY1,
                coords.WarnBoxX2,
                coords.WarnBoxY2,
                static (r, g, b) => r > 180 && g < 100 && b < 100);
        }
        catch (Exception ex)
        {
            _logger.Warning($"CheckAndCloseWarning: failed to read screen region ({ex.Message}). Skipping.");
            return false;
        }

        if (!found)
        {
            return false;
        }

        _logger.Warning($"Warehouse notification detected. Clicking ({coords.WarnClickX}, {coords.WarnClickY})...");
        _inputService.ClickScreenOnWindow(gameHandle, coords.WarnClickX, coords.WarnClickY);
        Thread.Sleep(TimeSpan.FromSeconds(_timingsProvider().WarningClickDelay));
        return true;
    }

    public bool CheckAndCloseMap()
    {
        var coords = _runtime.Coordinates ?? throw new InvalidOperationException("Coordinates are not initialized.");
        var gameHandle = RequireGameHandle();

        byte r, g, b;
        try
        {
            (r, g, b) = _screenCapture.GetPixelColor(coords.MapPixelX, coords.MapPixelY);
        }
        catch (Exception ex)
        {
            _logger.Warning($"CheckAndCloseMap: failed to read pixel ({ex.Message}). Skipping.");
            return false;
        }

        if (r > 200 && g < 40 && b is >= 80 and <= 140)
        {
            _logger.Warning("Map menu detected. Closing with ESC...");
            _inputService.SendKeyToGame(gameHandle, NativeKeys.Escape, 0.1);
            Thread.Sleep(TimeSpan.FromSeconds(_timingsProvider().MapCloseDelay));
            return true;
        }

        return false;
    }

    public bool IsAtPreStartMenu()
    {
        var coords = _runtime.Coordinates;
        if (coords is null)
        {
            return false;
        }

        try
        {
            var (r, g, b) = _screenCapture.GetPixelColor(coords.PreStartPixelX, coords.PreStartPixelY);
            return Math.Abs(r - GameConstants.PreStartR) <= GameConstants.PreStartTolerance
                && Math.Abs(g - GameConstants.PreStartG) <= GameConstants.PreStartTolerance
                && Math.Abs(b - GameConstants.PreStartB) <= GameConstants.PreStartTolerance;
        }
        catch (Exception ex)
        {
            _logger.Warning($"IsAtPreStartMenu: failed to read pixel ({ex.Message}).");
            return false;
        }
    }

    public bool IsAtCharacterSelect()
    {
        var coords = _runtime.Coordinates;
        if (coords is null)
        {
            return false;
        }

        try
        {
            var (r, g, b) = _screenCapture.GetPixelColor(coords.CharSelectPixelX, coords.CharSelectPixelY);
            return Math.Abs(r - GameConstants.CharSelectR) <= GameConstants.CharSelectTolerance
                && Math.Abs(g - GameConstants.CharSelectG) <= GameConstants.CharSelectTolerance
                && Math.Abs(b - GameConstants.CharSelectB) <= GameConstants.CharSelectTolerance;
        }
        catch (Exception ex)
        {
            _logger.Warning($"IsAtCharacterSelect: failed to read pixel ({ex.Message}).");
            return false;
        }
    }

    public bool IsInGame()
    {
        var coords = _runtime.Coordinates;
        if (coords is null)
        {
            return false;
        }

        try
        {
            var (r, g, b) = _screenCapture.GetPixelColor(coords.HudPixelX, coords.HudPixelY);

            // Same test as the HUD branch of SmartStateRecovery. Character select shares this
            // accent colour, so callers must rule that out first - IsAtCharacterSelect does it.
            return r >= 200 && g <= 60 && b is >= 80 and <= 170;
        }
        catch (Exception ex)
        {
            _logger.Warning($"IsInGame: failed to read pixel ({ex.Message}).");
            return false;
        }
    }

    public void SmartStateRecovery()
    {
        var coords = _runtime.Coordinates ?? throw new InvalidOperationException("Coordinates are not initialized.");
        var timings = _timingsProvider();
        var gameHandle = RequireGameHandle();

        _logger.Info("Analyzing UI state...");

        (byte r, byte g, byte b) hud;
        (byte r, byte g, byte b) mp;
        try
        {
            hud = _screenCapture.GetPixelColor(coords.HudPixelX, coords.HudPixelY);
            mp = _screenCapture.GetPixelColor(coords.MpPixelX, coords.MpPixelY);
        }
        catch (Exception ex)
        {
            // Coordinates can be transiently invalid right after the game window
            // moves/resizes/minimizes. Skip this recovery pass instead of crashing the engine.
            _logger.Warning($"SmartStateRecovery: failed to read screen state ({ex.Message}). Skipping this pass.");
            return;
        }

        var (rHud, gHud, bHud) = hud;
        var (rMp, gMp, bMp) = mp;

        // Must be checked before the HUD branch: the character-select screen uses the same pink
        // accent colour as the in-game HUD pixel, so the HUD check alone reports a false "In game".
        if (IsAtCharacterSelect())
        {
            _logger.Info("Status: Character-select screen. Not in game yet - skipping tablet/marketplace.");
            return;
        }

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
        _inputService.ClickScreenOnWindow(gameHandle, coords.CenterX, coords.CenterY);
        Thread.Sleep(TimeSpan.FromSeconds(1.0));
        _logger.Info($"Clicking marketplace icon ({coords.IconX}, {coords.IconY})...");
        _inputService.ClickScreenOnWindow(gameHandle, coords.IconX, coords.IconY);
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
