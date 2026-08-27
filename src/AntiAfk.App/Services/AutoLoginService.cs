using System.Diagnostics;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Vision;

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

        /// Project buttons in the launcher, clicked before login when the config names a project.
        public static readonly (int X, int Y) ProjectMajestic = GameConstants.ProjectMajestic;
        public static readonly (int X, int Y) ProjectRussiaOnline = GameConstants.ProjectRussiaOnline;

        /// Launcher login button. The launcher window spans roughly (410,170)-(1570,907).
        public static readonly (int X, int Y) LoginButton = (950, 487);

        // The server-connected indicator at (634,216) used to be waited on here, and it was the
        // cause of the launcher path misclicking: it belongs to the screen shown *before* character
        // select, so it went green while the character tiles were still loading. Nothing waits on
        // it now - the engine waits for the character-select screen itself, the same way it always
        // did when the game was already running. Left out rather than left unused.

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

        /// Last-resort spawn click, used only when the spawn bar could not be read at all.
        ///
        /// This is where the bot used to click every time, and it is worth knowing what that
        /// actually was: on a bar of five icons it lands on the fourth one. Not the fourth spawn
        /// point in any meaningful sense - just whatever the fourth icon happens to be for that
        /// player, on that day. Which spawn point that is changes the moment they buy a house,
        /// because the bar is centred and everything shifts. SpawnBarDetector exists to stop
        /// guessing; this stays behind it as something better than doing nothing.
        public static readonly (int X, int Y) DefaultSpawn = (1053, 964);

        /// In-game HUD pixel, present once the world has finished loading, approx. #ff007f.
        public static readonly (int X, int Y) HudPixel = GameConstants.BaseHudPixel;
        public const uint HudColor = 0xff007f;
        public const int HudTolerance = 40;
    }

    /// How long the character-select screen is left alone before the first click.
    private static readonly TimeSpan CharacterSelectSettle = TimeSpan.FromSeconds(3);

    /// How long to keep looking for the spawn bar after the character has been confirmed. The map
    /// screen fades in, and on a slow machine it is not there the moment the confirm click lands.
    private static readonly TimeSpan SpawnBarTimeout = TimeSpan.FromSeconds(20);

    /// Pause after the spawn click, so the game has acted on it before anything else happens.
    private static readonly TimeSpan SpawnClickSettle = TimeSpan.FromSeconds(4);

    /// How long to keep looking for the launcher window after the launcher process was started.
    /// A cold start on a slow disk can take a while to put anything on screen.
    private static readonly TimeSpan LauncherWindowTimeout = TimeSpan.FromSeconds(60);

    private readonly IAppLogger _logger;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IInputService _inputService;
    private readonly IWindowService _windowService;
    private readonly IConfigService _configService;

    private ProjectProfile ActiveProfile =>
        ProjectProfile.ForProject(_configService.Current.Project);

    public AutoLoginService(
        IAppLogger logger,
        IScreenCaptureService screenCapture,
        IInputService inputService,
        IWindowService windowService,
        IConfigService configService)
    {
        _logger = logger;
        _screenCapture = screenCapture;
        _inputService = inputService;
        _windowService = windowService;
        _configService = configService;
    }

    /// <summary>
    /// The launcher-only step: wait for its UI, then click login. Nothing else - the engine takes
    /// over from here and drives the same sequence it uses when the game was already running.
    /// </summary>
    public async Task StartLauncherLoginAsync(CancellationToken cancellationToken)
    {
        LogScreenGeometry();

        var launcher = await WaitForLauncherWindowAsync(cancellationToken);

        const int waitTimeSeconds = 5;
        _logger.Info($"Auto-login: waiting {waitTimeSeconds}s for launcher UI to render...");
        await Task.Delay(TimeSpan.FromSeconds(waitTimeSeconds), cancellationToken);

        // Re-read it after the wait: the launcher replaces its startup window with the real one
        // while it renders, so the handle found a moment ago can already be gone.
        launcher = _windowService.FindLauncherWindow() ?? launcher;
        var launcherHandle = launcher?.Handle ?? IntPtr.Zero;

        ClickProjectButton(launcherHandle);
        await Task.Delay(2000, cancellationToken);

        _logger.Info($"Auto-login: clicking launcher login button at ({Coords.LoginButton.X}, {Coords.LoginButton.Y})");
        ClickOnWindow(launcherHandle, Coords.LoginButton.X, Coords.LoginButton.Y);

        // Give the launcher a moment to start the game process before the engine begins looking
        // for its window.
        await Task.Delay(4000, cancellationToken);
    }

    /// <summary>
    /// Polls for the launcher window until it shows up or the timeout runs out. Returns null on
    /// timeout; the caller still clicks, it just cannot raise anything first.
    /// </summary>
    private async Task<LauncherWindowInfo?> WaitForLauncherWindowAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + LauncherWindowTimeout;
        var attempts = 0;

        while (DateTime.UtcNow < deadline)
        {
            var launcher = _windowService.FindLauncherWindow();
            if (launcher is not null)
            {
                _logger.Info(
                    $"Auto-login: launcher window \"{launcher.Title}\" found at " +
                    $"({launcher.Left},{launcher.Top}) {launcher.Width}x{launcher.Height}.");
                return launcher;
            }

            await Task.Delay(1000, cancellationToken);
            LogWaitProgress("the launcher window", ++attempts, (int)LauncherWindowTimeout.TotalSeconds);
        }

        _logger.Warning(
            $"Auto-login: no launcher window after {LauncherWindowTimeout.TotalSeconds:F0}s. " +
            "Clicking anyway - whatever is on top will receive it.");
        return null;
    }

    /// <summary>
    /// Character and spawn selection.
    ///
    /// There is no branching left in here. This used to choose between two sequences - one for
    /// arriving from the launcher, one for the game already running - and they drifted: they waited
    /// on different pixels and only one of them had the game window bound, focused and measured
    /// first. The caller now guarantees the same preconditions in both cases, so this is one
    /// sequence with one set of assumptions.
    /// </summary>
    public async Task<AutoLoginResult> AutoLoginAsync(CancellationToken cancellationToken, int characterSlot = 1)
    {
        try
        {
            _logger.Info("Auto-login: selecting character and spawn...");
            LogScreenGeometry();

            // Settle before the first click, exactly as before.
            await Task.Delay(CharacterSelectSettle, cancellationToken);

            // Character select is a game screen, so the game window is what has to be on top for
            // these clicks. The engine focuses it before calling in, but the settle delays below
            // give anything else on the machine a window in which to steal focus back.
            var gameHandle = _windowService.FindGameWindow()?.Handle ?? IntPtr.Zero;
            if (gameHandle == IntPtr.Zero)
            {
                _logger.Warning("Auto-login: game window not found; clicks will go to whatever is on top.");
            }

            await SelectCharacterAsync(gameHandle, characterSlot, cancellationToken);
            await SelectSpawnAsync(gameHandle, cancellationToken);

            if (!await WaitForGameLoadAsync(cancellationToken))
            {
                return AutoLoginResult.Failed;
            }

            _logger.Info("Auto-login sequence completed successfully");
            return AutoLoginResult.Succeeded;
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Auto-login cancelled");
            throw;
        }
        catch (Exception ex)
        {
            // Still not rethrown: the engine awaits this and should run its own state recovery
            // either way. The result is what tells it which of the two happened.
            _logger.Error("Auto-login failed", ex);
            return AutoLoginResult.Failed;
        }
    }

    private void ClickProjectButton(IntPtr launcherHandle)
    {
        var profile = ActiveProfile;
        var (x, y) = profile.ProjectButton;
        _logger.Info($"Auto-login: selecting project \"{profile.Id}\" at ({x}, {y})");
        ClickOnWindow(launcherHandle, x, y);
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

    private void LogWaitProgress(string what, int attempts, int maxAttempts)
    {
        const int logEverySeconds = 15;
        if (attempts % logEverySeconds == 0 && attempts < maxAttempts)
        {
            _logger.Info($"Auto-login: still waiting for {what}... ({attempts}s / {maxAttempts}s)");
        }
    }

    /// <summary>
    /// Clicks a screen position with <paramref name="windowHandle"/> raised first. Falls back to a
    /// bare click when the window could not be found, which is what this always used to do.
    /// </summary>
    private void ClickOnWindow(IntPtr windowHandle, int x, int y)
    {
        if (windowHandle == IntPtr.Zero)
        {
            _inputService.ClickScreen(x, y);
            return;
        }

        _inputService.ClickScreenOnWindow(windowHandle, x, y);
    }

    private async Task SelectCharacterAsync(IntPtr gameHandle, int characterSlot, CancellationToken cancellationToken)
    {
        var profile = ActiveProfile;
        int character = characterSlot;

        // Character 3 is only available if purchased; the probe pixel lights up when the slot is locked.
        if (character == 3)
        {
            var char3Locked = IsPixelColor(
                profile.Character3Probe.X, profile.Character3Probe.Y,
                profile.Character3ProbeColor, profile.Character3ProbeTolerance);
            if (char3Locked)
            {
                _logger.Info("Character 3 is locked, falling back to character 1.");
                character = 1;
            }
        }

        // On Russia Online, character 2 might not be created yet.
        if (character == 2 && profile.Character2Probe is { } c2Probe)
        {
            var char2NotCreated = IsPixelColor(
                c2Probe.X, c2Probe.Y,
                profile.Character2ProbeColor, profile.Character2ProbeTolerance);
            if (char2NotCreated)
            {
                _logger.Info("Character 2 is not created, falling back to character 1.");
                character = 1;
            }
        }

        var (selectX, selectY, confirmX, confirmY) = GetCharacterCoordinates(character, profile);

        _logger.Info($"Selecting character {character}: click ({selectX},{selectY})");
        ClickOnWindow(gameHandle, selectX, selectY);
        await Task.Delay(4000, cancellationToken);

        _logger.Info($"Confirming character {character}: click ({confirmX},{confirmY})");
        ClickOnWindow(gameHandle, confirmX, confirmY);
        await Task.Delay(5000, cancellationToken);
    }

    /// <summary>
    /// Reads the spawn bar and clicks the best spawn point on it.
    ///
    /// "Best" is the first entry of the configured priority list that the player actually has. The
    /// bar holds a different set of icons for every player and is centred, so there is no position
    /// that means the same thing twice - which is why this looks at what the icons are rather than
    /// where they sit.
    /// </summary>
    private async Task SelectSpawnAsync(IntPtr gameHandle, CancellationToken cancellationToken)
    {
        var profile = ActiveProfile;
        var layout = ResolveSpawnBarLayout();
        var (reading, alreadyInWorld) = await WaitForSpawnBarAsync(gameHandle, layout, cancellationToken);

        if (alreadyInWorld)
        {
            _logger.Info("Auto-login: the world is already up, so there was no spawn screen to answer.");
            return;
        }

        if (reading is null)
        {
            var fallback = profile.DefaultSpawn;
            var (fallbackX, fallbackY) = layout.ToScreen(fallback.X, fallback.Y);
            _logger.Warning(
                $"Auto-login: could not read the spawn bar. Clicking the fixed fallback point " +
                $"({fallbackX}, {fallbackY}) - whichever spawn point that is for this player.");
            ClickOnWindow(gameHandle, fallbackX, fallbackY);
            await Task.Delay(SpawnClickSettle, cancellationToken);
            return;
        }

        var selection = SpawnSelector.Select(reading, _configService.Current.Spawn.Priority);
        LogSpawnBar(reading, selection);

        var chosen = selection.Chosen.Icon;
        _logger.Info(
            selection.IsFallback
                ? $"Auto-login: none of the spawn points in the priority list are on the bar. Taking the " +
                  $"leftmost icon ({selection.Chosen.Label}) at ({chosen.ScreenX}, {chosen.ScreenY})."
                : $"Auto-login: selecting spawn point \"{selection.MatchedPriorityId}\" at " +
                  $"({chosen.ScreenX}, {chosen.ScreenY}), icon {chosen.Slot + 1} of {reading.Count}.");

        ClickOnWindow(gameHandle, chosen.ScreenX, chosen.ScreenY);
        await Task.Delay(SpawnClickSettle, cancellationToken);
    }

    /// <summary>
    /// Polls for the spawn bar until it is on screen. The map screen fades in after the character
    /// is confirmed, and how long that takes depends on the machine, so this is a wait rather than
    /// a single look.
    ///
    /// Gives up either when the wait runs out - the bar is null and the caller falls back - or as
    /// soon as the HUD is up, which means the game went straight into the world and there is no
    /// spawn screen to answer. Without that second exit the wait runs to the end and the fallback
    /// click lands in the world, on whatever happens to be under it.
    /// </summary>
    private async Task<(SpawnBarReading? Bar, bool AlreadyInWorld)> WaitForSpawnBarAsync(
        IntPtr gameHandle,
        SpawnBarLayout layout,
        CancellationToken cancellationToken)
    {
        _logger.Info(
            $"Auto-login: looking for the spawn bar - row y={layout.RowY}, centred on x={layout.CenterX}, " +
            $"{layout.Pitch}px apart.");

        var deadline = DateTime.UtcNow + SpawnBarTimeout;
        var attempts = 0;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            // The capture reads the desktop, not the game, so whatever is on top is what gets
            // measured. The game is normally already foreground here, and raising it again is
            // cheap next to reading a screen full of the wrong window.
            _windowService.ForceForeground(gameHandle);

            var strip = TryCaptureSpawnStrip(layout);
            if (strip is not null)
            {
                var reading = SpawnBarDetector.Detect(strip, layout);
                if (reading is not null)
                {
                    return (reading, false);
                }
            }

            // Checked after the bar, not before: the spawn screen does not light the HUD pixel,
            // but the order still matters if that ever changes - a bar on screen is the stronger
            // evidence of the two, because it is the whole bar rather than one pixel.
            var profile = ActiveProfile;
            var hudColor = ((uint)profile.HudR << 16) | ((uint)profile.HudG << 8) | profile.HudB;
            if (IsPixelColor(profile.HudPixel.X, profile.HudPixel.Y, hudColor, profile.HudTolerance))
            {
                return (null, true);
            }

            await Task.Delay(1000, cancellationToken);
            LogWaitProgress("the spawn bar", ++attempts, (int)SpawnBarTimeout.TotalSeconds);
        }

        return (null, false);
    }

    /// <summary>
    /// Where to look for the bar. Scaled to the game window when there is one - unlike the fixed
    /// login coordinates this is a search region, so being approximately right is enough and the
    /// click that follows goes to a detected icon rather than a guessed position.
    /// </summary>
    private SpawnBarLayout ResolveSpawnBarLayout()
    {
        var profile = ActiveProfile;
        var game = _windowService.FindGameWindow();

        if (game is null)
        {
            _logger.Warning("Auto-login: no game window to measure the spawn bar against. Using 1080p defaults.");
        }

        return SpawnBarLayout.ForWindow(
            profile.SpawnBarCenterX, profile.SpawnBarRowY, profile.SpawnBarPitch,
            profile.SpawnBarDiameter, profile.SpawnBarGlyphBox,
            profile.SpawnBarMaxIcons, profile.SpawnBarCircularBackground,
            game?.Left ?? 0, game?.Top ?? 0, game?.Width ?? 0, game?.Height ?? 0);
    }

    private PixelGrid? TryCaptureSpawnStrip(SpawnBarLayout layout)
    {
        // Clamp to the primary screen: on a windowed game near an edge the strip reaches past it,
        // and a capture that is not fully on one monitor is refused outright. A clipped strip
        // still works - the detector drops the slots it cannot see.
        var (screenWidth, screenHeight) = _windowService.GetScreenSize();
        var left = Math.Max(0, layout.StripLeft);
        var top = Math.Max(0, layout.StripTop);
        var right = Math.Min(screenWidth, layout.StripLeft + layout.StripWidth);
        var bottom = Math.Min(screenHeight, layout.StripTop + layout.StripHeight);

        if (right - left <= 0 || bottom - top <= 0)
        {
            _logger.Warning(
                $"Auto-login: the spawn bar strip ({layout.StripLeft},{layout.StripTop}) " +
                $"{layout.StripWidth}x{layout.StripHeight} is off-screen on a {screenWidth}x{screenHeight} display.");
            return null;
        }

        try
        {
            return _screenCapture.CaptureRegion(left, top, right - left, bottom - top);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Auto-login: failed to capture the spawn bar strip ({ex.Message}).");
            return null;
        }
    }

    /// Logs what was on the bar. Worth the lines: this is the only record of which spawn points a
    /// player has, and an icon reported as unknown is how a missing template gets noticed.
    private void LogSpawnBar(SpawnBarReading reading, SpawnSelection selection)
    {
        var icons = selection.Icons.Select(icon =>
        {
            var label = icon.Match is null
                ? $"{icon.Label} (closest {DescribeClosest(icon)})"
                : $"{icon.Label} ({icon.Match.Glyph} {icon.Match.Distance:F2})";
            return $"[{icon.Icon.Slot + 1}] {label} @{icon.Icon.ScreenX}";
        });

        _logger.Info(
            $"Auto-login: spawn bar has {reading.Count} icon(s) on row y={reading.RowY} " +
            $"(fit {reading.Confidence:F2}): {string.Join("  ", icons)}");

        foreach (var icon in selection.Icons.Where(candidate => candidate.Id is null))
        {
            // The ascii art is what makes an unrecognised icon actionable: it is the glyph as the
            // matcher saw it, so it shows whether this is an icon with no template or a slot that
            // was misdetected in the first place.
            _logger.Warning(
                $"Auto-login: spawn icon {icon.Icon.Slot + 1} at ({icon.Icon.ScreenX}, {icon.Icon.ScreenY}) " +
                $"is not in the icon catalog. Signature {icon.Icon.Signature.ToHex()}\n" +
                icon.Icon.Signature.ToAsciiArt());
        }
    }

    private static string DescribeClosest(IdentifiedSpawnIcon icon) =>
        icon.Closest is null
            ? "the icon catalog is empty"
            : $"{icon.Closest.Glyph} at {icon.Closest.Distance:F2}";

    /// <summary>
    /// Waits for the in-game HUD. Returns false if it never turned up, which means the world did
    /// not finish loading within the timeout.
    /// </summary>
    private async Task<bool> WaitForGameLoadAsync(CancellationToken cancellationToken)
    {
        var profile = ActiveProfile;
        var hudColor = ((uint)profile.HudR << 16) | ((uint)profile.HudG << 8) | profile.HudB;
        var loaded = false;
        var attempts = 0;
        const int maxAttempts = 300;

        _logger.Info($"Auto-login: waiting for in-game HUD (pixel {profile.HudPixel.X},{profile.HudPixel.Y})...");

        while (!loaded && attempts < maxAttempts)
        {
            if (IsPixelColor(profile.HudPixel.X, profile.HudPixel.Y, hudColor, profile.HudTolerance))
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
            return false;
        }

        _logger.Info("Game fully loaded (HUD detected)");
        return true;
    }

    private static (int selectX, int selectY, int confirmX, int confirmY) GetCharacterCoordinates(int character, ProjectProfile profile)
    {
        return character switch
        {
            2 => (profile.Character2.X, profile.Character2.Y, profile.Character2Confirm.X, profile.Character2Confirm.Y),
            3 => (profile.Character3.X, profile.Character3.Y, profile.Character3Confirm.X, profile.Character3Confirm.Y),
            _ => (profile.Character1.X, profile.Character1.Y, profile.Character1Confirm.X, profile.Character1Confirm.Y)
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
