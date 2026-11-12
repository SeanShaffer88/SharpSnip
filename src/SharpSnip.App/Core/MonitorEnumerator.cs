using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

/// <summary>Cached display configuration (§1.5). Refreshed on WM_DISPLAYCHANGE.</summary>
internal static class MonitorEnumerator
{
    private static volatile IReadOnlyList<MonitorInfo> _current = Array.Empty<MonitorInfo>();

    public static IReadOnlyList<MonitorInfo> Current
    {
        get
        {
            var c = _current;
            return c.Count > 0 ? c : Refresh();
        }
    }

    public static IReadOnlyList<MonitorInfo> Refresh()
    {
        var list = new List<MonitorInfo>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
        {
            var mi = new NativeMethods.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            if (!NativeMethods.GetMonitorInfo(hMon, ref mi)) return true;
            uint dpi = 96;
            if (NativeMethods.GetDpiForMonitor(hMon, 0, out uint dx, out _) == 0 && dx > 0) dpi = dx;
            var b = RectPx.FromLtrb(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
            list.Add(new MonitorInfo(list.Count + 1, b, dpi, (mi.dwFlags & 1) != 0, mi.szDevice ?? ""));
            return true;
        }, IntPtr.Zero);

        // Disabled/clamshell displays are not enumerated by EnumDisplayMonitors, so nothing to filter further.
        _current = list;
        Log.Info($"Monitors: {string.Join(" | ", list.Select(m => m.Label))}");
        return list;
    }

    public static (int X, int Y) CursorPos()
    {
        NativeMethods.GetCursorPos(out var p);
        return (p.X, p.Y);
    }
}
