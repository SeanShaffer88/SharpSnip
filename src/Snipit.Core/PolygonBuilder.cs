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
    private (int X, int Y, long Tick)? _lastClick;

    public enum Result { Added, Closed, Ignored }

    public IReadOnlyList<(int X, int Y)> Points => _points;
    public int Count => _points.Count;

    /// <summary>True when the outline spans the minimum size and has noncollinear vertices.</summary>
    public bool CanClose
    {
        get
        {
            if (_points.Count < MinVertices) return false;
            var first = _points[0];
            var second = _points[1];
            long dx = (long)second.X - first.X, dy = (long)second.Y - first.Y;
            int minX = first.X, maxX = first.X, minY = first.Y, maxY = first.Y;
            bool hasBend = false;
            foreach (var (x, y) in _points)
            {
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                hasBend |= (Int128)dx * ((long)y - first.Y) != (Int128)dy * ((long)x - first.X);
            }
            long width = (long)maxX - minX + 1, height = (long)maxY - minY + 1;
            return hasBend && width is >= MinExtent and <= int.MaxValue && height is >= MinExtent and <= int.MaxValue;
        }
    }

    public Result Click(int x, int y, long tick, uint doubleClickTime)
    {
        var previous = _lastClick;
        _lastClick = (x, y, tick);
        if (_points.Count >= MinVertices)
        {
            var first = _points[0];
            if (Near(first, x, y, CloseRadius)) return CanClose ? Result.Closed : Result.Ignored;
            if (Near(_points[^1], x, y, DuplicateRadius))
            {
                bool doubleClick = previous is { } last && tick >= last.Tick &&
                    (Int128)tick - last.Tick <= doubleClickTime &&
                    Near(_points[^1], last.X, last.Y, DuplicateRadius) &&
                    Near((last.X, last.Y), x, y, DuplicateRadius);
                return doubleClick && CanClose ? Result.Closed : Result.Ignored;
            }
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
        _lastClick = null;
        return true;
    }

    public void Clear()
    {
        _points.Clear();
        _lastClick = null;
    }

    private static bool Near((int X, int Y) p, int x, int y, int r) =>
        Math.Abs((long)p.X - x) <= r && Math.Abs((long)p.Y - y) <= r;
}
