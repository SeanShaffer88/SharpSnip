namespace Snipit.Core;

/// <summary>WCAG 2.x contrast ratio between two sRGB colours (1.0 to 21.0). Used to pin the theme palette in tests.</summary>
public static class ContrastRatio
{
    public static double Luminance(byte r, byte g, byte b)
    {
        static double Lin(byte c) { double v = c / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
    }

    public static double Between((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
    {
        double la = Luminance(a.R, a.G, a.B), lb = Luminance(b.R, b.G, b.B);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Parses "#RRGGBB" (or "RRGGBB").</summary>
    public static bool TryParse(string hex, out (byte R, byte G, byte B) c)
    {
        c = default;
        string s = hex.Trim().TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v)) return false;
        c = ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}
