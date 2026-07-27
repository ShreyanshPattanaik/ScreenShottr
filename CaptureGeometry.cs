using System.Drawing;

namespace ShottrClone;

internal static class CaptureGeometry
{
    public static Rectangle FromScreenPoints(System.Windows.Point first, System.Windows.Point second)
    {
        var left = (int)Math.Floor(Math.Min(first.X, second.X));
        var top = (int)Math.Floor(Math.Min(first.Y, second.Y));
        var right = (int)Math.Ceiling(Math.Max(first.X, second.X));
        var bottom = (int)Math.Ceiling(Math.Max(first.Y, second.Y));
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public static Rectangle ProjectCrop(
        double x,
        double y,
        double width,
        double height,
        double canvasWidth,
        double canvasHeight,
        int bitmapWidth,
        int bitmapHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || bitmapWidth <= 0 || bitmapHeight <= 0)
            return Rectangle.Empty;

        var scaleX = bitmapWidth / canvasWidth;
        var scaleY = bitmapHeight / canvasHeight;
        var pixelX = Math.Clamp((int)Math.Floor(x * scaleX), 0, bitmapWidth - 1);
        var pixelY = Math.Clamp((int)Math.Floor(y * scaleY), 0, bitmapHeight - 1);
        var pixelWidth = Math.Min((int)Math.Ceiling(width * scaleX), bitmapWidth - pixelX);
        var pixelHeight = Math.Min((int)Math.Ceiling(height * scaleY), bitmapHeight - pixelY);

        return pixelWidth > 0 && pixelHeight > 0
            ? new Rectangle(pixelX, pixelY, pixelWidth, pixelHeight)
            : Rectangle.Empty;
    }
}
