using System.Diagnostics;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;

namespace AntiAfk.App.Services;

public sealed class AutoLoginService : IAutoLoginService
{
    // Server-connected / character-select indicator pixel (approx. ff007e)
    private const int ServerPixelX = 634;
    private const int ServerPixelY = 216;
    private const uint ServerPixelColor = 0xff007e;

    // Character-select screen indicator (approx. e81c5a). If this is already visible when
    // AutoLoginAsync starts, we're past the launcher and GTA5 is already running - the user
    // may have started the script from this point rather than from the launcher.
    // Kept in sync with GameConstants.BaseCharSelectPixel, which StateDetector uses (scaled).
    private static readonly int CharacterSelectPixelX = GameConstants.BaseCharSelectPixel.X;
    private static readonly int CharacterSelectPixelY = GameConstants.BaseCharSelectPixel.Y;
    private const uint CharacterSelectPixelColor =
        ((uint)GameConstants.CharSelectR << 16) | ((uint)GameConstants.CharSelectG << 8) | GameConstants.CharSelectB;

    // Launcher login button
    private const int LoginButtonX = 950;
    private const int LoginButtonY = 487;

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

            // Note: the launcher is already started by the engine (GameLauncherService)
            // before this sequence runs, so we do NOT launch it again here.

            // Step 0: Check if we're already on the character-select screen. This covers the
            // case where the script is started mid-flow (e.g. the user already logged in via
            // the launcher manually).
            var alreadyAtCharacterSelect = IsPixelColor(
                CharacterSelectPixelX, CharacterSelectPixelY, CharacterSelectPixelColor, GameConstants.CharSelectTolerance);

            if (alreadyAtCharacterSelect)
            {
                // Steps 1-3 are all about getting *to* this screen, so skip them entirely.
                // Waiting for the server-connection indicator here would poll for a pixel that
                // belongs to the screen shown before character select, which never comes back.
                _logger.Info("Character-select screen already detected - skipping launcher login, GTA5 wait and server-connection wait");

                // Let the UI settle before clicking, same as the normal path does.
                await Task.Delay(3000, cancellationToken);
            }
            else
            {
                // Step 1: Wait for launcher UI, then click the login button
                await ClickMajesticLoginAsync(cancellationToken);

                // Step 2: Wait for GTA5.exe process to start
                await WaitForGTA5Async(cancellationToken);

                // Step 3: Wait until the server-connected / character-select screen appears
                var reached = await WaitForServerConnectionAsync(cancellationToken);
                if (!reached)
                {
                    _logger.Warning("Auto-login: character-select screen not detected; skipping automated selection");
                    return;
                }
            }

            // Step 4: Character selection (1 of 3)
            await SelectCharacterAsync(characterSlot, cancellationToken);

            // Step 5: Spawn selection
            await SelectSpawnAsync(spawnSlot, cancellationToken);

            // Step 6: Wait for the in-game HUD (fully loaded)
            await WaitForGameLoadAsync(cancellationToken);

            _logger.Info("Auto-login sequence completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Auto-login cancelled");
            throw;
        }
        catch (Exception ex)
        {
            // Do not rethrow: the engine awaits this and should continue to bind
            // the game window and run its own state recovery even if login partially fails.
            _logger.Error("Auto-login failed", ex);
        }
    }

    private async Task ClickMajesticLoginAsync(CancellationToken cancellationToken)
    {
        // Launcher was already started by the engine. Give the UI time to render,
        // then click the login button. (Launcher window: 410,170 -> 1570,907)
        const int waitTimeSeconds = 5;
        _logger.Info($"Auto-login: waiting {waitTimeSeconds}s for launcher UI to render...");

        for (int i = 0; i < waitTimeSeconds; i++)
        {
            await Task.Delay(1000, cancellationToken);
        }

        _logger.Info($"Auto-login: clicking launcher login button at ({LoginButtonX}, {LoginButtonY})");
        _inputService.ClickScreen(LoginButtonX, LoginButtonY);
        await Task.Delay(4000, cancellationToken); // Wait for game process to launch (4s)
    }

    private async Task WaitForGTA5Async(CancellationToken cancellationToken)
    {
        // Wait for GTA5.exe process (up to 5 minutes)
        const int maxAttempts = 300;
        var attempts = 0;

        _logger.Info("Auto-login: waiting for GTA5.exe to start...");

        while (Process.GetProcessesByName("GTA5").Length == 0 && attempts < maxAttempts)
        {
            await Task.Delay(1000, cancellationToken);
            attempts++;
            LogWaitProgress("GTA5.exe", attempts, maxAttempts);
        }

        if (attempts >= maxAttempts)
        {
            _logger.Warning("GTA5.exe did not start within timeout (5 minutes)");
        }
        else
        {
            _logger.Info("GTA5.exe started");
        }
    }

    private async Task<bool> WaitForServerConnectionAsync(CancellationToken cancellationToken)
    {
        // Wait for the server-connected / character-select indicator
        // (pixel 634,216 ~ ff007e). This screen means we can start selecting a character.
        var attempts = 0;
        const int maxAttempts = 300; // up to 5 minutes

        _logger.Info($"Auto-login: waiting for server connection (pixel {ServerPixelX},{ServerPixelY})...");

        while (attempts < maxAttempts)
        {
            if (IsPixelColor(ServerPixelX, ServerPixelY, ServerPixelColor, tolerance: 40))
            {
                _logger.Info("Server connection / character-select screen detected");
                // Let the UI stabilise before we start clicking (3s for slow connections)
                await Task.Delay(3000, cancellationToken);
                return true;
            }

            await Task.Delay(1000, cancellationToken);
            attempts++;
            LogWaitProgress("server connection", attempts, maxAttempts);
        }

        _logger.Warning("Server connection indicator not detected within timeout (5 minutes)");
        return false;
    }

    // Long polling loops are otherwise completely silent, which is indistinguishable from a hang.
    private void LogWaitProgress(string what, int attempts, int maxAttempts)
    {
        const int logEverySeconds = 15;
        if (attempts % logEverySeconds == 0 && attempts < maxAttempts)
        {
            _logger.Info($"Auto-login: still waiting for {what}... ({attempts}s / {maxAttempts}s)");
        }
    }

    private async Task SelectCharacterAsync(int characterSlot, CancellationToken cancellationToken)
    {
        // Character 3 is only available if purchased; fall back to slot 1 if not.
        var char3Available = IsPixelColor(1226, 1000, 0xe81c5a, tolerance: 30);

        int character = characterSlot;
        if (characterSlot == 3 && !char3Available)
        {
            _logger.Info("Character 3 not available, selecting character 1");
            character = 1;
        }

        var (selectX, selectY, confirmX, confirmY) = GetCharacterCoordinates(character);

        _logger.Info($"Selecting character {character}: click ({selectX},{selectY})");
        _inputService.ClickScreen(selectX, selectY);
        await Task.Delay(4000, cancellationToken); // Wait for UI to respond to selection (4s for stability)

        _logger.Info($"Confirming character {character}: click ({confirmX},{confirmY})");
        _inputService.ClickScreen(confirmX, confirmY);
        await Task.Delay(5000, cancellationToken); // Wait for character load and transition (5s for slow internet/low FPS)
    }

    private async Task SelectSpawnAsync(int spawnSlot, CancellationToken cancellationToken)
    {
        var (spawnX, spawnY) = GetSpawnCoordinates(spawnSlot);
        _logger.Info($"Selecting spawn slot {spawnSlot} at ({spawnX}, {spawnY})");
        _inputService.ClickScreen(spawnX, spawnY);
        await Task.Delay(4000, cancellationToken); // Wait for spawn confirmation to process (4s for stability)
    }

    private async Task WaitForGameLoadAsync(CancellationToken cancellationToken)
    {
        // Wait for the in-game HUD to appear (pixel 1888,25 ~ ff007f)
        var loaded = false;
        var attempts = 0;
        const int maxAttempts = 300; // up to 5 minutes

        _logger.Info("Auto-login: waiting for in-game HUD...");

        while (!loaded && attempts < maxAttempts)
        {
            if (IsPixelColor(1888, 25, 0xff007f, tolerance: 40))
            {
                loaded = true;
                break;
            }

            await Task.Delay(1000, cancellationToken);
            LogWaitProgress("in-game HUD", attempts + 1, maxAttempts);
            attempts++;
        }

        if (!loaded)
        {
            _logger.Warning("Game load (HUD) indicator not detected within timeout");
        }
        else
        {
            _logger.Info("Game fully loaded (HUD detected)");
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

    private (int spawnX, int spawnY) GetSpawnCoordinates(int spawnSlot)
    {
        // Default spawn point; extend here when additional spawn slots are mapped.
        return spawnSlot switch
        {
            _ => (1053, 964)
        };
    }

    private bool IsPixelColor(int x, int y, uint expectedColor, int tolerance = 30)
    {
        try
        {
            var (r, g, b) = _screenCapture.GetPixelColor(x, y);

            var expected = expectedColor & 0xFFFFFF;
            var r2 = (int)((expected >> 16) & 0xFF);
            var g2 = (int)((expected >> 8) & 0xFF);
            var b2 = (int)(expected & 0xFF);

            var rDiff = Math.Abs(r - r2);
            var gDiff = Math.Abs(g - g2);
            var bDiff = Math.Abs(b - b2);

            return rDiff <= tolerance && gDiff <= tolerance && bDiff <= tolerance;
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
