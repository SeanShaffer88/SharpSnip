namespace SharpSnip.Core;

/// <summary>One recognised word and its bounding box in image pixels, as reported by an OCR engine.</summary>
public readonly record struct OcrWord(string Text, RectPx Rect);

/// <summary>
/// Turns raw OCR words into reading-order plain text: words are grouped into
/// lines by vertical proximity (a word joins the current line when its centre
/// is within half the median word height of the line's first word), lines are
/// ordered top to bottom, words within a line left to right. The OS engine
/// (Windows.Media.Ocr) does the recognition; this layer owns the assembly,
/// so the behaviour is testable without it.
/// </summary>
public static class OcrText
{
    public static string Assemble(IReadOnlyList<OcrWord> words) =>
        string.Join("\n", GroupLines(words).Select(line =>
            string.Join(" ", line.OrderBy(w => w.Rect.X).Select(w => w.Text.Trim()))));

    /// <summary>
    /// The line grouping <see cref="Assemble"/> is built on, shared with the
    /// redactor: non-blank words grouped into lines (top to bottom), each
    /// line's words in reading order (left to right).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<OcrWord>> GroupLines(IReadOnlyList<OcrWord> words)
    {
        var clean = words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
        if (clean.Count == 0) return Array.Empty<IReadOnlyList<OcrWord>>();

        var heights = clean.Select(w => w.Rect.Height).OrderBy(h => h).ToList();
        int median = heights[heights.Count / 2];
        double tolerance = Math.Max(2, median / 2.0);

        var lines = new List<List<OcrWord>>();
        foreach (var w in clean.OrderBy(w => CenterY(w)).ThenBy(w => w.Rect.X))
        {
            if (lines.Count > 0 && Math.Abs(CenterY(w) - CenterY(lines[^1][0])) <= tolerance)
                lines[^1].Add(w);
            else
                lines.Add(new List<OcrWord> { w });
        }
        return lines.Select(l => (IReadOnlyList<OcrWord>)l.OrderBy(w => w.Rect.X).ToList()).ToList();
    }

    private static double CenterY(OcrWord w) => w.Rect.Y + w.Rect.Height / 2.0;
}
