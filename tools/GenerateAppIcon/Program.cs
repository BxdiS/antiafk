using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

var output = args.Length > 0
    ? args[0]
    : Path.Combine("src", "AntiAfk.App", "Assets", "app.ico");

Directory.CreateDirectory(Path.GetDirectoryName(output)!);

var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
var images = sizes.Select(Render).ToArray();

try
{
    WriteIco(output, images);
    Console.WriteLine($"Generated {output}");
}
finally
{
    foreach (var image in images)
    {
        image.Dispose();
    }
}

static Bitmap Render(int size)
{
    var bitmap = new Bitmap(size, size);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    graphics.Clear(Color.Transparent);

    using var font = new Font("Segoe UI", size * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel);
    const string letter = "A";
    var letterSize = graphics.MeasureString(letter, font);
    var x = (size - letterSize.Width) / 2f;
    var y = (size - letterSize.Height) / 2f + size * 0.02f;

    using var textBrush = new SolidBrush(Color.FromArgb(0x0D, 0x11, 0x17));
    graphics.DrawString(letter, font, textBrush, x, y);

    return bitmap;
}

static void WriteIco(string path, IReadOnlyList<Bitmap> images)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);

    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)images.Count);

    var offset = 6 + 16 * images.Count;
    var pngChunks = new List<byte[]>(images.Count);

    foreach (var image in images)
    {
        using var pngStream = new MemoryStream();
        image.Save(pngStream, ImageFormat.Png);
        pngChunks.Add(pngStream.ToArray());
    }

    for (var i = 0; i < images.Count; i++)
    {
        var size = images[i].Width;
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(pngChunks[i].Length + 22);
        writer.Write(offset);
        offset += pngChunks[i].Length + 22;
    }

    foreach (var png in pngChunks)
    {
        writer.Write(png.Length + 22);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(png);
    }
}
