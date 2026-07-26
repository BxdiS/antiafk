using System.Diagnostics;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;

namespace AntiAfk.App.Services;

public sealed class AutoLoginService : IAutoLoginService
{
    /// <summary>
    /// Every screen coordinate used by the login flow, in one place.
    ///
    /// These are raw screen pixels measured on a 1920x1080 primary monitor with the game running
    /// fullscreen from (0,0). Unlike the rest of the engine they deliberately do NOT go through
    /// CoordinateScaler: the launcher window exists before the game window does, so at the point
    /// the first clicks happen there is no game window to scale against.
    ///
    /// The consequence is that on any other resolution, in windowed mode, or with the game on a
    /// secondary monitor, these clicks land somewhere else entirely. AutoLoginAsync logs the actual
    /// screen size on entry so that shows up in the log rather than looking like a random misclick.
    ///
    /// Everything from character select onwards could in principle be scaled, since the game window
    /// does exist by then - see ROADMAP.
    /// </summary>
    private static class Coords
    {
        /// Resolution every value below was measured at.
        public const int MeasuredWidth = GameConstants.BaseWidth;
        public const int MeasuredHeight = GameConstants.BaseHeight;

        /// Launcher login button. The launcher window spans roughly (410,170)-(1570,907).
        public static readonly (int X, int Y) LoginButton = (950, 487);

        /// Server-connected / character-select indicator, approx. #ff007e.
        public static readonly (int X, int Y) ServerPixel = (634, 216);
        public const uint ServerColor = 0xff007e;
        public const int ServerTolerance = 40;

        /// Character-select screen indicator, approx. #e81c5a. If this is already lit when
        /// AutoLoginAsync starts we are past the launcher and GTA5 is already running - the user
        /// may have started the app from this point rather than from the launcher.
        /// Kept in sync with GameConstants.BaseCharSelectPixel, which StateDetector uses (scaled).
        public static readonly (int X, int Y) CharacterSelectPixel = GameConstants.BaseCharSelectPixel;
        public const uint CharacterSelectColor =
            ((uint)GameConstants.CharSelectR << 16) | ((uint)GameConstants.CharSelectG << 8) | GameConstants.CharSelectB;
        public const int CharacterSelectTolerance = GameConstants.CharSelectTolerance;

        /// Lit only when the third character slot has been purchased, approx. #e81c5a.
        public static readonly (int X, int Y) Character3Probe = (1226, 1000);
        public const uint Character3Color = 0xe81c5a;
        public const int Character3Tolerance = 30;

        /// Character tile, then the confirm button that appears after selecting it.
        public static readonly (int X, int Y) Character1 = (594, 933);
        public static readonly (int X, int Y) Character1Confirm = (593, 993);
        public static readonly (int X, int Y) Character2 = (982, 929);
        public static readonly (int X, int Y) Character2Confirm = (959, 993);
        public static readonly (int X, int Y) Character3 = (1333, 927);
        public static readonly (int X, int Y) Character3Confirm = (1323, 993);

        /// Default spawn point. Extend when additional spawn slots are mapped.
        public static readonly (int X, int Y) DefaultSpawn = (1053, 964);

        /// In-game HUD pixel, present once the world has finished loading, approx. #ff007f.
        public static readonly (int X, int Y) HudPixel = GameConstants.BaseHudPixel;
        public const uint HudColor = 0xff007f;
        public const int HudTolerance = 40;
    }

    private readonly IAppLogger _logger;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IInputService _inputService;
    private readonly IWindowService _windowService;

    public AutoLoginService(
        IAppLogger logger,
        IScreenCaptureService screenCapture,
        IInputService inputService,
        IWindowService windowService)
    {
        _logger = logger;
        _screenCapture = screenCapture;
        _inputService = inputService;
        _windowService = windowService;
    }

    public async Task AutoLoginAsync(CancellationToken cancellationToken, int characterSlot = 1, int spawnSlot = 1)
    {
        try
        {
            _logger.Info("Starting auto-login sequence...");
            LogScreenGeometry();

            // Note: the launcher is already started by the engine (GameLauncherService)
            // before this sequence runs, so we do NOT launch it again here.

            // Step 0: Check if we're already on the character-select screen. This covers the
            // case where the script is started mid-flow (e.g. the user already logged in via
            // the launcher manually).
            var alreadyAtCharacterSelect = IsPixelColor(
                Coords.CharacterSelectPixel.X,
                Coords.CharacterSelectPixel.Y,
                Coords.CharacterSelectColor,
                Coords.CharacterSelectTolerance);

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

    // Uses the same game-window lookup as the engine, which matches on the client's title rather
    // than trusting MainWindowHandle.
    private void ClickOnGame(int screenX, int screenY) =>
        ClickOnWindow(_windowService.FindGameWindow()?.Handle ?? IntPtr.Zero, "game", screenX, screenY);

    private void ClickOnLauncher(int screenX, int screenY) =>
        ClickOnWindow(
            _windowService.FindMainWindowByProcess(GameConstants.LauncherProcessName),
            "launcher",
            screenX,
            screenY);

    // Raises the owning window before clicking, so the press cannot be swallowed by whatever else
    // is in front. Falls back to a bare click when the window cannot be found - a click at the
    // right coordinates is still better than abandoning the login sequence.
    private void ClickOnWindow(IntPtr handle, string description, int screenX, int screenY)
    {
        if (handle == IntPtr.Zero)
        {
            _logger.Warning(
                $"Auto-login: {description} window not found, clicking without raising it. " +
                "The press may go to whatever window is in front.");
            _inputService.ClickScreen(screenX, screenY);
            return;
        }

        _inputService.ClickScreenOnWindow(handle, screenX, screenY);
    }

    // The coordinates below are fixed 1080p values, so the single most useful thing a log can say
    // when clicks "go nowhere" is what the screen actually is.
    private void LogScreenGeometry()
    {
        var (width, height) = _windowService.GetScreenSize();
        _logger.Info($"Auto-login: primary screen is {width}x{height}.");

        if (width != Coords.MeasuredWidth || height != Coords.MeasuredHeight)
        {
            _logger.Warning(
                $"Auto-login: login coordinates are hardcoded for {Coords.MeasuredWidth}x{Coords.MeasuredHeight} " +
                "fullscreen at (0,0) and are not scaled. On this screen the clicks will not line up.");
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

        _logger.Info($"Auto-login: clicking launcher login button at ({Coords.LoginButton.X}, {Coords.LoginButton.Y})");
        ClickOnLauncher(Coords.LoginButton.X, Coords.LoginButton.Y);
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
        // Wait for the server-connected / character-select indicator. Once it is lit we can
        // start selecting a character.
        var attempts = 0;
        const int maxAttempts = 300; // up to 5 minutes

        _logger.Info($"Auto-login: waiting for server connection (pixel {Coords.ServerPixel.X},{Coords.ServerPixel.Y})...");

        while (attempts < maxAttempts)
        {
            if (IsPixelColor(Coords.ServerPixel.X, Coords.ServerPixel.Y, Coords.ServerColor, Coords.ServerTolerance))
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
        var char3Available = IsPixelColor(
            Coords.Character3Probe.X, Coords.Character3Probe.Y, Coords.Character3Color, Coords.Character3Tolerance);

        int character = characterSlot;
        if (characterSlot == 3 && !char3Available)
        {
            _logger.Info("Character 3 not available, selecting character 1");
            character = 1;
        }

        var (selectX, selectY, confirmX, confirmY) = GetCharacterCoordinates(character);

        _logger.Info($"Selecting character {character}: click ({selectX},{selectY})");
        ClickOnGame(selectX, selectY);
        await Task.Delay(4000, cancellationToken); // Wait for UI to respond to selection (4s for stability)

        _logger.Info($"Confirming character {character}: click ({confirmX},{confirmY})");
        ClickOnGame(confirmX, confirmY);
        await Task.Delay(5000, cancellationToken); // Wait for character load and transition (5s for slow internet/low FPS)
    }

    private async Task SelectSpawnAsync(int spawnSlot, CancellationToken cancellationToken)
    {
        var (spawnX, spawnY) = GetSpawnCoordinates(spawnSlot);
        _logger.Info($"Selecting spawn slot {spawnSlot} at ({spawnX}, {spawnY})");
        ClickOnGame(spawnX, spawnY);
        await Task.Delay(4000, cancellationToken); // Wait for spawn confirmation to process (4s for stability)
    }

    private async Task WaitForGameLoadAsync(CancellationToken cancellationToken)
    {
        // Wait for the in-game HUD to appear
        var loaded = false;
        var attempts = 0;
        const int maxAttempts = 300; // up to 5 minutes

        _logger.Info($"Auto-login: waiting for in-game HUD (pixel {Coords.HudPixel.X},{Coords.HudPixel.Y})...");

        while (!loaded && attempts < maxAttempts)
        {
            if (IsPixelColor(Coords.HudPixel.X, Coords.HudPixel.Y, Coords.HudColor, Coords.HudTolerance))
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
            2 => (Coords.Character2.X, Coords.Character2.Y, Coords.Character2Confirm.X, Coords.Character2Confirm.Y),
            3 => (Coords.Character3.X, Coords.Character3.Y, Coords.Character3Confirm.X, Coords.Character3Confirm.Y),
            _ => (Coords.Character1.X, Coords.Character1.Y, Coords.Character1Confirm.X, Coords.Character1Confirm.Y)
        };
    }

    private (int spawnX, int spawnY) GetSpawnCoordinates(int spawnSlot)
    {
        // Default spawn point; extend here when additional spawn slots are mapped.
        return spawnSlot switch
        {
            _ => (Coords.DefaultSpawn.X, Coords.DefaultSpawn.Y)
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
