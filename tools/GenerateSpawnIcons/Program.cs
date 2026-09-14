// Turns screenshots of the spawn screen into the icon table in SpawnIconCatalog.
//
// The app has to recognise spawn icons on the first run, with no help from the user, so the
// reference data cannot be learned at runtime - it has to ship inside the exe. This is where it
// comes from: run it over a screenshot, check the glyphs it prints look like the icons on screen,
// and paste the lines it emits into SpawnIconCatalog.
//
//   dotnet run --project tools/GenerateSpawnIcons -- <screenshot.png|folder> [more...] [--crops <dir>] [--project majestic|russia_online]
//
// Also prints the per-position numbers the detector worked from, which is what to look at when a
// screenshot does not detect: it says whether a slot was missed for want of a disc or a glyph.

using System.Drawing;
using AntiAfk.Core.Constants;
using AntiAfk.Core.Models;
using AntiAfk.Core.Vision;
using AntiAfk.Infrastructure.Services;

var inputs = new List<string>();
string? cropDirectory = null;
var project = GameConstants.DefaultProject;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--crops" or "-c")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("--crops needs a directory.");
            return 2;
        }

        cropDirectory = args[++i];
        continue;
    }

    if (args[i] is "--project" or "-p")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("--project needs a value (majestic or russia_online).");
            return 2;
        }

        project = args[++i];
        continue;
    }

    inputs.Add(args[i]);
}

if (inputs.Count == 0)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/GenerateSpawnIcons -- <screenshot.png|folder> [...] [--crops <dir>] [--project majestic|russia_online]");
    return 2;
}

var profile = ProjectProfile.ForProject(project);
Console.WriteLine($"Project: {profile.Id}");

var files = new List<string>();
foreach (var input in inputs)
{
    if (Directory.Exists(input))
    {
        files.AddRange(Directory.EnumerateFiles(input, "*.png").OrderBy(path => path));
        continue;
    }

    if (File.Exists(input))
    {
        files.Add(input);
        continue;
    }

    Console.Error.WriteLine($"Not found: {input}");
    return 2;
}

if (cropDirectory is not null)
{
    Directory.CreateDirectory(cropDirectory);
}

foreach (var file in files)
{
    Console.WriteLine();
    Console.WriteLine($"=== {Path.GetFileName(file)} ===");
    Report(file, cropDirectory, profile);
}

return 0;

static void Report(string file, string? cropDirectory, ProjectProfile profile)
{
    using var bitmap = new Bitmap(file);

    // A screenshot is the game window, so it stands in for one at (0,0) of its own size. That is
    // also what makes a 1440p screenshot work here without a second set of measurements.
    var layout = SpawnBarLayout.ForWindow(profile.SpawnBar, 0, 0, bitmap.Width, bitmap.Height);
    var grid = ScreenCaptureService.ToPixelGrid(bitmap, 0, 0);

    Console.WriteLine(
        $"{bitmap.Width}x{bitmap.Height}: expecting the bar centred on x={layout.CenterX}, row y={layout.RowY}, " +
        $"pitch {layout.Pitch}, disc {layout.Diameter}, glyph box {layout.GlyphBox}");

    var strip = Crop(grid, layout);
    ScanBarExtent(strip, layout);
    var reading = SpawnBarDetector.Detect(strip, layout);

    if (reading is null)
    {
        Console.WriteLine("No spawn bar detected. Positions on the expected row, best first:");
        DumpRow(strip, layout, layout.RowY);
        Console.WriteLine();
        Console.WriteLine("Scanning ±80 pixels around expected row for any glyph signal:");
        ScanRows(strip, layout);
        Console.WriteLine();
        Console.WriteLine("Glyph pixel column density (peaks are icon centres):");
        DumpGlyphPeaks(strip, layout);
        Console.WriteLine();
        Console.WriteLine("Glyph mask (# = glyph pixel, . = dark background, ' ' = bright/other):");
        DumpGlyphMask(strip, layout);
        Console.WriteLine();
        var dumpPath = Path.ChangeExtension(file, ".strip.png");
        DumpStripPng(strip, dumpPath);
        Console.WriteLine($"Captured strip saved to {dumpPath}");
        return;
    }

    Console.WriteLine($"Detected {reading.Count} icon(s) on row y={reading.RowY}, fit {reading.Confidence:F2}.");
    Console.WriteLine();

    // Named through SpawnSelector with the shipped priority list rather than the catalog directly,
    // so the tool reports what the app would actually do with this screenshot - which icon it
    // calls what, and which one it would end up clicking.
    var priority = new SpawnSettings().Priority;
    var selection = SpawnSelector.Select(reading, priority);

    foreach (var named in selection.Icons)
    {
        var icon = named.Icon;
        var known = named.Match is not null
            ? $"\"{named.Label}\" (glyph {named.Match.Glyph}, {named.Match.Distance:F2})"
            : named.Closest is null
                ? "catalog is empty"
                : $"NO MATCH (closest glyph \"{named.Closest.Glyph}\" at {named.Closest.Distance:F2})";

        Console.WriteLine(
            $"[{icon.Slot + 1}] x={icon.ScreenX} y={icon.ScreenY} score={icon.Score:F2} " +
            $"glyph={icon.GlyphRatio:P1} disc={icon.DiscRatio:P1} - {known}");
        Console.WriteLine(Indent(icon.Signature.ToAsciiArt()));
        Console.WriteLine(EmitTemplate(named.Match?.Glyph ?? $"icon{icon.Slot + 1}", icon.Signature));
        Console.WriteLine();

        if (cropDirectory is not null)
        {
            SaveCrop(file, cropDirectory, icon, layout);
        }
    }

    var chosen = selection.Chosen;
    Console.WriteLine(
        selection.IsFallback
            ? $"WOULD CLICK icon {chosen.Icon.Slot + 1} at ({chosen.Icon.ScreenX}, {chosen.Icon.ScreenY}) - " +
              $"leftmost, because nothing on the priority list is on this bar"
            : $"WOULD CLICK icon {chosen.Icon.Slot + 1} at ({chosen.Icon.ScreenX}, {chosen.Icon.ScreenY}) - " +
              $"\"{selection.MatchedPriorityId}\", first match in the priority list");
}

// Captures the same strip the app captures at runtime, so what the tool sees and what the app
// sees are the same pixels.
static PixelGrid Crop(PixelGrid full, SpawnBarLayout layout)
{
    var left = Math.Max(0, layout.StripLeft);
    var top = Math.Max(0, layout.StripTop);
    var right = Math.Min(full.Width, layout.StripLeft + layout.StripWidth);
    var bottom = Math.Min(full.Height, layout.StripTop + layout.StripHeight);
    var width = right - left;
    var height = bottom - top;

    var rgb = new byte[width * height * 3];
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var (r, g, b) = full[left + x, top + y];
            var offset = (y * width + x) * 3;
            rgb[offset] = r;
            rgb[offset + 1] = g;
            rgb[offset + 2] = b;
        }
    }

    return new PixelGrid(left, top, width, height, rgb);
}

static void DumpRow(PixelGrid strip, SpawnBarLayout layout, int rowY)
{
    // Every position an icon could be centred on: odd counts sit on the centre, even counts
    // straddle it, so candidates fall on a half-pitch grid.
    var half = layout.Pitch / 2;
    var probes = new List<SpawnSlotProbe>();

    for (var x = layout.CenterX - layout.Pitch * 6; x <= layout.CenterX + layout.Pitch * 6; x += half)
    {
        var probe = SpawnBarDetector.Probe(strip, layout, x, rowY);
        if (probe is not null)
        {
            probes.Add(probe);
        }
    }

    foreach (var probe in probes.OrderByDescending(candidate => candidate.Score).Take(20))
    {
        var verdict = probe.Score >= SpawnBarDetector.MinSlotScore ? "icon" : "-";
        Console.WriteLine(
            $"  x={probe.CenterX,5} score={probe.Score:F2} glyph={probe.GlyphRatio:P1} " +
            $"disc={probe.DiscRatio:P1}  {verdict}");
    }
}

// Sweeps candidate rows and reports the best score found on each one - so a screenshot where the
// bar sits a few rows off can be found by eye, without guessing at row Y.
static void ScanRows(PixelGrid strip, SpawnBarLayout layout)
{
    var half = layout.Pitch / 2;
    var stripBottomScreen = strip.OriginY + strip.Height - 1;
    var minY = Math.Max(strip.OriginY + layout.Diameter / 2 + 1, layout.RowY - 80);
    var maxY = Math.Min(stripBottomScreen - layout.Diameter / 2 - 1, layout.RowY + 80);

    for (var rowY = minY; rowY <= maxY; rowY += 4)
    {
        var bestScore = 0.0;
        var bestGlyph = 0.0;
        var bestDisc = 0.0;
        var bestX = 0;

        for (var x = layout.CenterX - layout.Pitch * 6; x <= layout.CenterX + layout.Pitch * 6; x += half)
        {
            var probe = SpawnBarDetector.Probe(strip, layout, x, rowY);
            if (probe is null)
            {
                continue;
            }

            if (probe.GlyphRatio > bestGlyph)
            {
                bestGlyph = probe.GlyphRatio;
                bestScore = probe.Score;
                bestDisc = probe.DiscRatio;
                bestX = probe.CenterX;
            }
        }

        Console.WriteLine(
            $"  y={rowY,4} best glyph={bestGlyph:P1} at x={bestX,5} (score={bestScore:F2}, disc={bestDisc:P1})");
    }
}

// Finds where the dark bar background actually starts and ends horizontally, by looking at what
// share of each column is dark. This says whether the "3rd icon" the detector found sits inside
// the real bar or on the map beyond it.
static void ScanBarExtent(PixelGrid strip, SpawnBarLayout layout)
{
    var darkPerCol = new double[strip.Width];
    for (var lx = 0; lx < strip.Width; lx++)
    {
        var dark = 0;
        for (var ly = 0; ly < strip.Height; ly++)
        {
            var (r, g, b) = strip[lx, ly];
            if (PixelGrid.Luminance(r, g, b) <= layout.DiscMaxLuminance)
            {
                dark++;
            }
        }
        darkPerCol[lx] = dark / (double)strip.Height;
    }

    // Report the darkness heat map: `#` = ≥ 80% dark, `.` = 40-80%, ` ` = < 40% (map showing through).
    Console.WriteLine("Dark bar background across the strip (# = solid bar, . = partial, ' ' = map):");
    var head = new System.Text.StringBuilder("     ");
    for (var col = 0; col < strip.Width; col++)
    {
        if (col % 20 == 0)
        {
            head.Append($"{strip.OriginX + col,-20}");
        }
    }
    Console.WriteLine(head);

    var line = new System.Text.StringBuilder("     ");
    for (var col = 0; col < strip.Width; col++)
    {
        line.Append(darkPerCol[col] >= 0.80 ? '#' : darkPerCol[col] >= 0.40 ? '.' : ' ');
    }
    Console.WriteLine(line);

    // Same again but at a stricter threshold — the actual bar background is around lum 30-50,
    // dark map shadows sit higher. This second pass shows just the bar itself.
    var strictLumThreshold = 60;
    var strictPerCol = new double[strip.Width];
    for (var lx = 0; lx < strip.Width; lx++)
    {
        var dark = 0;
        for (var ly = 0; ly < strip.Height; ly++)
        {
            var (r, g, b) = strip[lx, ly];
            if (PixelGrid.Luminance(r, g, b) <= strictLumThreshold)
            {
                dark++;
            }
        }
        strictPerCol[lx] = dark / (double)strip.Height;
    }

    Console.WriteLine($"Same, but only counting luminance <= {strictLumThreshold} (the actual bar, not map shadows):");
    var strictLine = new System.Text.StringBuilder("     ");
    for (var col = 0; col < strip.Width; col++)
    {
        strictLine.Append(strictPerCol[col] >= 0.70 ? '#' : strictPerCol[col] >= 0.30 ? '.' : ' ');
    }
    Console.WriteLine(strictLine);
    Console.WriteLine();
}

// Sums glyph pixels per column across the whole strip. Local maxima are icon centres. Prints the
// top few peaks with their screen-X and the pitches between neighbours - which is what to plug
// back into ProjectProfile.SpawnBar* if the configured layout does not match.
static void DumpGlyphPeaks(PixelGrid strip, SpawnBarLayout layout)
{
    var perColumn = new int[strip.Width];
    for (var lx = 0; lx < strip.Width; lx++)
    {
        var count = 0;
        for (var ly = 0; ly < strip.Height; ly++)
        {
            var (r, g, b) = strip[lx, ly];
            if (SpawnIconSignature.IsGlyphPixel(r, g, b, layout.GlyphWhiteRampLow, layout.GlyphWhiteRampHigh))
            {
                count++;
            }
        }
        perColumn[lx] = count;
    }

    // Smooth with a small window so single-pixel noise does not read as a peak.
    var smoothed = new int[strip.Width];
    for (var lx = 0; lx < strip.Width; lx++)
    {
        var sum = 0;
        for (var d = -3; d <= 3; d++)
        {
            var i = lx + d;
            if (i >= 0 && i < strip.Width)
            {
                sum += perColumn[i];
            }
        }
        smoothed[lx] = sum;
    }

    // Segment into runs where smoothed >= minPeak with at least a gap wide enough to separate two
    // icons between runs. For each run, compute the pixel-weighted centre of mass — that is the
    // visual centre of the icon, not just the column with the most pixels.
    var minCol = 4;
    var minRunLen = 6;
    var runs = new List<(int Start, int End)>();
    var runStart = -1;

    for (var lx = 0; lx < strip.Width; lx++)
    {
        if (smoothed[lx] >= minCol)
        {
            if (runStart < 0) runStart = lx;
        }
        else if (runStart >= 0)
        {
            if (lx - runStart >= minRunLen)
            {
                runs.Add((runStart, lx - 1));
            }
            runStart = -1;
        }
    }
    if (runStart >= 0 && strip.Width - runStart >= minRunLen)
    {
        runs.Add((runStart, strip.Width - 1));
    }

    var icons = new List<(int ScreenX, int Weight, int Width)>();
    foreach (var (s, e) in runs)
    {
        long wsum = 0;
        long weight = 0;
        for (var lx = s; lx <= e; lx++)
        {
            wsum += (long)lx * smoothed[lx];
            weight += smoothed[lx];
        }
        if (weight == 0) continue;
        var localCentre = (int)(wsum / weight);
        icons.Add((strip.OriginX + localCentre, (int)weight, e - s + 1));
    }

    Console.WriteLine($"  found {icons.Count} glyph cluster(s):");
    for (var i = 0; i < icons.Count; i++)
    {
        var pitch = i > 0 ? $"  pitch from prev = {icons[i].ScreenX - icons[i - 1].ScreenX}" : "";
        Console.WriteLine($"  x={icons[i].ScreenX,5}  weight={icons[i].Weight,4}  width={icons[i].Width,3}{pitch}");
    }

    if (icons.Count >= 2)
    {
        var totalSpan = icons[^1].ScreenX - icons[0].ScreenX;
        var pitchAvg = totalSpan / (double)(icons.Count - 1);
        var centre = (icons[0].ScreenX + icons[^1].ScreenX) / 2;
        Console.WriteLine($"  → bar centre {centre}, average pitch {pitchAvg:F1}");
    }
}

// ASCII visualisation of the strip: each character is a 2x2 sample. `#` means the sample is a
// glyph pixel under the layout's whiteness ramp; `.` means dark background; ' ' means everything
// else. The row header prints screen-X every 20 characters so icon positions can be read off.
static void DumpGlyphMask(PixelGrid strip, SpawnBarLayout layout)
{
    var cellW = 2;
    var cellH = 2;
    var cols = strip.Width / cellW;
    var rows = strip.Height / cellH;

    var header = new System.Text.StringBuilder("     ");
    for (var col = 0; col < cols; col++)
    {
        if (col % 20 == 0)
        {
            var screenX = strip.OriginX + col * cellW;
            header.Append($"{screenX,-20}");
        }
    }
    Console.WriteLine(header.ToString());

    for (var row = 0; row < rows; row++)
    {
        var screenY = strip.OriginY + row * cellH;
        var line = new System.Text.StringBuilder($"{screenY,4} ");

        for (var col = 0; col < cols; col++)
        {
            var glyph = 0;
            var dark = 0;
            for (var dy = 0; dy < cellH; dy++)
            {
                for (var dx = 0; dx < cellW; dx++)
                {
                    var lx = col * cellW + dx;
                    var ly = row * cellH + dy;
                    if (!strip.Contains(lx, ly))
                    {
                        continue;
                    }

                    var (r, g, b) = strip[lx, ly];
                    if (SpawnIconSignature.IsGlyphPixel(r, g, b, layout.GlyphWhiteRampLow, layout.GlyphWhiteRampHigh))
                    {
                        glyph++;
                    }
                    else if (PixelGrid.Luminance(r, g, b) <= layout.DiscMaxLuminance)
                    {
                        dark++;
                    }
                }
            }

            line.Append(glyph >= 2 ? '#' : dark >= 2 ? '.' : ' ');
        }

        Console.WriteLine(line.ToString());
    }
}

// Dumps the captured strip as a PNG so the sampled area can be looked at directly.
static void DumpStripPng(PixelGrid strip, string path)
{
    using var bitmap = new Bitmap(strip.Width, strip.Height);
    for (var y = 0; y < strip.Height; y++)
    {
        for (var x = 0; x < strip.Width; x++)
        {
            var (r, g, b) = strip[x, y];
            bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
        }
    }

    bitmap.Save(path);
}

static void SaveCrop(string sourceFile, string cropDirectory, SpawnIconHit icon, SpawnBarLayout layout)
{
    using var source = new Bitmap(sourceFile);

    // A quarter of the disc again in margin. Cutting exactly on the disc edge leaves the circle
    // touching the frame, which looks cropped wrong even when it is centred to the pixel.
    var size = layout.Diameter * 5 / 4;
    var box = new Rectangle(icon.ScreenX - size / 2, icon.ScreenY - size / 2, size, size);
    box.Intersect(new Rectangle(0, 0, source.Width, source.Height));

    if (box.Width <= 0 || box.Height <= 0)
    {
        return;
    }

    using var crop = source.Clone(box, source.PixelFormat);
    var name = $"{Path.GetFileNameWithoutExtension(sourceFile)}-{icon.Slot + 1}.png";
    crop.Save(Path.Combine(cropDirectory, name));
    Console.WriteLine($"    saved {name}");
}

// One source line per row of the icon, so the table in SpawnIconCatalog stays something a person
// can look at: the shape of the glyph is visible in the shape of the digits.
static string EmitTemplate(string glyph, SpawnIconSignature signature)
{
    var hex = signature.ToHex();
    var rows = Enumerable.Range(0, SpawnIconSignature.Grid)
        .Select(row => hex.Substring(row * SpawnIconSignature.Grid, SpawnIconSignature.Grid))
        .Select((row, index) => $"            \"{row}\"" + (index == SpawnIconSignature.Grid - 1 ? "))," : " +"));

    return $"        new SpawnIconTemplate(\n            \"{glyph}\",\n            [\"\"],\n"
           + "            SpawnIconSignature.FromHex(\n"
           + string.Join('\n', rows);
}

static string Indent(string text) =>
    string.Join('\n', text.TrimEnd('\n').Split('\n').Select(line => "    " + line));
