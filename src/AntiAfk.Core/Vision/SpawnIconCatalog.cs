namespace AntiAfk.Core.Vision;

/// <summary>
/// A glyph this build knows by sight, and the spawn points drawn with it.
///
/// <see cref="Ids"/> is usually one name. It is more than one where the game draws two different
/// spawn points with the same picture, and then the order matters: they are listed in the order
/// they appear on the bar, so the first icon with this glyph is the first name, the second is the
/// second. See SpawnSelector, which does the assigning.
/// </summary>
public sealed record SpawnIconTemplate(
    string Glyph,
    IReadOnlyList<string> Ids,
    IReadOnlyList<SpawnIconSignature> References)
{
    /// Distance to the nearest reference of this glyph.
    ///
    /// More than one reference per glyph because the same icon does not produce the same numbers
    /// everywhere: rendered at 1440p and at 1080p it lands up to 0.22 from itself, which eats most
    /// of the room between "this is the icon" and "this is a different icon". Keeping a reference
    /// from each resolution seen means a capture is compared against one taken the same way, and
    /// the worst case drops to a few hundredths.
    public double DistanceTo(SpawnIconSignature signature)
    {
        var best = double.MaxValue;

        foreach (var reference in References)
        {
            var distance = signature.DistanceTo(reference);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }
}

/// The closest glyph to a signature, and how far off it was.
public sealed record SpawnIconMatch(string Glyph, double Distance, string? RunnerUpGlyph, double RunnerUpDistance);

/// <summary>
/// The spawn icons this build can recognise.
///
/// Signatures are generated from screenshots by tools/GenerateSpawnIcons and pasted in here, which
/// is why they are a table of hex digits rather than image files: the app has to identify icons
/// with no help from the user, on the first run, before it has ever seen that player's bar, so
/// there is nothing to learn from at runtime and the reference data has to ship inside the exe.
///
/// Adding an icon is adding an entry. Anything not in this table is reported as unknown and
/// skipped when the priority list is applied, so an unrecognised icon costs one spawn point rather
/// than breaking the login.
///
/// The thresholds below come from measurement, not from taste. The test that produced them is the
/// one worth repeating after adding an icon: identify every icon in the sample screenshots with a
/// catalog that does not contain that icon's own reference. Across three screenshots at two
/// resolutions, all fourteen still came out right - worst distance to their own glyph 0.22,
/// closest a different glyph ever came 0.36 - and both thresholds sit in that gap.
///
/// So an icon the catalog does not have comes back as "unknown" rather than as the nearest
/// building. That direction matters: an unrecognised icon costs one spawn point, a misrecognised
/// one spawns the player somewhere they did not ask to be.
/// </summary>
public static class SpawnIconCatalog
{
    /// Beyond this the closest template is not the same glyph. Different glyphs score 0.36 and up
    /// even across a resolution change, so this rejects them while leaving room above the 0.22 a
    /// glyph can score against its own template.
    public const double MaxMatchDistance = 0.30;

    /// How much closer the winner has to be than the runner-up. Kept small deliberately: the
    /// distance limit above is what rules out a wrong glyph, and a larger margin here only rejects
    /// correct matches.
    public const double MinMatchMargin = 0.05;

    private static readonly SpawnIconTemplate[] Catalog = BuildCatalog();

    public static IReadOnlyList<SpawnIconTemplate> All => Catalog;

    /// Every spawn id the catalog can name, in no particular order.
    public static IEnumerable<string> KnownIds => Catalog.SelectMany(template => template.Ids);

    public static bool IsKnown(string id) =>
        KnownIds.Any(known => string.Equals(known, id, StringComparison.OrdinalIgnoreCase));

    /// The names drawn with <paramref name="glyph"/>, in the order they appear on the bar.
    public static IReadOnlyList<string> IdsForGlyph(string glyph) =>
        Catalog.FirstOrDefault(template => template.Glyph == glyph)?.Ids ?? [];

    /// <summary>
    /// The glyph <paramref name="signature"/> is, or null when it matches nothing well enough to
    /// be sure.
    /// </summary>
    public static SpawnIconMatch? Match(SpawnIconSignature signature)
    {
        var closest = Closest(signature);

        if (closest is null || closest.Distance > MaxMatchDistance)
        {
            return null;
        }

        if (closest.RunnerUpGlyph is not null && closest.RunnerUpDistance - closest.Distance < MinMatchMargin)
        {
            return null;
        }

        return closest;
    }

    /// <summary>
    /// The nearest glyph whatever the distance, for logging. An icon this build has no template
    /// for still has a nearest neighbour, and how far away it is says whether the catalog is
    /// missing an entry or the detector found something that is not an icon at all.
    /// </summary>
    public static SpawnIconMatch? Closest(SpawnIconSignature signature)
    {
        if (Catalog.Length == 0)
        {
            return null;
        }

        var bestGlyph = string.Empty;
        var bestDistance = double.MaxValue;
        string? runnerUpGlyph = null;
        var runnerUpDistance = double.MaxValue;

        foreach (var template in Catalog)
        {
            var distance = template.DistanceTo(signature);

            if (distance < bestDistance)
            {
                runnerUpGlyph = bestGlyph.Length == 0 ? null : bestGlyph;
                runnerUpDistance = bestDistance;
                bestGlyph = template.Glyph;
                bestDistance = distance;
            }
            else if (distance < runnerUpDistance)
            {
                runnerUpGlyph = template.Glyph;
                runnerUpDistance = distance;
            }
        }

        return new SpawnIconMatch(bestGlyph, bestDistance, runnerUpGlyph, runnerUpDistance);
    }

    // Ids are what the config's spawn priority list is written in, so they are stable, lowercase
    // and in English regardless of the interface language.
    //
    // Signatures cut from three screenshots of the real bar: 2560x1440 with five icons, and two at
    // 1920x1080 with four and five.
    private static SpawnIconTemplate[] BuildCatalog() =>
    [
        // A map with a location pin. Where the player logged out.
        new SpawnIconTemplate(
            "pin",
            ["exit_point"],
            [
                // 2560x1440
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "00006b8100000000" +
                    "0009e8ec00000000" +
                    "000f905f00000000" +
                    "000bd4ae00000000" +
                    "0002eff678860000" +
                    "00006fa5ffe38300" +
                    "0000373efa4bf800" +
                    "0000ebed47effb00" +
                    "0004ffc234dfff00" +
                    "0007e75dfb57ef20" +
                    "00054bfffffb4620" +
                    "00005aaaaaaaa000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000"),
                // 1920x1080
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000253000000000" +
                    "0007fef500000000" +
                    "000ea07d00000000" +
                    "000e907c00000000" +
                    "0008fdf722210000" +
                    "0001cfc6fff64200" +
                    "0000394ffd4af700" +
                    "0002d6cfa1dffa00" +
                    "0005ffe508fffe00" +
                    "0009fb4ce75dff10" +
                    "000756ffffe84a40" +
                    "00009ffffffff000" +
                    "0000011000001000" +
                    "0000000000000000" +
                    "0000000000000000"),
                // 1920x1080_2
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000045000000000" +
                    "0002eefb00000000" +
                    "0009f23f50000000" +
                    "0009f12f50000000" +
                    "0003feec12320000" +
                    "00006ff5effb1300" +
                    "0000265cff67fc00" +
                    "0000b99ff39fff00" +
                    "0000fff913ffff40" +
                    "0004fd59fa4aff60" +
                    "000384dffffa4770" +
                    "00001ffffffff500" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000")
            ]),

        // An aeroplane. The spawn every new character starts with.
        new SpawnIconTemplate(
            "airplane",
            ["starting_spawn"],
            [
                // 1920x1080
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000010000" +
                    "00000000001eb000" +
                    "0000000000be5000" +
                    "0006dfffffd20000" +
                    "0000027dffa00000" +
                    "0000000dffa00000" +
                    "000056ae6fa00000" +
                    "00004cf20aa00000" +
                    "000003c205a00000" +
                    "0000003001a00000" +
                    "0000000000300000" +
                    "0000000000000000" +
                    "0000000000000000")
            ]),

        // A house with a pitched roof.
        new SpawnIconTemplate(
            "house",
            ["personal_house"],
            [
                // 2560x1440
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000008a80000" +
                    "000299999cbc5000" +
                    "0007ffffffffe000" +
                    "000dfffffffff400" +
                    "002ffffd9ffff700" +
                    "006fffe559fffb00" +
                    "0002446df8444000" +
                    "0009c7c88fbaf000" +
                    "000980920e10d000" +
                    "003cc8d98e77d500" +
                    "008ffffffffffb00" +
                    "0036666666666400" +
                    "0000000000000000" +
                    "0000000000000000")
            ]),

        // A tower block with rows of windows.
        new SpawnIconTemplate(
            "tower",
            ["personal_apartment"],
            [
                // 2560x1440
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000244440000000" +
                    "0000affff4000000" +
                    "0001fffffc000000" +
                    "0001f9c7fc6bb000" +
                    "0000feedfc7ff000" +
                    "0001f8b5fc75f000" +
                    "0001fdedfb7df000" +
                    "0001f8c6fc76f100" +
                    "0001fffffc7ff100" +
                    "0000f9b7fb774100" +
                    "0000feedfb73f700" +
                    "0000fffffb76a100" +
                    "0000bbbbbbabba10" +
                    "0000000000000000" +
                    "0000000000000000")
            ]),

        // Two people. The game draws the family house and the family mansion with this same
        // icon - not a similar one, the same one: their pixel masks differ by 3 pixels out of
        // 1936, which is the anti-aliasing of a half-pixel offset. Nothing can tell them apart
        // by sight, so they are told apart by counting, and the order below is the order they
        // sit in on the bar. SpawnSelector lines the names up with the end of this list because
        // the mansion cannot be owned without the house: one icon is the house, two are the
        // mansion followed by the house.
        new SpawnIconTemplate(
            "people",
            ["family_mansion", "family_house"],
            [
                // 2560x1440
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "00008fb005ed1000" +
                    "0000eff30bff5000" +
                    "00008fa8d5fd1000" +
                    "0003744ae2667000" +
                    "003fff9243eff800" +
                    "007ffb5ffc6ffb00" +
                    "008ff8bfff6ffb00" +
                    "0000008aaa200000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000"),
                // 1920x1080
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000010000000000" +
                    "00006fb002ed1000" +
                    "0000fff207ff8000" +
                    "00009fd6a6fe3000" +
                    "0003864af5668100" +
                    "000effd023fff900" +
                    "004fff4cd97ffd00" +
                    "005ffb9fff5fff00" +
                    "0012217aaa311100" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000")
            ]),

        // An office block with a column of windows beside it.
        new SpawnIconTemplate(
            "office",
            ["family_office"],
            [
                // 2560x1440
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000000024000000" +
                    "0000027cff000000" +
                    "00009fffff000000" +
                    "0001fecdff000000" +
                    "0002fb45ff150000" +
                    "0002feabff3fd000" +
                    "0002fb45ff3ff300" +
                    "0002fd88ff3ff400" +
                    "0002fd67ff3ff400" +
                    "0002ffffff3ff400" +
                    "0002ffffff3ff400" +
                    "0001dddddb3dd200" +
                    "0000000000000000" +
                    "0000000000000000" +
                    "0000000000000000"),
                // 1920x1080
                SpawnIconSignature.FromHex(
                    "0000000000000000" +
                    "0000000000000000" +
                    "00000015ae100000" +
                    "000029efff200000" +
                    "0000ffffff200000" +
                    "0002ff66ff200000" +
                    "0002ffccff2e9100" +
                    "0002ff33ff2ff600" +
                    "0002ffffff2ff700" +
                    "0002ff22ff2ff700" +
                    "0002ffffff2ff700" +
                    "0002ffffff2ff700" +
                    "0002ffffff2ff700" +
                    "0000555555055200" +
                    "0000000000000000" +
                    "0000000000000000")
            ]),
    ];
}
