using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Engine;
using AntiAfk.Core.Models;
using AntiAfk.Core.Screens;

namespace AntiAfk.App.Services;

/// <summary>
/// The actions themselves. Every click coordinate the login flow uses lives in <see cref="Targets"/>
/// and nowhere else.
///
/// Each method does one thing and then pauses long enough for the game to have reacted. The pause
/// belongs here rather than at the call sites: it is a property of the action - how long this
/// particular UI takes to respond - not of whatever happens to be calling it, and putting it here
/// is what stops two callers from disagreeing about it.
/// </summary>
public sealed class GameActions : IGameActions
{
    /// <summary>
    /// Click targets, in the 1920x1080 space everything here was measured at. Same caveat as
    /// <see cref="ScreenCatalogue"/>: not scaled, because the flow starts before the game window
    /// exists.
    /// </summary>
    private static class Targets
    {
        /// Launcher login button. The launcher window spans roughly (410,170)-(1570,907).
        public static readonly (int X, int Y) LauncherLogin = (950, 487);

        /// Character tile, then the confirm button that appears after selecting it.
        public static readonly (int X, int Y) Character1 = (594, 933);
        public static readonly (int X, int Y) Character1Confirm = (593, 993);
        public static readonly (int X, int Y) Character2 = (982, 929);
        public static readonly (int X, int Y) Character2Confirm = (959, 993);
        public static readonly (int X, int Y) Character3 = (1333, 927);
        public static readonly (int X, int Y) Character3Confirm = (1323, 993);

        /// Default spawn point. Extend when further spawn slots are mapped.
        public static readonly (int X, int Y) DefaultSpawn = (1053, 964);

        public static readonly (int X, int Y) ScreenCentre = (GameConstants.BaseCenterX, GameConstants.BaseCenterY);
        public static readonly (int X, int Y) MarketplaceIcon = (GameConstants.BaseIconX, GameConstants.BaseIconY);
        public static readonly (int X, int Y) WarningDismiss = GameConstants.BaseWarnClick;
    }

    // How long each action leaves the game to react. Measured against the UI, not guessed: the
    // confirm step transitions to a loading screen, which is the slowest of them.
    private static readonly TimeSpan AfterCharacterTile = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan AfterCharacterConfirm = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AfterSpawn = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan AfterLauncherLogin = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan AfterKeyPress = TimeSpan.FromSeconds(1);

    private readonly IInputService _inputService;
    private readonly IWindowService _windowService;
    private readonly IScreenRecognizer _recognizer;
    private readonly IAppLogger _logger;
    private readonly Func<TimingSettings> _timings;

    public GameActions(
        IInputService inputService,
        IWindowService windowService,
        IScreenRecognizer recognizer,
        IAppLogger logger,
        Func<TimingSettings> timings)
    {
        _inputService = inputService;
        _windowService = windowService;
        _recognizer = recognizer;
        _logger = logger;
        _timings = timings;
    }

    public async Task ClickLauncherLoginAsync(CancellationToken cancellationToken)
    {
        _logger.Info($"Action: click launcher login at ({Targets.LauncherLogin.X},{Targets.LauncherLogin.Y})");
        ClickOn(_windowService.FindMainWindowByProcess(GameConstants.LauncherProcessName), "launcher", Targets.LauncherLogin);
        await Task.Delay(AfterLauncherLogin, cancellationToken);
    }

    public async Task ChooseCharacterAsync(int slot, CancellationToken cancellationToken)
    {
        var (tile, confirm) = CharacterTargets(slot);

        _logger.Info($"Action: choose character {slot} - tile ({tile.X},{tile.Y})");
        ClickOnGame(tile);
        await Task.Delay(AfterCharacterTile, cancellationToken);

        _logger.Info($"Action: confirm character {slot} - ({confirm.X},{confirm.Y})");
        ClickOnGame(confirm);
        await Task.Delay(AfterCharacterConfirm, cancellationToken);
    }

    public async Task ChooseSpawnAsync(int slot, CancellationToken cancellationToken)
    {
        var target = SpawnTarget(slot);
        _logger.Info($"Action: choose spawn {slot} at ({target.X},{target.Y})");
        ClickOnGame(target);
        await Task.Delay(AfterSpawn, cancellationToken);
    }

    public async Task OpenTabletAsync(CancellationToken cancellationToken)
    {
        _logger.Info("Action: open tablet (Down)");
        _inputService.SendKeyToGame(GameHandle(), NativeKeys.Down, 0.1);
        await Task.Delay(TimeSpan.FromSeconds(_timings().TabletOpenDelay), cancellationToken);
    }

    public async Task ClickScreenCentreAsync(CancellationToken cancellationToken)
    {
        _logger.Info($"Action: click centre ({Targets.ScreenCentre.X},{Targets.ScreenCentre.Y})");
        ClickOnGame(Targets.ScreenCentre);
        await Task.Delay(AfterKeyPress, cancellationToken);
    }

    public async Task OpenMarketplaceAsync(CancellationToken cancellationToken)
    {
        _logger.Info($"Action: click marketplace icon ({Targets.MarketplaceIcon.X},{Targets.MarketplaceIcon.Y})");
        ClickOnGame(Targets.MarketplaceIcon);
        await Task.Delay(TimeSpan.FromSeconds(_timings().MarketplaceOpenDelay), cancellationToken);
    }

    public async Task DismissWarningAsync(CancellationToken cancellationToken)
    {
        _logger.Info($"Action: dismiss warning ({Targets.WarningDismiss.X},{Targets.WarningDismiss.Y})");
        ClickOnGame(Targets.WarningDismiss);
        await Task.Delay(TimeSpan.FromSeconds(_timings().WarningClickDelay), cancellationToken);
    }

    public async Task PressEscapeAsync(CancellationToken cancellationToken)
    {
        _logger.Info("Action: press Escape");
        _inputService.SendKeyToGame(GameHandle(), NativeKeys.Escape, 0.1);
        await Task.Delay(TimeSpan.FromSeconds(_timings().EscDelay), cancellationToken);
    }

    public bool IsThirdCharacterAvailable() => _recognizer.Matches(ScreenCatalogue.ThirdCharacterAvailable);

    private IntPtr GameHandle() => _windowService.FindGameWindow()?.Handle ?? IntPtr.Zero;

    private void ClickOnGame((int X, int Y) target) => ClickOn(GameHandle(), "game", target);

    // A click has to go to a window that is actually receiving input. ClickScreenOnGame raises the
    // target first; falling back to a bare click is better than abandoning the action, but it is
    // worth a warning because that press goes wherever the cursor happens to be.
    private void ClickOn(IntPtr handle, string what, (int X, int Y) target)
    {
        if (handle == IntPtr.Zero)
        {
            _logger.Warning($"{what} window not found; clicking without raising it. The press may go elsewhere.");
            _inputService.ClickScreen(target.X, target.Y);
            return;
        }

        _inputService.ClickScreenOnGame(handle, target.X, target.Y);
    }

    private static ((int X, int Y) Tile, (int X, int Y) Confirm) CharacterTargets(int slot) => slot switch
    {
        2 => (Targets.Character2, Targets.Character2Confirm),
        3 => (Targets.Character3, Targets.Character3Confirm),
        _ => (Targets.Character1, Targets.Character1Confirm)
    };

    private static (int X, int Y) SpawnTarget(int slot) => slot switch
    {
        _ => Targets.DefaultSpawn
    };
}
