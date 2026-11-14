using System.Globalization;

namespace Snipit.Core;

/// <summary>WCAG 2.x contrast ratio between two sRGB colours given as #RRGGBB.</summary>
public static class WcagContrast
{
    public const double AaNormalText = 4.5;
    public const double AaLargeOrUi = 3.0;

    public static double Ratio(string foregroundHex, string backgroundHex)
    {
        double a = Luminance(foregroundHex), b = Luminance(backgroundHex);
        double hi = Math.Max(a, b), lo = Math.Min(a, b);
        return (hi + 0.05) / (lo + 0.05);
    }

    public static double Luminance(string hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#') throw new FormatException("Expected #RRGGBB.");
        double Channel(int start)
        {
            double c = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}
