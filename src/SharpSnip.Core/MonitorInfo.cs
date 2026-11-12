namespace SharpSnip.Core;

/// <summary>One display. Index is 1-based and stable for the current display configuration.</summary>
public sealed record MonitorInfo(int Index, RectPx Bounds, uint Dpi, bool IsPrimary, string DeviceName)
{
    public double Scale => Dpi / 96.0;
    public int ScalePercent => (int)Math.Round(Dpi * 100.0 / 96.0);

    /// <summary>Chip text, e.g. "1 · 2560×1440 · 150% · PRIMARY".</summary>
    public string Label =>
        $"{Index} · {Bounds.Width}×{Bounds.Height} · {ScalePercent}%{(IsPrimary ? " · PRIMARY" : "")}";
}
