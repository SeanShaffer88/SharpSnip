using System.Windows.Media;
using SharpSnip.Core;

namespace SharpSnip;

internal sealed record OverlayStyle(Color Accent, byte DimAlpha, double ThicknessPx, bool HighContrast)
{
    /// <summary>The high-contrast look is used when ticked in Settings or when Windows High Contrast is on.</summary>
    public static OverlayStyle From(Preferences p) => From(p, System.Windows.SystemParameters.HighContrast);

    public static OverlayStyle From(Preferences p, bool systemHighContrast)
    {
        bool hc = p.HighContrastHighlight || systemHighContrast;
        return new(
            hc ? Color.FromRgb(0xFF, 0xFF, 0x00) : Color.FromRgb(0x2D, 0x9C, 0xFF),
            (byte)Math.Round(p.DimmerOpacity * 255),
            hc ? 3 : 2,
            hc);
    }
}
