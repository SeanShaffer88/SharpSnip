namespace Snipit.Core;

public enum FullScreenKind { AllMonitors, CurrentMonitor, MonitorIndex }

/// <summary>Which part of the virtual desktop a full-screen capture grabs.</summary>
public readonly record struct FullScreenTarget(FullScreenKind Kind, int MonitorIndex = 0)
{
    public static FullScreenTarget All => new(FullScreenKind.AllMonitors);
    public static FullScreenTarget Current => new(FullScreenKind.CurrentMonitor);
    public static FullScreenTarget Monitor(int n) => new(FullScreenKind.MonitorIndex, n);

    /// <summary>
    /// Cycle to the next/previous monitor (wraps). From "all" Tab goes to monitor 1, Shift+Tab to the last.
    /// </summary>
    public FullScreenTarget Cycle(int monitorCount, int cursorMonitorIndex, int step)
    {
        if (monitorCount <= 0) return this;
        int next;
        if (Kind == FullScreenKind.AllMonitors)
        {
            next = step >= 0 ? 1 : monitorCount;
        }
        else
        {
            int cur = Kind == FullScreenKind.MonitorIndex ? MonitorIndex : cursorMonitorIndex;
            cur = Math.Clamp(cur, 1, monitorCount);
            next = (((cur - 1 + step) % monitorCount) + monitorCount) % monitorCount + 1;
        }
        return Monitor(next);
    }

    public string Format() => Kind switch
    {
        FullScreenKind.AllMonitors => "AllMonitors",
        FullScreenKind.MonitorIndex => $"Monitor:{MonitorIndex}",
        _ => "CurrentMonitor",
    };

    public static FullScreenTarget Parse(string? text)
    {
        if (string.Equals(text, "AllMonitors", StringComparison.OrdinalIgnoreCase)) return All;
        if (text is not null && text.StartsWith("Monitor:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text.AsSpan(8), out int n) && n is >= 1 and <= 9)
            return Monitor(n);
        return Current;
    }
}
