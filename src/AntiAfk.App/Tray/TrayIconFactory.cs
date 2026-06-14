using System.Drawing;
using System.Drawing.Drawing2D;

namespace AntiAfk.App.Tray;

public static class TrayIconFactory
{
    public static Icon CreateRunningIcon()
    {
        return CreateStatusIcon(Color.FromArgb(34, 197, 94));
    }

    public static Icon CreateStoppedIcon()
    {
        return CreateStatusIcon(Color.FromArgb(239, 68, 68));
    }

    public static Icon CreateWaitingIcon()
    {
        return CreateStatusIcon(Color.FromArgb(250, 204, 21));
    }

    public static Icon CreateUpdateIcon()
    {
        return CreateStatusIcon(Color.FromArgb(59, 130, 246));
    }

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

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
