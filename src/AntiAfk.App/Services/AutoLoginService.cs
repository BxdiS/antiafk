using System.Diagnostics;
using AntiAfk.Core.Abstractions;

namespace AntiAfk.App.Services;

public sealed class AutoLoginService
{
    private readonly IAppLogger _logger;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IInputService _inputService;

    public AutoLoginService(IAppLogger logger, IScreenCaptureService screenCapture, IInputService inputService)
    {
        _logger = logger;
        _screenCapture = screenCapture;
        _inputService = inputService;
    }

    public async Task AutoLoginAsync(CancellationToken cancellationToken, int characterSlot = 1, int spawnSlot = 1)
    {
        try
        {
            _logger.Info("Starting auto-login sequence...");

            // Step 1: Launch Majestic Launcher
            await LaunchMajesticAsync(cancellationToken);
            _logger.Info("Majestic Launcher launched");

            // Step 2: Wait for launcher to load and click login
            await ClickMajesticLoginAsync(cancellationToken);
            _logger.Info("Clicked Majestic login button");

            // Step 3: Wait for GTA5.exe to start
            await WaitForGTA5Async(cancellationToken);
            _logger.Info("GTA5.exe started");

            // Step 4: Wait for server connection
            await WaitForServerConnectionAsync(cancellationToken);
            _logger.Info("Connected to server");

            // Step 5: Handle character selection
            await SelectCharacterAsync(characterSlot, cancellationToken);
            _logger.Info($"Selected character slot {characterSlot}");

            // Step 6: Select spawn location
            await SelectSpawnAsync(spawnSlot, cancellationToken);
            _logger.Info($"Selected spawn slot {spawnSlot}");

            // Step 7: Wait for full game load
            await WaitForGameLoadAsync(cancellationToken);
            _logger.Info("Game fully loaded");

            _logger.Info("Auto-login sequence completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Auto-login cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error("Auto-login failed", ex);
            throw;
        }
    }

    private async Task LaunchMajesticAsync(CancellationToken cancellationToken)
    {
        try
        {
            var launcherPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Majestic RP", "Launcher", "MajesticRPLauncher.exe");

            if (File.Exists(launcherPath))
            {
                _logger.Info($"Found Majestic Launcher at: {launcherPath}");
                Process.Start(launcherPath);
                _logger.Info("Launcher process started, waiting for UI to initialize...");
                await Task.Delay(3000, cancellationToken); // Increased from 2s to 3s for UI initialization
            }
            else
            {
                _logger.Warning($"Majestic Launcher not found at {launcherPath}, assuming it's already running");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to launch Majestic", ex);
        }
    }

    private async Task ClickMajesticLoginAsync(CancellationToken cancellationToken)
    {
        _logger.Info("ClickMajesticLoginAsync: Starting wait for launcher window...");

        // Wait for launcher to load with simple delay (launcher is visible after LaunchMajesticAsync)
        // Launcher window: left=410, top=170, right=1570, bottom=907
        const int waitTimeSeconds = 5;
        _logger.Info($"ClickMajesticLoginAsync: Waiting {waitTimeSeconds} seconds for launcher UI to fully render...");

        for (int i = 0; i < waitTimeSeconds; i++)
        {
            _logger.Info($"ClickMajesticLoginAsync: Waiting... ({i + 1}/{waitTimeSeconds})");
            await Task.Delay(1000, cancellationToken);
        }

        // Click login button at 950, 487
        const int loginButtonX = 950;
        const int loginButtonY = 487;

        _logger.Info($"ClickMajesticLoginAsync: About to click button at ({loginButtonX}, {loginButtonY})");
        try
        {
            _inputService.ClickScreen(loginButtonX, loginButtonY);
            _logger.Info($"ClickMajesticLoginAsync: Successfully clicked login button at ({loginButtonX}, {loginButtonY})");
        }
        catch (Exception ex)
        {
            _logger.Error($"ClickMajesticLoginAsync: Failed to click button: {ex.Message}", ex);
            throw;
        }

        _logger.Info("ClickMajesticLoginAsync: Waiting 2 seconds after click...");
        await Task.Delay(2000, cancellationToken);
        _logger.Info("ClickMajesticLoginAsync: Complete");
    }

    private async Task WaitForGTA5Async(CancellationToken cancellationToken)
    {
        // Wait for GTA5.exe process
        var attempts = 0;
        while (Process.GetProcessesByName("GTA5").Length == 0 && attempts < 300)
        {
            await Task.Delay(1000, cancellationToken);
            attempts++;
        }

        if (attempts >= 300)
        {
            _logger.Warning("GTA5.exe did not start within timeout (5 minutes)");
        }
        else
        {
            _logger.Info("GTA5.exe started");
        }
    }

    private async Task WaitForServerConnectionAsync(CancellationToken cancellationToken)
    {
        // Wait for initial server connection (check pixel at 630, 210 ≈ ff007e)
        var connected = false;
        var attempts = 0;

        while (!connected && attempts < 180)
        {
            if (IsPixelColor(630, 210, 0xff007e, tolerance: 30))
            {
                connected = true;
                break;
            }

            await Task.Delay(1000, cancellationToken);
            attempts++;
        }

        if (!connected)
        {
            _logger.Warning("Server connection indicator not detected within timeout");
        }
        else
        {
            _logger.Info("Server connection detected");
            // Click to proceed
            _inputService.ClickScreen(630, 210);
            await Task.Delay(1000, cancellationToken);
        }
    }

    private async Task SelectCharacterAsync(int characterSlot, CancellationToken cancellationToken)
    {
        // Check if character 3 is available (not purchased)
        var char3Available = IsPixelColor(1226, 1000, 0xe81c5a, tolerance: 30);

        int character = characterSlot;
        if (characterSlot == 3 && !char3Available)
        {
            _logger.Info("Character 3 not available, selecting character 1");
            character = 1;
        }

        var (selectX, selectY, confirmX, confirmY) = GetCharacterCoordinates(character);

        // Click select
        _inputService.ClickScreen(selectX, selectY);
        await Task.Delay(500, cancellationToken);

        // Click confirm
        _inputService.ClickScreen(confirmX, confirmY);
        await Task.Delay(2000, cancellationToken);
    }

    private async Task SelectSpawnAsync(int spawnSlot, CancellationToken cancellationToken)
    {
        // For now, always use default spawn (1053, 964)
        _inputService.ClickScreen(1053, 964);
        await Task.Delay(1000, cancellationToken);
    }

    private async Task WaitForGameLoadAsync(CancellationToken cancellationToken)
    {
        // Wait for game to fully load (check pixel at 1888, 25 = ff007f)
        var loaded = false;
        var attempts = 0;

        while (!loaded && attempts < 180)
        {
            if (IsPixelColor(1888, 25, 0xff007f, tolerance: 30))
            {
                loaded = true;
                break;
            }

            await Task.Delay(1000, cancellationToken);
            attempts++;
        }

        if (!loaded)
        {
            _logger.Warning("Game load indicator not detected within timeout");
        }
        else
        {
            _logger.Info("Game fully loaded");
        }
    }

    private (int selectX, int selectY, int confirmX, int confirmY) GetCharacterCoordinates(int character)
    {
        return character switch
        {
            1 => (594, 933, 593, 993),
            2 => (982, 929, 959, 993),
            3 => (1333, 927, 1323, 993),
            _ => (594, 933, 593, 993)
        };
    }

    private bool IsPixelColorSafe(int x, int y)
    {
        try
        {
            _ = _screenCapture.GetPixelColor(x, y);
            return true; // Successfully captured pixel, window exists
        }
        catch
        {
            return false; // Failed to capture, window may not exist yet
        }
    }

    private bool IsPixelColor(int x, int y, uint expectedColor, int tolerance = 30)
    {
        try
        {
            var (r, g, b) = _screenCapture.GetPixelColor(x, y);

            var pixelColor = ((uint)r << 16) | ((uint)g << 8) | (uint)b;
            var expected = expectedColor & 0xFFFFFF;

            var r1 = (pixelColor >> 16) & 0xFF;
            var g1 = (pixelColor >> 8) & 0xFF;
            var b1 = pixelColor & 0xFF;

            var r2 = (expected >> 16) & 0xFF;
            var g2 = (expected >> 8) & 0xFF;
            var b2 = expected & 0xFF;

            var rDiff = Math.Abs((int)r1 - (int)r2);
            var gDiff = Math.Abs((int)g1 - (int)g2);
            var bDiff = Math.Abs((int)b1 - (int)b2);

            var match = rDiff <= tolerance && gDiff <= tolerance && bDiff <= tolerance;
            return match;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.Error($"Invalid pixel coordinates ({x}, {y}): {ex.Message}");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warning($"Failed to capture pixel at ({x}, {y}), screen may be locked or scaling issue: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error checking pixel color at ({x}, {y}): {ex.Message}", ex);
            return false;
        }
    }
}
