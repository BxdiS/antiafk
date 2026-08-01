// Turns screenshots of the spawn screen into the icon table in SpawnIconCatalog.
//
// The app has to recognise spawn icons on the first run, with no help from the user, so the
// reference data cannot be learned at runtime - it has to ship inside the exe. This is where it
// comes from: run it over a screenshot, check the glyphs it prints look like the icons on screen,
// and paste the lines it emits into SpawnIconCatalog.
//
//   dotnet run --project tools/GenerateSpawnIcons -- <screenshot.png|folder> [more...] [--crops <dir>]
//
// Also prints the per-position numbers the detector worked from, which is what to look at when a
// screenshot does not detect: it says whether a slot was missed for want of a disc or a glyph.

using System.Drawing;
using AntiAfk.Core.Models;
using AntiAfk.Core.Vision;
using AntiAfk.Infrastructure.Services;

var inputs = new List<string>();
string? cropDirectory = null;

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

    inputs.Add(args[i]);
}

if (inputs.Count == 0)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/GenerateSpawnIcons -- <screenshot.png|folder> [...] [--crops <dir>]");
    return 2;
}

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
    Report(file, cropDirectory);
}

return 0;

static void Report(string file, string? cropDirectory)
{
    using var bitmap = new Bitmap(file);

    // A screenshot is the game window, so it stands in for one at (0,0) of its own size. That is
    // also what makes a 1440p screenshot work here without a second set of measurements.
    var layout = SpawnBarLayout.ForWindow(0, 0, bitmap.Width, bitmap.Height);
    var grid = ScreenCaptureService.ToPixelGrid(bitmap, 0, 0);

    Console.WriteLine(
        $"{bitmap.Width}x{bitmap.Height}: expecting the bar centred on x={layout.CenterX}, row y={layout.RowY}, " +
        $"pitch {layout.Pitch}, disc {layout.Diameter}, glyph box {layout.GlyphBox}");

    var strip = Crop(grid, layout);
    var reading = SpawnBarDetector.Detect(strip, layout);

    if (reading is null)
    {
        Console.WriteLine("No spawn bar detected. Positions on the expected row, best first:");
        DumpRow(strip, layout, layout.RowY);
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
