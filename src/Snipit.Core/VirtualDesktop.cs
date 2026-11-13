namespace Snipit.Core;

/// <summary>One monitor's slice of a capture: read <see cref="Source"/> from the screen, write at (DestX,DestY).</summary>
public readonly record struct Placement(MonitorInfo Monitor, RectPx Source, int DestX, int DestY);

/// <summary>Multi-monitor geometry. Everything is physical pixels; no DPI scaling is ever applied.</summary>
public static class VirtualDesktop
{
    public static RectPx Union(IReadOnlyList<MonitorInfo> monitors)
    {
        RectPx r = RectPx.Empty;
        foreach (var m in monitors) r = r.Union(m.Bounds);
        return r;
    }

    public static MonitorInfo? ByIndex(IReadOnlyList<MonitorInfo> monitors, int index) =>
        monitors.FirstOrDefault(m => m.Index == index);

    /// <summary>Monitor containing the point, else the nearest one.</summary>
    public static MonitorInfo? MonitorAt(IReadOnlyList<MonitorInfo> monitors, int x, int y)
    {
        MonitorInfo? best = null;
        long bestDist = long.MaxValue;
        foreach (var m in monitors)
        {
            if (m.Bounds.Contains(x, y)) return m;
            long dx = Math.Max(Math.Max(m.Bounds.X - x, 0), x - (m.Bounds.Right - 1));
            long dy = Math.Max(Math.Max(m.Bounds.Y - y, 0), y - (m.Bounds.Bottom - 1));
            long d = dx * dx + dy * dy;
            if (d < bestDist) { bestDist = d; best = m; }
        }
        return best;
    }

    /// <summary>Resolve a target to a rectangle; null when the monitor no longer exists.</summary>
    public static RectPx? ResolveTarget(FullScreenTarget target, IReadOnlyList<MonitorInfo> monitors, int cursorX, int cursorY)
    {
        if (monitors.Count == 0) return null;
        return target.Kind switch
        {
            FullScreenKind.AllMonitors => Union(monitors),
            FullScreenKind.CurrentMonitor => MonitorAt(monitors, cursorX, cursorY)?.Bounds,
            _ => ByIndex(monitors, target.MonitorIndex)?.Bounds,
        };
    }

    public static IReadOnlyList<Placement> Placements(RectPx target, IReadOnlyList<MonitorInfo> monitors)
    {
        var list = new List<Placement>();
        foreach (var m in monitors)
        {
            var inter = m.Bounds.Intersect(target);
            if (!inter.IsEmpty) list.Add(new Placement(m, inter, inter.X - target.X, inter.Y - target.Y));
        }
        return list;
    }

    /// <summary>True when monitors do not cover the whole target (L-shaped layouts etc.).</summary>
    public static bool HasGaps(RectPx target, IReadOnlyList<MonitorInfo> monitors)
    {
        long covered = 0;
        foreach (var p in Placements(target, monitors)) covered += p.Source.Area;
        return covered < target.Area;
    }
}
