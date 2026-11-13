namespace SharpSnip.Core;

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

    public static double BarWidth(int buttonCount) =>
        buttonCount <= 0 ? 0 : PadX + buttonCount * ButtonWidth + SeparatorWidth + CloseWidth + PadX;

    public static double ButtonLeft(int index) => PadX + index * ButtonWidth;

    public static double CloseLeft(int buttonCount) => PadX + buttonCount * ButtonWidth + SeparatorWidth;

    /// <summary>What a bar-local point hits: a button index, the close button, or nothing.</summary>
    public readonly record struct Hit(int Index, bool IsClose);

    /// <summary>Returns null when the point is on padding, the separator, or outside the bar.</summary>
    public static Hit? HitTest(double x, double y, int buttonCount)
    {
        if (buttonCount <= 0 || y < 0 || y >= BarHeight || x < PadX) return null;
        double buttonsEnd = PadX + buttonCount * ButtonWidth;
        if (x < buttonsEnd) return new Hit((int)((x - PadX) / ButtonWidth), false);
        if (x < CloseLeft(buttonCount)) return null; // separator
        if (x < CloseLeft(buttonCount) + CloseWidth) return new Hit(-1, true);
        return null;
    }
}
