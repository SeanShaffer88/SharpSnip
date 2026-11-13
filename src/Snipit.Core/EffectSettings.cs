namespace Snipit.Core;

/// <summary>
/// Turns the saved effect settings (0 = automatic) into the numbers the editor's Frame, Shadow,
/// Torn and Wave buttons use, for a given image size. Pure logic so every default and clamp is tested.
/// </summary>
public static class EffectSettings
{
    public const string DefaultFrameColor = "1B1B1D";
    public const int DefaultShadowOpacity = 45;

    /// <summary>Border width in pixels. 0 = automatic (about 1/60 of the shorter side, at least 2).</summary>
    public static int FrameThickness(int setting, int w, int h) =>
        setting > 0 ? Math.Clamp(setting, 1, ImageFrames.MaxThickness) : Math.Clamp(Math.Max(2, Math.Min(w, h) / 60), 1, ImageFrames.MaxThickness);

    /// <summary>Shadow blur radius. 0 = automatic (about 1/40 of the shorter side, 4 to 24).</summary>
    public static int ShadowBlur(int setting, int w, int h) =>
        setting > 0 ? Math.Clamp(setting, 1, ImageFrames.MaxBlur) : Math.Clamp(Math.Min(w, h) / 40, 4, 24);

    /// <summary>Shadow offset in pixels, down and to the right; follows the blur so the shadow looks natural.</summary>
    public static int ShadowOffset(int blur) => Math.Clamp(blur, 0, ImageFrames.MaxBlur) / 2 + 2;

    /// <summary>Shadow opacity 0..1 from a percent setting. NaN-safe by construction (int in).</summary>
    public static double ShadowOpacity(int percent) => Math.Clamp(percent, 0, 100) / 100.0;

    /// <summary>Torn and wave cut depth. 0 = automatic (about 1/40 of the shorter side, at least 4). Never beyond a quarter of the short side.</summary>
    public static int EdgeDepth(int setting, int w, int h) =>
        Math.Min(setting > 0 ? Math.Max(1, setting) : Math.Max(4, Math.Min(w, h) / 40), EdgeEffects.MaxDepthFor(w, h));

    /// <summary>A valid six-digit hex colour, upper-case, no hash; anything unreadable becomes the default.</summary>
    public static string SanitizeColor(string? s)
    {
        if (s is not null && ColorFormat.TryParseHex(s, out var c)) return $"{c.R:X2}{c.G:X2}{c.B:X2}";
        return DefaultFrameColor;
    }

    public static (byte R, byte G, byte B) FrameRgb(string? s)
    {
        ColorFormat.TryParseHex(SanitizeColor(s), out var c);
        return c;
    }
}
