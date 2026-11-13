using SharpSnip.Core;

namespace SharpSnip;

/// <summary>One interval (time-lapse) frame: capture a fixed target and write it to the run folder. No clipboard.</summary>
internal static class IntervalCapture
{
    public static string CaptureOnce(Preferences prefs, FullScreenTarget target, string folder, int index)
    {
        var cap = CaptureBitmap(prefs, target);
        return Exporter.Save(cap.Image, prefs, Path.Combine(folder, IntervalPlan.FrameName(index, prefs.Extension)));
    }

    /// <summary>Capture one frame without saving it (the animated-GIF sink writes frames itself).</summary>
    public static CaptureResult CaptureBitmap(Preferences prefs, FullScreenTarget target)
    {
        var monitors = MonitorEnumerator.Refresh();
        RectPx rect = VirtualDesktop.ResolveTarget(target, monitors, 0, 0)
                      ?? (monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0]).Bounds; // monitor vanished → primary
        return CaptureEngine.Capture(rect, monitors, prefs.MatteArgb);
    }

    /// <summary>"Current monitor" is fixed once, when the run starts.</summary>
    public static FullScreenTarget ResolveStartTarget(FullScreenTarget t)
    {
        if (t.Kind != FullScreenKind.CurrentMonitor) return t;
        var (cx, cy) = MonitorEnumerator.CursorPos();
        var m = VirtualDesktop.MonitorAt(MonitorEnumerator.Current, cx, cy);
        return m is null ? t : FullScreenTarget.Monitor(m.Index);
    }
}
