namespace Snipit.Core;

/// <summary>
/// Multiple-area capture geometry: several rectangles become one image, the bounding box of all of them,
/// with everything outside the rectangles fully transparent. Points and rectangles are virtual-desktop
/// pixels; the frame is addressed through its own rectangle. Overlapping rectangles simply merge.
/// </summary>
public static class AreaMask
{
    public const int MaxAreas = 16;

    /// <summary>The smallest rectangle holding every non-empty area; Empty when there are none.</summary>
    public static RectPx Bounds(IReadOnlyList<RectPx> areas)
    {
        var b = RectPx.Empty;
        foreach (var a in areas)
        {
            if (a.IsEmpty) continue;
            b = b.IsEmpty ? a : b.Union(a);
        }
        return b;
    }

    /// <summary>Sets alpha (and colour) to 0 on every pixel of the frame that lies outside all areas.</summary>
    public static void ApplyAlpha(byte[] bgra, int width, int height, IReadOnlyList<RectPx> areas, RectPx frameRect)
    {
        if (bgra is null) throw new ArgumentNullException(nameof(bgra));
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "Frame size must be positive.");
        if (bgra.Length < (long)width * height * 4) throw new ArgumentException("Buffer smaller than the declared frame.", nameof(bgra));
        var keep = new bool[width * height];
        foreach (var a in areas)
        {
            var hit = a.Intersect(frameRect);
            if (hit.IsEmpty) continue;
            for (int y = hit.Y; y < hit.Bottom; y++)
            {
                int row = (y - frameRect.Y) * width - frameRect.X;
                for (int x = hit.X; x < hit.Right; x++) keep[row + x] = true;
            }
        }
        for (int i = 0; i < keep.Length; i++)
            if (!keep[i]) { int p = i * 4; bgra[p] = bgra[p + 1] = bgra[p + 2] = bgra[p + 3] = 0; }
    }
}
