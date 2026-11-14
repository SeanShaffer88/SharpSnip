namespace Snipit.Core;

/// <summary>
/// Freehand capture geometry: even-odd point-in-polygon over the drawn outline,
/// the outline's bounds, and applying the mask to a BGRA frame (outside pixels
/// become fully transparent). Points are virtual-desktop pixels; the frame is
/// addressed through its own rectangle.
/// </summary>
public static class PolygonMask
{
    public static RectPx Bounds(IReadOnlyList<(int X, int Y)> polygon)
    {
        if (polygon.Count == 0) return RectPx.Empty;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var (x, y) in polygon)
        {
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        return new RectPx(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>Even-odd ray cast: true when the point lies inside the (implicitly closed) polygon.</summary>
    public static bool Contains(IReadOnlyList<(int X, int Y)> polygon, int x, int y)
    {
        bool inside = false;
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var (xi, yi) = polygon[i];
            var (xj, yj) = polygon[j];
            if ((yi > y) != (yj > y) && x < ((double)xj - xi) * ((double)y - yi) / ((double)yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Clears every BGRA channel outside the polygon.</summary>
    public static void ApplyAlpha(byte[] bgra, int width, int height, IReadOnlyList<(int X, int Y)> polygon, RectPx frameRect)
    {
        if (bgra.Length < (long)width * height * 4) throw new ArgumentException("Buffer smaller than the declared frame.", nameof(bgra));
        for (int row = 0; row < height; row++)
        {
            int vy = frameRect.Y + row;
            for (int col = 0; col < width; col++)
            {
                if (!Contains(polygon, frameRect.X + col, vy))
                {
                    int i = (row * width + col) * 4;
                    bgra[i] = bgra[i + 1] = bgra[i + 2] = bgra[i + 3] = 0;
                }
            }
        }
    }
}

/// <summary>The loupe's colour readout line.</summary>
public static class ColorReadout
{
    public static string Format(byte r, byte g, byte b, int x, int y) =>
        $"#{r:X2}{g:X2}{b:X2} · ({r}, {g}, {b}) · {x}, {y}";
}
