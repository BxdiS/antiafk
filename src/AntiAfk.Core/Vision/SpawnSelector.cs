namespace AntiAfk.Core.Vision;

/// <summary>
/// An icon on the bar together with what it was taken to be.
///
/// <see cref="Id"/> is null when the icon could not be named - either no glyph matched, or the
/// glyph matched but the bar holds more icons drawn with it than there are names for it.
/// <see cref="Closest"/> is the nearest glyph either way, which is what turns "unknown icon" in a
/// log into something that can be acted on.
/// </summary>
public sealed record IdentifiedSpawnIcon(
    SpawnIconHit Icon,
    string? Id,
    SpawnIconMatch? Match,
    SpawnIconMatch? Closest)
{
    public const string UnknownId = "unknown";

    public string Label => Id ?? UnknownId;
}

/// <summary>
/// What the bar turned out to hold and which icon is going to be clicked.
/// <see cref="MatchedPriorityId"/> is null when nothing on the priority list was on the bar and
/// the leftmost icon was taken instead.
/// </summary>
public sealed record SpawnSelection(
    IReadOnlyList<IdentifiedSpawnIcon> Icons,
    IdentifiedSpawnIcon Chosen,
    string? MatchedPriorityId)
{
    public bool IsFallback => MatchedPriorityId is null;
}

/// <summary>
/// Names the icons on a bar that has already been found on screen, then applies the configured
/// priority to them.
///
/// Priority is a list of spawn ids in the order the player wants them, and the first one actually
/// on the bar wins. That is the point of recognising icons rather than counting positions: which
/// spawn points a player has varies, so "the third one" means a different place for every player,
/// while "my house, or my flat if I have no house" means the same thing for everybody.
///
/// Naming is not purely a matter of recognising the picture, because the game draws some spawn
/// points with the same picture - the family house and the family mansion are the same two-person
/// icon. Nothing can tell those apart by sight, so they are told apart by counting: the catalog
/// lists the names sharing a glyph in bar order, and the icons carrying that glyph take those
/// names left to right. A player who owns only the later of the two therefore gets the earlier
/// name, which is wrong; there is no way to detect that case from the screen, and it is the reason
/// the priority list should not be relied on to tell those two apart.
///
/// When nothing on the priority list is there - a new player who owns nothing, or a bar full of
/// icons this build has no template for - the leftmost icon is taken. Some spawn point is always
/// better than leaving the game sitting on the map screen until it times out.
/// </summary>
public static class SpawnSelector
{
    public static SpawnSelection Select(SpawnBarReading reading, IReadOnlyList<string> priority)
    {
        if (reading.Icons.Count == 0)
        {
            throw new ArgumentException("Cannot choose a spawn point from an empty bar.", nameof(reading));
        }

        var identified = Identify(reading.Icons);

        foreach (var wanted in priority)
        {
            var match = identified.FirstOrDefault(
                candidate => candidate.Id is not null
                             && string.Equals(candidate.Id, wanted, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return new SpawnSelection(identified, match, match.Id);
            }
        }

        return new SpawnSelection(identified, identified[0], null);
    }

    private static IdentifiedSpawnIcon[] Identify(IReadOnlyList<SpawnIconHit> icons)
    {
        var matches = new SpawnIconMatch?[icons.Count];
        var closest = new SpawnIconMatch?[icons.Count];
        var totalPerGlyph = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < icons.Count; index++)
        {
            matches[index] = SpawnIconCatalog.Match(icons[index].Signature);
            closest[index] = SpawnIconCatalog.Closest(icons[index].Signature);

            if (matches[index] is { } match)
            {
                totalPerGlyph[match.Glyph] = totalPerGlyph.GetValueOrDefault(match.Glyph) + 1;
            }
        }

        var seenPerGlyph = new Dictionary<string, int>(StringComparer.Ordinal);
        var identified = new IdentifiedSpawnIcon[icons.Count];

        for (var index = 0; index < icons.Count; index++)
        {
            string? id = null;

            if (matches[index] is { } match)
            {
                var names = SpawnIconCatalog.IdsForGlyph(match.Glyph);
                var present = totalPerGlyph[match.Glyph];
                var seen = seenPerGlyph.GetValueOrDefault(match.Glyph);
                seenPerGlyph[match.Glyph] = seen + 1;

                // Names sharing a glyph are lined up with the END of the catalog's list, because
                // the ones that can be missing are the ones listed first: the family mansion cannot
                // be owned without the family house, so a bar with a single two-person icon is
                // showing the house, not the mansion. Lining up from the start instead would call
                // that single icon a mansion and spawn the player at a property they do not own.
                //
                // A bar with more of a glyph than there are names for it leaves the extra ones
                // unnamed. Guessing there would put the player somewhere at random.
                var offset = names.Count - present;
                var nameIndex = seen + offset;
                id = nameIndex >= 0 && nameIndex < names.Count ? names[nameIndex] : null;
            }

            identified[index] = new IdentifiedSpawnIcon(icons[index], id, matches[index], closest[index]);
        }

        return identified;
    }
}
