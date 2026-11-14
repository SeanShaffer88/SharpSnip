namespace Snipit.Core;

/// <summary>Right-angle route for the editor's Connector tool: one bend, along the longer drag axis first.</summary>
public static class ConnectorRoute
{
    /// <summary>Below this many pixels of travel on either axis the route is a straight line (no visible bend).</summary>
    public const double MinBend = 6;

    /// <summary>Points from start to end: 2 when straight, otherwise 3 (start, corner, end).</summary>
    public static (double X, double Y)[] Elbow(double x1, double y1, double x2, double y2)
    {
        double dx = Math.Abs(x2 - x1), dy = Math.Abs(y2 - y1);
        if (dx < MinBend || dy < MinBend) return new[] { (x1, y1), (x2, y2) };
        return dx >= dy
            ? new[] { (x1, y1), (x2, y1), (x2, y2) }
            : new[] { (x1, y1), (x1, y2), (x2, y2) };
    }
}
