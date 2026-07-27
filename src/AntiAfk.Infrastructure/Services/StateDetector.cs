using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Engine;
using AntiAfk.Core.Models;
using AntiAfk.Core.Screens;

namespace AntiAfk.Infrastructure.Services;

public sealed class StateDetector : IStateDetector
{
    private readonly IScreenCaptureService _screenCapture;
    private readonly IScreenRecognizer _recognizer;
    private readonly IInputService _inputService;
    private readonly IAppLogger _logger;
    private readonly EngineRuntime _runtime;
    private readonly Func<TimingSettings> _timingsProvider;

    public StateDetector(
        IScreenCaptureService screenCapture,
        IScreenRecognizer recognizer,
        IInputService inputService,
        IAppLogger logger,
        EngineRuntime runtime,
        Func<TimingSettings> timingsProvider)
    {
        _screenCapture = screenCapture;
        _recognizer = recognizer;
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
        _inputService.ClickScreenOnGame(gameHandle, coords.WarnClickX, coords.WarnClickY);
        Thread.Sleep(TimeSpan.FromSeconds(_timingsProvider().WarningClickDelay));
        return true;
    }

    public bool CheckAndCloseMap()
    {
        var gameHandle = RequireGameHandle();

        if (_recognizer.Recognize() != GameScreen.MapOpen)
        {
            return false;
        }

        _logger.Warning("Map menu detected. Closing with ESC...");
        _inputService.SendKeyToGame(gameHandle, NativeKeys.Escape, 0.1);
        Thread.Sleep(TimeSpan.FromSeconds(_timingsProvider().MapCloseDelay));
        return true;
    }

    public bool IsAtCharacterSelect()
    {
        return _recognizer.Recognize() == GameScreen.CharacterSelect;
    }

    public void SmartStateRecovery()
    {
        var coords = _runtime.Coordinates ?? throw new InvalidOperationException("Coordinates are not initialized.");
        var timings = _timingsProvider();
        var gameHandle = RequireGameHandle();

        // One recognise call, one decision. This used to read two pixels itself and compare them
        // against channel ranges written inline, duplicating what ScreenCatalogue already says -
        // including the ordering rule that character select must be tested before the HUD, because
        // the two share the same accent colour. Two copies of that rule is one too many, and the
        // catalogue now owns it.
        var screen = _recognizer.Recognize();

        switch (screen)
        {
            // Neither of these is in the world, so there is no tablet to open and no marketplace to
            // recover. Falling through to the default open on ConnectingToServer is what pushed the
            // game into the tablet while it was still connecting, instead of letting the login flow
            // reach character select.
            case GameScreen.CharacterSelect:
                _logger.Info("Status: character-select screen. Not in game yet - skipping tablet/marketplace.");
                return;

            case GameScreen.ConnectingToServer:
                _logger.Info("Status: connecting to the server. Not in game yet - skipping tablet/marketplace.");
                return;

            case GameScreen.MarketplaceWarning:
                _logger.Info("Status: marketplace open with an overlay. Closing it...");
                CheckAndCloseWarning();
                return;

            case GameScreen.Marketplace:
                _logger.Info("Status: marketplace active. Nothing to recover.");
                return;

            case GameScreen.MapOpen:
                _logger.Info("Status: map open. Closing it...");
                CheckAndCloseMap();
                return;

            case GameScreen.InGame:
                _logger.Info("Status: in game. Opening tablet and marketplace...");
                OpenMarketplace(gameHandle, coords, timings);
                return;

            default:
                _logger.Warning($"Status: {screen}. Trying the default open...");
                OpenMarketplace(gameHandle, coords, timings);
                return;
        }
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
