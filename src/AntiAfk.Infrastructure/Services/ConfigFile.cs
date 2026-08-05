using System.Text.Json;
using System.Text.Json.Nodes;
using AntiAfk.Core.Abstractions;
using AntiAfk.Core.Models;
using AntiAfk.Core.Vision;
using AntiAfk.Infrastructure.Localization;

namespace AntiAfk.Infrastructure.Services;

/// <summary>
/// What was in AntiAFK.json, and whether there was one at all.
///
/// The flag is not cosmetic: it is what lets a change made in the settings window say out loud that
/// the file is going to win again on the next start.
/// </summary>
public sealed record LoadedConfig(AppConfig Config, bool FromFile);

/// <summary>
/// Reads the optional AntiAFK.json sitting next to the executable.
///
/// Optional in the strong sense: no file is the normal case, the app never creates one, and every
/// section inside it can be left out. Anything absent keeps the built-in default, so a file holding
/// nothing but a spawn priority is a complete, valid config.
///
/// Nothing here is allowed to stop the app starting. The file is hand-written by whoever wants to
/// change something the settings window does not expose, which means it will regularly be malformed,
/// misspelled or full of values that make no sense - and every one of those cases has to end in a
/// log line and the built-in defaults, never in a crash on startup. That is also why the warnings
/// below name the field and say what was done about it: an advanced setting that silently does
/// nothing is worse than no setting at all.
/// </summary>
public static class ConfigFile
{
    public const string FileName = "AntiAFK.json";

    /// Longest delay any single field is allowed to ask for. Not a judgement about what is sensible
    /// - a day is already absurd for every field here - but a guard against 1e308 in a hand-edited
    /// file reaching TimeSpan.FromSeconds and throwing somewhere far away from this method.
    private const double MaxDelaySeconds = 86400;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// Next to the executable, which for a single-file publish is the exe's own folder rather than
    /// the bundle extraction directory. Applying an update moves a new exe over the old one and
    /// leaves the rest of the folder alone, so a config here survives auto-updates.
    public static string FullPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public static LoadedConfig Load(IAppLogger logger)
    {
        var path = FullPath;

        if (!File.Exists(path))
        {
            logger.Info($"No {FileName} next to the executable ({path}). Using built-in defaults.");
            return new LoadedConfig(AppConfig.CreateDefault(), false);
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not read {path} ({ex.Message}). Using built-in defaults.");
            return new LoadedConfig(AppConfig.CreateDefault(), false);
        }

        AppConfig? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AppConfig>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            // ex.Message carries the line and position, which is the whole point of surfacing it.
            logger.Warning($"{FileName} is not valid JSON: {ex.Message} Using built-in defaults.");
            return new LoadedConfig(AppConfig.CreateDefault(), false);
        }

        if (parsed is null)
        {
            logger.Warning($"{FileName} holds no object. Using built-in defaults.");
            return new LoadedConfig(AppConfig.CreateDefault(), false);
        }

        logger.Info($"Loaded settings from {path}.");
        WarnAboutUnknownKeys(json, logger);
        Validate(parsed, logger);
        return new LoadedConfig(parsed, true);
    }

    /// <summary>
    /// A deep copy, through the serializer rather than field by field.
    ///
    /// The settings window needs one so that Cancel really cancels. It used to hand-copy all
    /// twenty-odd fields, which meant every setting added after it had to be remembered here too -
    /// and a forgotten one does not fail loudly, it silently resets that setting to its default the
    /// moment the user presses Save.
    /// </summary>
    public static AppConfig Clone(AppConfig source) =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(source, WriteOptions), ReadOptions)
        ?? AppConfig.CreateDefault();

    /// <summary>
    /// Reports keys the file has and this build does not.
    ///
    /// System.Text.Json ignores unknown properties, so without this a misspelled "escDelaay" reads
    /// as a config that simply does nothing, with no way to tell that from a setting that is being
    /// applied. Comparing the file against a serialised default gives the misspelling by name and
    /// path. Failing here costs a warning, not the config - it has already been parsed by now.
    /// </summary>
    private static void WarnAboutUnknownKeys(string json, IAppLogger logger)
    {
        JsonObject? actual;
        JsonObject? expected;

        try
        {
            actual = JsonNode.Parse(json, documentOptions: DocumentOptions) as JsonObject;
            expected = JsonSerializer.SerializeToNode(AppConfig.CreateDefault(), WriteOptions) as JsonObject;
        }
        catch (JsonException)
        {
            return;
        }

        foreach (var path in UnknownPaths(actual, expected, string.Empty))
        {
            logger.Warning($"Config: {FileName} sets \"{path}\", which this build has no setting for. Ignored.");
        }
    }

    private static IEnumerable<string> UnknownPaths(JsonObject? actual, JsonObject? expected, string prefix)
    {
        if (actual is null || expected is null)
        {
            yield break;
        }

        foreach (var (key, value) in actual)
        {
            var match = expected.FirstOrDefault(
                candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));

            if (match.Key is null)
            {
                yield return prefix + key;
                continue;
            }

            foreach (var nested in UnknownPaths(value as JsonObject, match.Value as JsonObject, $"{prefix}{key}."))
            {
                yield return nested;
            }
        }
    }

    private static void Validate(AppConfig config, IAppLogger logger)
    {
        ReplaceNulls(config, logger);
        ValidateLanguage(config, logger);
        ValidateSpawnPriority(config.Spawn, logger);
        ValidateTimings(config.Timings, logger);
        ValidateUpdate(config.Update, logger);
    }

    /// <summary>
    /// An explicit null in the file is not the same as a missing key.
    ///
    /// Leaving "timings" out keeps the built-in timings, because the deserializer never touches the
    /// property and the initialiser on AppConfig has already run. Writing "timings": null assigns
    /// null over it, which the nullable annotations say cannot happen and JSON does not care about,
    /// and the app then dies on the first read - out here in Main, before there is a window to show
    /// the error in. So every section a hand-written file can null out is put back here.
    /// </summary>
    private static void ReplaceNulls(AppConfig config, IAppLogger logger)
    {
        config.Language = OrDefault(config.Language, "ru", "language", logger);
        config.Timings = OrDefault(config.Timings, TimingSettings.CreateDefault(), "timings", logger);
        config.Spawn = OrDefault(config.Spawn, new SpawnSettings(), "spawn", logger);
        config.Update = OrDefault(config.Update, new UpdateSettings(), "update", logger);
        config.Spawn.Priority = OrDefault(config.Spawn.Priority, new SpawnSettings().Priority, "spawn.priority", logger);

        // Silent: an empty launcher path is a documented value meaning "find it yourself", so null
        // and "" ask for the same thing and there is nothing to report.
        config.LauncherPath ??= string.Empty;
    }

    private static T OrDefault<T>(T? value, T fallback, string field, IAppLogger logger) where T : class
    {
        if (value is not null)
        {
            return value;
        }

        logger.Warning($"Config: {field} is null. Using the built-in value.");
        return fallback;
    }

    private static void ValidateLanguage(AppConfig config, IAppLogger logger)
    {
        if (LocalizationService.Supported.Contains(config.Language, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        logger.Warning(
            $"Config: language \"{config.Language}\" is not one this build ships " +
            $"({string.Join(", ", LocalizationService.Supported)}). Falling back to ru.");
    }

    private static void ValidateSpawnPriority(SpawnSettings spawn, IAppLogger logger)
    {
        if (spawn.Priority.Count == 0)
        {
            logger.Info("Config: spawn.priority is empty, so the leftmost icon on the bar is always taken.");
            return;
        }

        // Left in the list rather than removed: an id nothing on the bar can match is already a
        // no-op in SpawnSelector, and dropping it would only hide the typo from a later reading of
        // the config. The warning is what makes it actionable.
        var unknown = spawn.Priority.Where(id => !SpawnIconCatalog.IsKnown(id)).ToArray();
        if (unknown.Length == 0)
        {
            return;
        }

        logger.Warning(
            $"Config: spawn.priority names {unknown.Length} spawn point(s) this build cannot recognise " +
            $"({string.Join(", ", unknown)}). They will never match. Known ids: " +
            $"{string.Join(", ", SpawnIconCatalog.KnownIds)}.");

        if (unknown.Length == spawn.Priority.Count)
        {
            logger.Warning("Config: nothing in spawn.priority is recognisable, so the leftmost icon on the bar will be taken.");
        }
    }

    private static void ValidateTimings(TimingSettings timings, IAppLogger logger)
    {
        // Same trap as the sections above, one level down: "cycleSleepDelay": null nulls the range.
        var defaults = TimingSettings.CreateDefault();
        timings.BackgroundClickDelay = OrDefault(timings.BackgroundClickDelay, defaults.BackgroundClickDelay, "timings.backgroundClickDelay", logger);
        timings.CycleSleepDelay = OrDefault(timings.CycleSleepDelay, defaults.CycleSleepDelay, "timings.cycleSleepDelay", logger);
        timings.WalkDuration = OrDefault(timings.WalkDuration, defaults.WalkDuration, "timings.walkDuration", logger);
        timings.TurnKeyDuration = OrDefault(timings.TurnKeyDuration, defaults.TurnKeyDuration, "timings.turnKeyDuration", logger);
        timings.TurnGapJitter = OrDefault(timings.TurnGapJitter, defaults.TurnGapJitter, "timings.turnGapJitter", logger);

        NormalizeRange(timings.BackgroundClickDelay, "backgroundClickDelay", logger);
        NormalizeRange(timings.CycleSleepDelay, "cycleSleepDelay", logger);
        NormalizeRange(timings.WalkDuration, "walkDuration", logger);
        NormalizeRange(timings.TurnKeyDuration, "turnKeyDuration", logger);
        NormalizeRange(timings.TurnGapJitter, "turnGapJitter", logger);

        timings.TurnGapMeanFirst = Sane(timings.TurnGapMeanFirst, "turnGapMeanFirst", logger);
        timings.TurnGapMeanSecond = Sane(timings.TurnGapMeanSecond, "turnGapMeanSecond", logger);
        timings.FocusSwitchDelay = Sane(timings.FocusSwitchDelay, "focusSwitchDelay", logger);
        timings.InitFocusDelay = Sane(timings.InitFocusDelay, "initFocusDelay", logger);
        timings.MarketplaceOpenDelay = Sane(timings.MarketplaceOpenDelay, "marketplaceOpenDelay", logger);
        timings.TabletOpenDelay = Sane(timings.TabletOpenDelay, "tabletOpenDelay", logger);
        timings.EscDelay = Sane(timings.EscDelay, "escDelay", logger);
        timings.WarningClickDelay = Sane(timings.WarningClickDelay, "warningClickDelay", logger);
        timings.MapCloseDelay = Sane(timings.MapCloseDelay, "mapCloseDelay", logger);
        timings.PostTurnDelay = Sane(timings.PostTurnDelay, "postTurnDelay", logger);
        timings.PostWalkDelay = Sane(timings.PostWalkDelay, "postWalkDelay", logger);
    }

    /// <summary>
    /// A range with min above max is not a harmless typo. RandomRange.Sample computes
    /// NextDouble() * (Max - Min) + Min, so an inverted range returns values below Min - negative
    /// ones for the small defaults here - and a negative TimeSpan reaches Thread.Sleep in
    /// StateDetector, which throws.
    /// </summary>
    private static void NormalizeRange(RandomRange range, string field, IAppLogger logger)
    {
        if (range.Min > range.Max)
        {
            logger.Warning($"Config: {field} has min {range.Min} above max {range.Max}. Swapped.");
            (range.Min, range.Max) = (range.Max, range.Min);
        }

        range.Min = Sane(range.Min, $"{field}.min", logger);
        range.Max = Sane(range.Max, $"{field}.max", logger);
    }

    private static double Sane(double value, string field, IAppLogger logger)
    {
        if (double.IsNaN(value))
        {
            logger.Warning($"Config: {field} is not a number. Using 0.");
            return 0;
        }

        if (value < 0)
        {
            logger.Warning($"Config: {field} is negative ({value}). Using 0.");
            return 0;
        }

        if (value > MaxDelaySeconds)
        {
            logger.Warning($"Config: {field} is {value} seconds. Clamped to {MaxDelaySeconds}.");
            return MaxDelaySeconds;
        }

        return value;
    }

    private static void ValidateUpdate(UpdateSettings update, IAppLogger logger)
    {
        if (update.CheckIntervalHours < 1)
        {
            logger.Warning($"Config: update.checkIntervalHours is {update.CheckIntervalHours}. Checking once an hour instead.");
            update.CheckIntervalHours = 1;
        }

        if (string.IsNullOrWhiteSpace(update.GitHubOwner) || string.IsNullOrWhiteSpace(update.GitHubRepo))
        {
            logger.Warning("Config: update.gitHubOwner or update.gitHubRepo is blank, so update checks will fail. Restoring the defaults.");
            update.GitHubOwner = new UpdateSettings().GitHubOwner;
            update.GitHubRepo = new UpdateSettings().GitHubRepo;
        }
    }
}
