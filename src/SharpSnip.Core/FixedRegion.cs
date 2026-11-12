namespace SharpSnip.Core;

/// <summary>Fixed-size region: a W×H rectangle centred on the cursor and kept inside the desktop.</summary>
public static class FixedRegion
{
    public const int MinSize = 16, MaxSize = 16384;

    public static int ClampSize(int v) => Math.Clamp(v, MinSize, MaxSize);

    public static RectPx Place(int width, int height, int cx, int cy, RectPx bounds)
    {
        int w = Math.Min(ClampSize(width), Math.Max(1, bounds.Width));
        int h = Math.Min(ClampSize(height), Math.Max(1, bounds.Height));
        int x = Math.Clamp(cx - w / 2, bounds.X, bounds.Right - w);
        int y = Math.Clamp(cy - h / 2, bounds.Y, bounds.Bottom - h);
        return new RectPx(x, y, w, h);
    }
}
