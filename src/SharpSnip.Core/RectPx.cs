namespace SharpSnip.Core;

/// <summary>Integer rectangle in PHYSICAL screen pixels (virtual-desktop coordinates).</summary>
public readonly record struct RectPx(int X, int Y, int Width, int Height)
{
    public static readonly RectPx Empty = new(0, 0, 0, 0);

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public long Area => IsEmpty ? 0 : (long)Width * Height;

    public bool Contains(int px, int py) => !IsEmpty && px >= X && px < Right && py >= Y && py < Bottom;

    public static RectPx FromLtrb(int l, int t, int r, int b) => new(l, t, r - l, b - t);

    public static RectPx FromCorners(int x0, int y0, int x1, int y1) =>
        FromLtrb(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));

    public RectPx Intersect(RectPx o)
    {
        int l = Math.Max(X, o.X), t = Math.Max(Y, o.Y);
        int r = Math.Min(Right, o.Right), b = Math.Min(Bottom, o.Bottom);
        return r <= l || b <= t ? Empty : FromLtrb(l, t, r, b);
    }

    public bool IntersectsWith(RectPx o) => !Intersect(o).IsEmpty;

    public RectPx Union(RectPx o)
    {
        if (IsEmpty) return o;
        if (o.IsEmpty) return this;
        return FromLtrb(Math.Min(X, o.X), Math.Min(Y, o.Y), Math.Max(Right, o.Right), Math.Max(Bottom, o.Bottom));
    }

    public RectPx Inflate(int d) => new(X - d, Y - d, Width + 2 * d, Height + 2 * d);

    public RectPx Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    public override string ToString() => $"{X},{Y} {Width}x{Height}";
}
