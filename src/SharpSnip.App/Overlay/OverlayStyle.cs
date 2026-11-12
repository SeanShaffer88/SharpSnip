using System.Windows.Media;
using SharpSnip.Core;

namespace SharpSnip;

internal sealed record OverlayStyle(Color Accent, byte DimAlpha, double ThicknessPx, bool HighContrast)
{
    public static OverlayStyle From(Preferences p) => new(
        p.HighContrastHighlight ? Color.FromRgb(0xFF, 0xFF, 0x00) : Color.FromRgb(0x2D, 0x9C, 0xFF),
        (byte)Math.Round(p.DimmerOpacity * 255),
        p.HighContrastHighlight ? 3 : 2,
        p.HighContrastHighlight);
}
