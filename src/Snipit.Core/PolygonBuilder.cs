namespace Snipit.Core;

/// <summary>
/// Click-by-click polygon capture: collects vertices, closes on a click near the
/// first vertex or a double-click on the last, and supports undoing the last vertex.
/// Points are virtual-desktop pixels.
/// </summary>
public sealed class PolygonBuilder
{
    public const int CloseRadius = 8;
    public const int DuplicateRadius = 2;
    public const int MinVertices = 3;
    public const int MinExtent = 8;

    private readonly List<(int X, int Y)> _points = new();

    public enum Result { Added, Closed, Ignored }

    public IReadOnlyList<(int X, int Y)> Points => _points;
    public int Count => _points.Count;

    /// <summary>True when there are enough vertices and the outline is big enough to capture.</summary>
    public bool CanClose
    {
        get
        {
            if (_points.Count < MinVertices) return false;
            var b = PolygonMask.Bounds(_points);
            return b.Width >= MinExtent && b.Height >= MinExtent;
        }
    }

    public Result Click(int x, int y)
    {
        if (_points.Count >= MinVertices)
        {
            var first = _points[0];
            if (Near(first, x, y, CloseRadius)) return CanClose ? Result.Closed : Result.Ignored;
            // double-click: a second click on the last vertex finishes the outline
            if (Near(_points[^1], x, y, DuplicateRadius)) return CanClose ? Result.Closed : Result.Ignored;
        }
        if (_points.Count > 0 && Near(_points[^1], x, y, DuplicateRadius)) return Result.Ignored;
        _points.Add((x, y));
        return Result.Added;
    }

    /// <summary>Removes the last vertex; false when there was none.</summary>
    public bool Undo()
    {
        if (_points.Count == 0) return false;
        _points.RemoveAt(_points.Count - 1);
        return true;
    }

    public void Clear() => _points.Clear();

    private static bool Near((int X, int Y) p, int x, int y, int r) => Math.Abs(p.X - x) <= r && Math.Abs(p.Y - y) <= r;
}
