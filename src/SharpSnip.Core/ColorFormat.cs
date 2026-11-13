namespace SharpSnip.Core;

/// <summary>Eyedropper output formats for one RGB colour.</summary>
public static class ColorFormat
{
    public static readonly string[] Names = { "HEX", "RGB", "HTML", "C++", "Delphi" };

    public static string Format(string kind, byte r, byte g, byte b) => kind switch
    {
        "RGB" => $"rgb({r}, {g}, {b})",
        "HTML" => $"#{r:X2}{g:X2}{b:X2}",
        "C++" => $"0x{b:X2}{g:X2}{r:X2}",            // COLORREF 0x00BBGGRR
        "Delphi" => $"$00{b:X2}{g:X2}{r:X2}",         // TColor $00BBGGRR
        _ => $"{r:X2}{g:X2}{b:X2}",                   // HEX
    };

    public static bool TryParseHex(string s, out (byte R, byte G, byte B) c)
    {
        c = default;
        s = s.Trim().TrimStart('#');
        if (s.Length == 3) s = string.Concat(s.Select(ch => new string(ch, 2)));
        if (s.Length != 6 || !int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v)) return false;
        c = ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    /// <summary>Evenly spaced palette from two colours (inclusive), at least 2 steps.</summary>
    public static (byte R, byte G, byte B)[] Gradient((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, int steps)
    {
        steps = Math.Clamp(steps, 2, 64);
        var o = new (byte, byte, byte)[steps];
        for (int i = 0; i < steps; i++)
        {
            double t = i / (double)(steps - 1);
            o[i] = ((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        }
        return o;
    }
}
