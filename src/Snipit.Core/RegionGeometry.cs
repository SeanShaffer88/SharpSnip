namespace Snipit.Core;

public enum RegionHandle { None, Body, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

/// <summary>Pure geometry for the rubber-band region picker.</summary>
public static class RegionGeometry
{
    public static RegionHandle HitTest(RectPx r, int x, int y, int tol)
    {
        if (r.IsEmpty) return RegionHandle.None;
        bool inX = x >= r.X - tol && x <= r.Right + tol;
        bool inY = y >= r.Y - tol && y <= r.Bottom + tol;
        if (!inX || !inY) return RegionHandle.None;

        bool l = Math.Abs(x - r.X) <= tol, rt = Math.Abs(x - r.Right) <= tol;
        bool t = Math.Abs(y - r.Y) <= tol, b = Math.Abs(y - r.Bottom) <= tol;

        if (l && t) return RegionHandle.TopLeft;
        if (rt && t) return RegionHandle.TopRight;
        if (l && b) return RegionHandle.BottomLeft;
        if (rt && b) return RegionHandle.BottomRight;
        if (t) return RegionHandle.Top;
        if (b) return RegionHandle.Bottom;
        if (l) return RegionHandle.Left;
        if (rt) return RegionHandle.Right;
        return r.Contains(x, y) ? RegionHandle.Body : RegionHandle.None;
    }

    /// <summary>Drag a handle to (x,y). Crossing the opposite edge flips the rectangle.</summary>
    public static RectPx Resize(RectPx r, RegionHandle h, int x, int y)
    {
        int l = r.X, t = r.Y, rt = r.Right, b = r.Bottom;
        switch (h)
        {
            case RegionHandle.TopLeft: l = x; t = y; break;
            case RegionHandle.Top: t = y; break;
            case RegionHandle.TopRight: rt = x; t = y; break;
            case RegionHandle.Right: rt = x; break;
            case RegionHandle.BottomRight: rt = x; b = y; break;
            case RegionHandle.Bottom: b = y; break;
            case RegionHandle.BottomLeft: l = x; b = y; break;
            case RegionHandle.Left: l = x; break;
            default: return r;
        }
        return RectPx.FromCorners(l, t, rt, b);
    }

    /// <summary>Move a rectangle, keeping it inside <paramref name="bounds"/>.</summary>
    public static RectPx Move(RectPx r, int dx, int dy, RectPx bounds)
    {
        int nx = Math.Clamp(r.X + dx, bounds.X, Math.Max(bounds.X, bounds.Right - r.Width));
        int ny = Math.Clamp(r.Y + dy, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - r.Height));
        return new RectPx(nx, ny, r.Width, r.Height);
    }

    /// <summary>Snap a coordinate to the nearest candidate within tolerance, else return it unchanged.</summary>
    public static int Snap(int value, IEnumerable<int> candidates, int tol)
    {
        int best = value, bestDist = tol + 1;
        foreach (int c in candidates)
        {
            int d = Math.Abs(c - value);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }
}
