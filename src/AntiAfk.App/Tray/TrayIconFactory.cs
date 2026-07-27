using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AntiAfk.App.Tray;

public static class TrayIconFactory
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static readonly Icon RunningIcon = CreateStatusIcon(Color.FromArgb(34, 197, 94));
    private static readonly Icon StoppedIcon = CreateStatusIcon(Color.FromArgb(239, 68, 68));
    private static readonly Icon WaitingIcon = CreateStatusIcon(Color.FromArgb(250, 204, 21));
    private static readonly Icon UpdateIcon = CreateStatusIcon(Color.FromArgb(59, 130, 246));

    public static Icon CreateRunningIcon() => RunningIcon;

    public static Icon CreateStoppedIcon() => StoppedIcon;

    public static Icon CreateWaitingIcon() => WaitingIcon;

    public static Icon CreateUpdateIcon() => UpdateIcon;

    private static Icon CreateStatusIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 4, 4, 24, 24);

        using var border = new Pen(Color.FromArgb(30, 30, 30), 2);
        graphics.DrawEllipse(border, 4, 4, 24, 24);

        // GetHicon hands out a GDI icon handle that Icon.FromHandle does not own, so disposing the
        // wrapper does not release it. Clone first (that copy owns its own handle), then destroy
        // the original explicitly.
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
