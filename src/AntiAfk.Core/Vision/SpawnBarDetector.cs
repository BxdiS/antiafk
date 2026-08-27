namespace AntiAfk.Core.Vision;

/// One icon found on the spawn bar. Slot is its position from the left, counting only icons that
/// are actually there.
public sealed record SpawnIconHit(
    int Slot,
    int ScreenX,
    int ScreenY,
    SpawnIconSignature Signature,
    double Score,
    double GlyphRatio,
    double DiscRatio);

/// The bar as it was found on screen: which icons, and how convincing the fit was.
public sealed record SpawnBarReading(IReadOnlyList<SpawnIconHit> Icons, int RowY, double Confidence)
{
    public int Count => Icons.Count;
}

/// What one position on the bar looks like. Exposed for tooling: when detection fails, the
/// per-position numbers are the difference between tuning a threshold and guessing at one.
public sealed record SpawnSlotProbe(int CenterX, double Score, double GlyphRatio, double DiscRatio);

/// <summary>
/// Finds the row of spawn icons along the bottom of the map screen.
///
/// The problem this solves is that the bar has no fixed layout. Every player owns a different set
/// of spawn points, so the bar holds anywhere from a couple of icons to a dozen, and because it is
/// centred, adding one moves every icon that was already there. A fixed click - which is what this
/// replaced - therefore lands on a different spawn point for every player, and on a different one
/// again the day they buy a house.
///
/// What does not change is the spacing between icons and the row they sit on. So rather than
/// hunting for edges, this fits the one thing it knows: for each possible icon count the positions
/// are fully determined, and the right count is the one where every slot has an icon in it and the
/// positions just outside both ends are empty. That last part is what makes it a fit rather than a
/// guess - a count that is too small still has all its slots filled, and is rejected because there
/// is another icon sitting beyond the end.
/// </summary>
public static class SpawnBarDetector
{
    private const int MinIcons = 2;

    /// A pixel at or below this luminance counts as part of the dark disc behind a glyph. The
    /// discs are drawn dark and translucent over the map, so this has to clear the darkest parts
    /// of the city seen from above without reaching the disc itself.
    private const int DiscMaxLuminance = 110;

    /// Share of the ring inside a disc that has to be dark for a slot to look like an icon.
    private const double MinDiscRatio = 0.70;

    /// Glyph share of the glyph box at which a slot counts as fully convincing. The glyphs are
    /// line art, not solid shapes, so even a large one covers well under a tenth of its box.
    private const double TargetGlyphRatio = 0.06;

    /// Below this there is no glyph, above it the box is not a glyph but something white behind
    /// the bar - the map has bright patches and the disc does not cover the whole box.
    private const double MinGlyphRatio = 0.015;
    private const double MaxGlyphRatio = 0.60;

    /// Score a slot has to reach to hold an icon, and that the positions past both ends have to
    /// stay under for the count to be the right one.
    public const double MinSlotScore = 0.55;

    /// Step used when searching for the exact row. Finer than this measures nothing: the glyph box
    /// is sampled in cells several pixels across, so a two-pixel error changes no cell.
    private const int RowSearchStep = 4;

    /// <summary>
    /// Returns the icons found in <paramref name="strip"/>, or null when the bar is not there -
    /// which is the normal answer on every screen other than spawn selection.
    /// </summary>
    public static SpawnBarReading? Detect(PixelGrid strip, SpawnBarLayout layout)
    {
        var scores = new Dictionary<(int RowY, int CenterX), SpawnSlotProbe>();

        SpawnBarReading? best = null;

        for (var offset = -layout.RowSearchMargin; offset <= layout.RowSearchMargin; offset += RowSearchStep)
        {
            var rowY = layout.RowY + offset;

            for (var count = MinIcons; count <= layout.MaxIcons; count++)
            {
                var reading = TryFit(strip, layout, rowY, count, scores);
                if (reading is not null && (best is null || reading.Confidence > best.Confidence))
                {
                    best = reading;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Scores a single position without fitting a whole bar around it. Null when the position is
    /// not fully inside the strip. Only useful for tools: it is how the thresholds above were set,
    /// and how a screenshot where detection fails gets turned into numbers.
    /// </summary>
    public static SpawnSlotProbe? Probe(PixelGrid strip, SpawnBarLayout layout, int centerX, int rowY) =>
        ProbeSlot(strip, layout, centerX, rowY, []);

    /// <summary>
    /// Checks one (row, count) pair. Returns null unless every slot holds an icon and neither
    /// position beyond the ends does.
    /// </summary>
    private static SpawnBarReading? TryFit(
        PixelGrid strip,
        SpawnBarLayout layout,
        int rowY,
        int count,
        Dictionary<(int, int), SpawnSlotProbe> cache)
    {
        var slots = new SpawnSlotProbe[count];
        var weakest = double.MaxValue;

        for (var index = 0; index < count; index++)
        {
            var centerX = layout.SlotCenterX(index, count);
            var score = ProbeSlot(strip, layout, centerX, rowY, cache);

            if (score is null || score.Score < MinSlotScore)
            {
                return null;
            }

            slots[index] = score;
            weakest = Math.Min(weakest, score.Score);
        }

        // The count is only right if the bar stops where this arrangement says it does. Without
        // this test every count from 2 up to the real one fits, because their slots are a subset
        // of the icons that are on screen.
        var strongestFlank = 0.0;
        foreach (var flankX in new[]
                 {
                     layout.SlotCenterX(-1, count),
                     layout.SlotCenterX(count, count)
                 })
        {
            var flank = ProbeSlot(strip, layout, flankX, rowY, cache);
            if (flank is null)
            {
                // Off the edge of the captured strip. Nothing can be there, so nothing to rule out.
                continue;
            }

            if (flank.Score >= MinSlotScore)
            {
                return null;
            }

            strongestFlank = Math.Max(strongestFlank, flank.Score);
        }

        var icons = new SpawnIconHit[count];
        for (var index = 0; index < count; index++)
        {
            var slot = slots[index];
            icons[index] = new SpawnIconHit(
                index,
                slot.CenterX,
                rowY,
                SpawnIconSignature.Sample(strip, strip.ToLocalX(slot.CenterX), strip.ToLocalY(rowY), layout.GlyphBox),
                slot.Score,
                slot.GlyphRatio,
                slot.DiscRatio);
        }

        return new SpawnBarReading(icons, rowY, weakest - strongestFlank);
    }

    /// <summary>
    /// How much the position at (<paramref name="centerX"/>, <paramref name="rowY"/>) looks like a
    /// spawn icon: a dark background with a white glyph in it. Null when the position is not fully
    /// inside the captured strip.
    /// </summary>
    private static SpawnSlotProbe? ProbeSlot(
        PixelGrid strip,
        SpawnBarLayout layout,
        int centerX,
        int rowY,
        Dictionary<(int, int), SpawnSlotProbe> cache)
    {
        if (cache.TryGetValue((rowY, centerX), out var cached))
        {
            return cached;
        }

        var localX = strip.ToLocalX(centerX);
        var localY = strip.ToLocalY(rowY);
        var reach = layout.Diameter / 2;

        var ringTotal = 0;
        var ringDark = 0;

        if (layout.CircularBackground)
        {
            if (!strip.Contains(localX - reach, localY - reach) || !strip.Contains(localX + reach, localY + reach))
            {
                return null;
            }

            var innerRadius = layout.Diameter * 0.36;
            var outerRadius = layout.Diameter * 0.46;
            var innerSquared = innerRadius * innerRadius;
            var outerSquared = outerRadius * outerRadius;

            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared < innerSquared || distanceSquared > outerSquared)
                    {
                        continue;
                    }

                    var (r, g, b) = strip[localX + dx, localY + dy];
                    ringTotal++;

                    if (PixelGrid.Luminance(r, g, b) <= DiscMaxLuminance)
                    {
                        ringDark++;
                    }
                }
            }
        }
        else
        {
            var halfPitch = layout.Pitch / 2;
            if (!strip.Contains(localX - halfPitch, localY - reach) || !strip.Contains(localX + halfPitch, localY + reach))
            {
                return null;
            }

            var glyphHalf = layout.GlyphBox / 2;
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -halfPitch; dx <= halfPitch; dx++)
                {
                    if (Math.Abs(dx) <= glyphHalf && Math.Abs(dy) <= glyphHalf)
                    {
                        continue;
                    }

                    var (r, g, b) = strip[localX + dx, localY + dy];
                    ringTotal++;

                    if (PixelGrid.Luminance(r, g, b) <= DiscMaxLuminance)
                    {
                        ringDark++;
                    }
                }
            }
        }

        var discRatio = ringTotal == 0 ? 0 : ringDark / (double)ringTotal;

        var boxHalf = layout.GlyphBox / 2;
        var glyphTotal = 0;
        var glyphPixels = 0;

        for (var dy = -boxHalf; dy <= boxHalf; dy++)
        {
            for (var dx = -boxHalf; dx <= boxHalf; dx++)
            {
                var (r, g, b) = strip[localX + dx, localY + dy];
                glyphTotal++;

                if (SpawnIconSignature.IsGlyphPixel(r, g, b))
                {
                    glyphPixels++;
                }
            }
        }

        var glyphRatio = glyphTotal == 0 ? 0 : glyphPixels / (double)glyphTotal;
        var score = ComputeScore(discRatio, glyphRatio);

        var result = new SpawnSlotProbe(centerX, score, glyphRatio, discRatio);
        cache[(rowY, centerX)] = result;
        return result;
    }

    private static double ComputeScore(double discRatio, double glyphRatio)
    {
        if (discRatio < MinDiscRatio || glyphRatio < MinGlyphRatio || glyphRatio > MaxGlyphRatio)
        {
            return 0;
        }

        // Both parts have to hold up: a disc with nothing on it is the gap between two icons seen
        // against a dark rooftop, and a white shape with no disc under it is the map.
        var glyphStrength = Math.Min(1.0, glyphRatio / TargetGlyphRatio);
        return discRatio * glyphStrength;
    }
}
