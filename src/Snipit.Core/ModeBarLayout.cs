namespace Snipit.Core;

/// <summary>
/// Geometry of the mode bar — the Snipping-Tool-style tab that hangs from the
/// top-centre of the screen — in device-independent units relative to the
/// bar's top-left corner. Mode buttons sit left to right, then a separator,
/// then the close button. Pure layout math so hit-testing is testable
/// without Windows; the app layer converts physical pixels to these units
/// with the target monitor's scale.
/// </summary>
public static class ModeBarLayout
{
    public const double ButtonWidth = 64;
    public const double BarHeight = 72;
    public const double CloseWidth = 36;
    public const double SeparatorWidth = 9;
    public const double PadX = 6;
    public const double CellHeight = 60;
    /// <summary>Drag handle at the left end of the bar.</summary>
    public const double GripWidth = 16;

    public static double BarWidth(int buttonCount) =>
        buttonCount <= 0 ? 0 : PadX + GripWidth + buttonCount * ButtonWidth + SeparatorWidth + CloseWidth + PadX;

    public static double ButtonLeft(int index) => PadX + GripWidth + index * ButtonWidth;

    public static double CloseLeft(int buttonCount) => PadX + GripWidth + buttonCount * ButtonWidth + SeparatorWidth;

    /// <summary>What a bar-local point hits: a button index, the close button, the drag grip, or nothing.</summary>
    public readonly record struct Hit(int Index, bool IsClose, bool IsGrip = false);

    /// <summary>Returns null when the point is on padding, the separator, or outside the bar.</summary>
    public static Hit? HitTest(double x, double y, int buttonCount)
    {
        if (buttonCount <= 0 || y < 0 || y >= BarHeight || x < PadX) return null;
        if (x < PadX + GripWidth) return new Hit(-1, false, true);
        double buttonsEnd = ButtonLeft(buttonCount);
        if (x < buttonsEnd) return new Hit((int)((x - ButtonLeft(0)) / ButtonWidth), false);
        if (x < CloseLeft(buttonCount)) return null; // separator
        if (x < CloseLeft(buttonCount) + CloseWidth) return new Hit(-1, true);
        return null;
    }

    /// <summary>Where the bar's centre sits along the top edge: a fraction of the monitor width (0.5 = centred, the default).</summary>
    public const double DefaultCenter = 0.5;

    /// <summary>Clamps a stored centre fraction; NaN and out-of-range values fall back to the default or the nearest edge.</summary>
    public static double SanitizeCenter(double f) => double.IsNaN(f) || double.IsInfinity(f) ? DefaultCenter : Math.Clamp(f, 0, 1);

    /// <summary>Bar-left in device-independent units for a centre fraction, kept fully on the monitor. A bar wider than the monitor pins to 0.</summary>
    public static double Left(double centerFraction, double monitorWidth, double barWidth)
    {
        double max = Math.Max(0, monitorWidth - barWidth);
        return Math.Clamp(SanitizeCenter(centerFraction) * monitorWidth - barWidth / 2, 0, max);
    }

    /// <summary>Inverse of <see cref="Left"/>: the centre fraction to store for a dragged bar-left. Always in 0..1.</summary>
    public static double CenterFor(double left, double monitorWidth, double barWidth) =>
        monitorWidth <= 0 ? DefaultCenter : SanitizeCenter((left + barWidth / 2) / monitorWidth);
}
