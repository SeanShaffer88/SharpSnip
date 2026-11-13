namespace SharpSnip.Core;

/// <summary>Resize dialog math: dimensions clamp to a sane range and the aspect-preserving partner dimension rounds.</summary>
public static class ResizePlan
{
    public const int MinDim = 16, MaxDim = 32767;

    public static int ClampDim(int v) => Math.Clamp(v, MinDim, MaxDim);

    public static (int W, int H) FromWidth(int origW, int origH, int newW)
    {
        newW = ClampDim(newW);
        int h = (int)Math.Round((double)origH * newW / Math.Max(1, origW));
        return (newW, ClampDim(h));
    }

    public static (int W, int H) FromHeight(int origW, int origH, int newH)
    {
        newH = ClampDim(newH);
        int w = (int)Math.Round((double)origW * newH / Math.Max(1, origH));
        return (ClampDim(w), newH);
    }
}

/// <summary>Watermark placement geometry (the rendering itself is the editor's).</summary>
public static class WatermarkPlan
{
    /// <summary>Bottom-right origin for a single watermark of the given size.</summary>
    public static (int X, int Y) CornerOrigin(int imageW, int imageH, int textW, int textH, int margin = 12) =>
        (Math.Max(0, imageW - textW - margin), Math.Max(0, imageH - textH - margin));

    /// <summary>Grid origins for a tiled watermark, covering the image with the given steps.</summary>
    public static IReadOnlyList<(int X, int Y)> TileOrigins(int imageW, int imageH, int stepX, int stepY)
    {
        var list = new List<(int, int)>();
        if (stepX <= 0 || stepY <= 0) return list;
        for (int y = 0; y < imageH; y += stepY)
            for (int x = 0; x < imageW; x += stepX)
                list.Add((x, y));
        return list;
    }
}

/// <summary>Auto-apply effect choices for newly opened captures.</summary>
public static class AutoEffects
{
    public static readonly string[] All = { "None", "Border", "Grayscale" };
    public const string Default = "None";
    public static string Sanitize(string? name) =>
        name is not null && Array.IndexOf(All, name) >= 0 ? name : Default;
}

/// <summary>
/// Find &amp; replace planning over OCR words: a query matches a run of whole
/// words (case-insensitive) or a substring inside a single word. Returns the
/// word runs to cover, with the union rectangle of each.
/// </summary>
public static class TextReplace
{
    public sealed record Match(int StartWord, int WordCount, RectPx Union);

    public static IReadOnlyList<Match> Find(IReadOnlyList<OcrWord> words, string query)
    {
        var result = new List<Match>();
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || words.Count == 0) return result;
        int i = 0;
        while (i < words.Count)
        {
            if (tokens.Length == 1 && words[i].Text.Contains(tokens[0], StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new Match(i, 1, words[i].Rect));
                i++;
                continue;
            }
            if (i + tokens.Length <= words.Count)
            {
                bool ok = true;
                for (int t = 0; t < tokens.Length; t++)
                    if (!string.Equals(words[i + t].Text, tokens[t], StringComparison.OrdinalIgnoreCase)) { ok = false; break; }
                if (ok)
                {
                    var u = words[i].Rect;
                    for (int t = 1; t < tokens.Length; t++) u = Union(u, words[i + t].Rect);
                    result.Add(new Match(i, tokens.Length, u));
                    i += tokens.Length;
                    continue;
                }
            }
            i++;
        }
        return result;
    }

    private static RectPx Union(RectPx a, RectPx b)
    {
        int x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        int r = Math.Max(a.Right, b.Right), bot = Math.Max(a.Bottom, b.Bottom);
        return new RectPx(x, y, r - x, bot - y);
    }
}
