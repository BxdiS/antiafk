using AntiAfk.Core.Constants;
using AntiAfk.Core.Models;

namespace AntiAfk.Core.Services;

public static class CoordinateScaler
{
    public static ScaledCoordinates Apply(int winLeft, int winTop, int winWidth, int winHeight, int screenWidth, int screenHeight)
    {
        if (winWidth <= 0 || winHeight <= 0)
        {
            winLeft = 0;
            winTop = 0;
            winWidth = screenWidth;
            winHeight = screenHeight;
        }

        var buttons = GameConstants.BaseButtons
            .Select(point => ScaleToClient(point.X, point.Y, winWidth, winHeight, screenWidth, screenHeight))
            .ToArray();

        var ad1 = ScaleToClient(GameConstants.BaseAdZoneX1, GameConstants.BaseAdZoneY1, winWidth, winHeight, screenWidth, screenHeight);
        var ad2 = ScaleToClient(GameConstants.BaseAdZoneX2, GameConstants.BaseAdZoneY2, winWidth, winHeight, screenWidth, screenHeight);

        var warnBox = ScaleBoxToScreen(GameConstants.BaseWarnBox, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        var warnClick = ScaleToScreen(GameConstants.BaseWarnClick.X, GameConstants.BaseWarnClick.Y, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        var mapPixel = ScaleToScreen(GameConstants.BaseMapPixel.X, GameConstants.BaseMapPixel.Y, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        var hudPixel = ScaleToScreen(GameConstants.BaseHudPixel.X, GameConstants.BaseHudPixel.Y, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        var mpPixel = ScaleToScreen(GameConstants.BaseMpPixel.X, GameConstants.BaseMpPixel.Y, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        var center = ScaleToScreen(GameConstants.BaseCenterX, GameConstants.BaseCenterY, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        var icon = ScaleToScreen(GameConstants.BaseIconX, GameConstants.BaseIconY, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);

        return new ScaledCoordinates
        {
            Buttons = buttons,
            AdZoneX1 = Math.Min(ad1.X, ad2.X),
            AdZoneY1 = Math.Min(ad1.Y, ad2.Y),
            AdZoneX2 = Math.Max(ad1.X, ad2.X),
            AdZoneY2 = Math.Max(ad1.Y, ad2.Y),
            CenterX = center.X,
            CenterY = center.Y,
            IconX = icon.X,
            IconY = icon.Y,
            WarnBoxX1 = warnBox.X1,
            WarnBoxY1 = warnBox.Y1,
            WarnBoxX2 = warnBox.X2,
            WarnBoxY2 = warnBox.Y2,
            WarnClickX = warnClick.X,
            WarnClickY = warnClick.Y,
            MapPixelX = mapPixel.X,
            MapPixelY = mapPixel.Y,
            HudPixelX = hudPixel.X,
            HudPixelY = hudPixel.Y,
            MpPixelX = mpPixel.X,
            MpPixelY = mpPixel.Y
        };
    }

    private static (int X, int Y) ScaleToClient(int baseX, int baseY, int winWidth, int winHeight, int screenWidth, int screenHeight)
    {
        var scaleX = winWidth > 0 ? winWidth / (double)GameConstants.BaseWidth : screenWidth / (double)GameConstants.BaseWidth;
        var scaleY = winHeight > 0 ? winHeight / (double)GameConstants.BaseHeight : screenHeight / (double)GameConstants.BaseHeight;
        return ((int)Math.Round(baseX * scaleX), (int)Math.Round(baseY * scaleY));
    }

    private static (int X, int Y) ScaleToScreen(int baseX, int baseY, int winLeft, int winTop, int winWidth, int winHeight, int screenWidth, int screenHeight)
    {
        var client = ScaleToClient(baseX, baseY, winWidth, winHeight, screenWidth, screenHeight);
        return (winLeft + client.X, winTop + client.Y);
    }

    private static (int X1, int Y1, int X2, int Y2) ScaleBoxToScreen(
        (int X1, int Y1, int X2, int Y2) box,
        int winLeft,
        int winTop,
        int winWidth,
        int winHeight,
        int screenWidth,
        int screenHeight)
    {
        var topLeft = ScaleToScreen(box.X1, box.Y1, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        var bottomRight = ScaleToScreen(box.X2, box.Y2, winLeft, winTop, winWidth, winHeight, screenWidth, screenHeight);
        return (topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }
}
