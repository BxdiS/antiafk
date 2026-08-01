using System.Text;

namespace AntiAfk.Core.Vision;

/// <summary>
/// What a spawn icon looks like, reduced to something that can be compared and written down.
///
/// The icons are white glyphs on a dark translucent disc laid over the city map, so the glyph is
/// the only part that is the same for every player: the disc dims whatever is behind it, and the
/// glyph on top is drawn at full white. How white each part of the icon box is, sampled down to a
/// 16x16 grid, is small enough to keep in a table in source and specific enough to tell a house
/// from a garage.
///
/// Cells hold how white they are rather than whether they are white. A hard yes/no threshold makes
/// every anti-aliased edge cell a coin toss, and since a glyph is mostly edge, two captures of the
/// same icon disagree on a scattering of cells for no reason to do with the icon. Measured on the
/// real bar, a graded cell keeps the same icon within a few hundredths of itself while different
/// icons stay far apart.
///
/// Colour deliberately plays no part. The map behind the disc is a different colour under every
/// icon and changes with the time of day in game, so any signature that took it into account would
/// be matching on the map rather than on the icon.
/// </summary>
public sealed class SpawnIconSignature
{
    /// Cells per side. 16x16 at one hex digit per cell is 256 characters - long, but it is data,
    /// and it diffs one icon at a time.
    public const int Grid = 16;

    /// Levels a cell is quantised to. 16 fits a hex digit and is far finer than the noise between
    /// two captures of the same icon.
    public const int Levels = 16;

    private const int CellCount = Grid * Grid;

    /// Below this a pixel counts as fully disc. The discs measure in the 40-80 range against a
    /// glyph at 250, so there is a wide gap to put the ramp in.
    private const int WhiteRampLow = 120;

    /// At or above this a pixel counts as fully glyph.
    private const int WhiteRampHigh = 230;

    /// How far apart the channels may be before a bright pixel counts as coloured rather than
    /// white. The map shows through the disc tinted blue, so a bright but clearly blue pixel is
    /// background, not glyph.
    private const int MaxChannelSpread = 45;

    /// Cells, row-major, each 0..Levels-1.
    private readonly byte[] _cells;

    private SpawnIconSignature(byte[] cells)
    {
        _cells = cells;

        var total = 0;
        foreach (var cell in cells)
        {
            total += cell;
        }

        Fill = total / (double)(CellCount * (Levels - 1));
    }

    /// Share of the box covered by glyph, 0..1. Only used for logging.
    public double Fill { get; }

    /// <summary>
    /// Whether a pixel is white enough to be part of a glyph. This is the yes/no version, used to
    /// find the glyph rather than to describe it - see SpawnBarDetector.
    /// </summary>
    public static bool IsGlyphPixel(byte r, byte g, byte b) => Whiteness(r, g, b) >= 0.5;

    /// <summary>
    /// How much this pixel looks like glyph rather than disc, 0..1.
    /// </summary>
    public static double Whiteness(byte r, byte g, byte b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        if (max - min > MaxChannelSpread)
        {
            return 0;
        }

        if (min <= WhiteRampLow)
        {
            return 0;
        }

        if (min >= WhiteRampHigh)
        {
            return 1;
        }

        return (min - WhiteRampLow) / (double)(WhiteRampHigh - WhiteRampLow);
    }

    /// <summary>
    /// Samples the square of side <paramref name="boxSize"/> around (<paramref name="centerX"/>,
    /// <paramref name="centerY"/>) in grid-local coordinates.
    ///
    /// The box is divided into Grid x Grid cells rather than sampled at fixed pixel offsets, so the
    /// same code produces a comparable signature at any resolution - a 1440p icon is simply a
    /// bigger box divided the same way.
    ///
    /// It is then centred on the glyph rather than on the point it was asked about. The detector
    /// locates the row to within a few pixels, and a few pixels is most of a cell: without this the
    /// same icon sampled from two screenshots lands on a different grid and the two signatures stop
    /// matching each other for reasons that have nothing to do with the icon.
    /// </summary>
    public static SpawnIconSignature Sample(PixelGrid grid, int centerX, int centerY, int boxSize)
    {
        var cells = new byte[CellCount];
        var (glyphX, glyphY) = FindGlyphCenter(grid, centerX, centerY, boxSize);
        var left = glyphX - boxSize / 2;
        var top = glyphY - boxSize / 2;

        for (var cellY = 0; cellY < Grid; cellY++)
        {
            var y0 = top + cellY * boxSize / Grid;
            var y1 = top + (cellY + 1) * boxSize / Grid;

            for (var cellX = 0; cellX < Grid; cellX++)
            {
                var x0 = left + cellX * boxSize / Grid;
                var x1 = left + (cellX + 1) * boxSize / Grid;

                var total = 0;
                var whiteness = 0.0;

                for (var y = y0; y < y1; y++)
                {
                    for (var x = x0; x < x1; x++)
                    {
                        if (!grid.Contains(x, y))
                        {
                            continue;
                        }

                        total++;
                        var (r, g, b) = grid[x, y];
                        whiteness += Whiteness(r, g, b);
                    }
                }

                var level = total == 0 ? 0 : (int)Math.Round(whiteness / total * (Levels - 1));
                cells[cellY * Grid + cellX] = (byte)Math.Clamp(level, 0, Levels - 1);
            }
        }

        return new SpawnIconSignature(cells);
    }

    /// <summary>
    /// The centre of mass of the glyph, searched a little wider than the box itself so a glyph
    /// sitting slightly off the icon centre is still found whole. Falls back to the requested
    /// centre when there is no glyph there to find.
    ///
    /// The search area is a disc, not a square, and that is the whole point of it. A square of the
    /// same reach has corners further out than its sides - far enough to clear the icon's own dark
    /// disc and land on the map behind it. Over a bright map, which is most of them from the air,
    /// those corners read as white, drag the centre of mass towards one of them, and the glyph gets
    /// sampled a row or two off. Two captures of the same icon then disagree about which row its
    /// head is on, which is not a difference between icons at all.
    /// </summary>
    private static (int X, int Y) FindGlyphCenter(PixelGrid grid, int centerX, int centerY, int boxSize)
    {
        var reach = boxSize * 2 / 3;
        var reachSquared = reach * reach;
        var sumX = 0.0;
        var sumY = 0.0;
        var weight = 0.0;

        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                if (dx * dx + dy * dy > reachSquared)
                {
                    continue;
                }

                var x = centerX + dx;
                var y = centerY + dy;
                if (!grid.Contains(x, y))
                {
                    continue;
                }

                var (r, g, b) = grid[x, y];
                var whiteness = Whiteness(r, g, b);
                if (whiteness <= 0)
                {
                    continue;
                }

                sumX += x * whiteness;
                sumY += y * whiteness;
                weight += whiteness;
            }
        }

        return weight <= 0
            ? (centerX, centerY)
            : ((int)Math.Round(sumX / weight), (int)Math.Round(sumY / weight));
    }

    /// <summary>
    /// Weighted Jaccard distance: 0 for identical, 1 for nothing in common.
    ///
    /// Overlap rather than plain per-cell difference, because a glyph covers well under half its
    /// box. Summing differences would score every pair of icons in the high nineties on the empty
    /// background they share, and the part that tells them apart would be lost in the rounding.
    /// </summary>
    public double DistanceTo(SpawnIconSignature other)
    {
        var intersection = 0;
        var union = 0;

        for (var i = 0; i < CellCount; i++)
        {
            var a = _cells[i];
            var b = other._cells[i];
            intersection += Math.Min(a, b);
            union += Math.Max(a, b);
        }

        return union == 0 ? 1.0 : 1.0 - intersection / (double)union;
    }

    public string ToHex()
    {
        var builder = new StringBuilder(CellCount);

        foreach (var cell in _cells)
        {
            builder.Append(cell.ToString("x1"));
        }

        return builder.ToString();
    }

    public static SpawnIconSignature FromHex(string hex)
    {
        if (hex.Length != CellCount)
        {
            throw new ArgumentException(
                $"A signature is {CellCount} hex characters ({Grid}x{Grid} cells), got {hex.Length}.", nameof(hex));
        }

        var cells = new byte[CellCount];
        for (var i = 0; i < hex.Length; i++)
        {
            cells[i] = (byte)Convert.ToInt32(hex[i].ToString(), 16);
        }

        return new SpawnIconSignature(cells);
    }

    /// The glyph drawn as text, one line per row. Only used for logging - looking at the shape is
    /// the fastest way to tell a misdetected slot from an icon this build has no template for.
    public string ToAsciiArt()
    {
        const string ramp = " .:-=+*#%@";
        var builder = new StringBuilder();

        for (var y = 0; y < Grid; y++)
        {
            for (var x = 0; x < Grid; x++)
            {
                var level = _cells[y * Grid + x] * (ramp.Length - 1) / (Levels - 1);
                builder.Append(ramp[level]);
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }
}
