namespace Snipit.Core;

/// <summary>
/// Resolves a desktop rectangle to the monitor it mostly sits on and the
/// monitor-relative crop of it — what a region recording needs from a picked
/// or remembered rectangle. Pure math over the monitor list so the choice is
/// testable: the monitor with the largest overlap wins; a rectangle that
/// touches no monitor resolves to null.
/// </summary>
public static class RegionCrop
{
    public static (int MonitorIndex, RectPx Crop)? Resolve(RectPx rect, IReadOnlyList<MonitorInfo> monitors)
    {
        if (rect.IsEmpty) return null;
        int best = -1; RectPx bestCrop = RectPx.Empty; long bestArea = 0;
        for (int i = 0; i < monitors.Count; i++)
        {
            var hit = rect.Intersect(monitors[i].Bounds);
            if (!hit.IsEmpty && hit.Area > bestArea)
            {
                bestArea = hit.Area; best = i;
                bestCrop = new RectPx(hit.X - monitors[i].Bounds.X, hit.Y - monitors[i].Bounds.Y, hit.Width, hit.Height);
            }
        }
        return best < 0 ? null : (best, bestCrop);
    }
}
