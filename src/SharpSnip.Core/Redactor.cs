using System.Text;
using System.Text.RegularExpressions;

namespace SharpSnip.Core;

/// <summary>One sensitive span found in recognised text: the box to cover, and what kind of thing it is.</summary>
public sealed record RedactionHit(RectPx Rect, string Kind);

/// <summary>
/// Smart Redact (roadmap): finds sensitive spans in OCR'd text — emails, URLs,
/// payment cards (Luhn-validated), IPv4 addresses, phone numbers, and long
/// mixed letter+digit tokens shaped like API keys — and maps each span back to
/// the union of the word boxes it touches, so the editor can cover exactly
/// those words. Detectors run per assembled line in a fixed priority order;
/// a span already claimed by a stronger detector is never double-claimed.
/// Everything here is deterministic and offline; the OCR itself is the OS's.
/// </summary>
public static class Redactor
{
    private static readonly (string Kind, Regex Rx, Func<string, bool>? Valid)[] Detectors =
    {
        ("url",   new Regex(@"(?:https?://|www\.)[^\s]+", RegexOptions.Compiled), null),
        // One optional space either side of '@': OCR engines sometimes split the address into two words there.
        ("email", new Regex(@"[A-Za-z0-9._%+-]+ ?@ ?[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled), null),
        ("card",  new Regex(@"\b(?:\d[ -]?){12,18}\d\b", RegexOptions.Compiled), IsCardNumber),
        ("ipv4",  new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled), IsIPv4),
        ("phone", new Regex(@"\(\d{3}\)\s?\d{3}-\d{4}|\b\d{3}[-.]\d{3}[-.]\d{4}\b", RegexOptions.Compiled), null),
        ("token", new Regex(@"\b[A-Za-z0-9_-]{24,}\b", RegexOptions.Compiled), IsTokenShape),
    };

    public static IReadOnlyList<RedactionHit> Find(IReadOnlyList<OcrWord> words)
    {
        var hits = new List<RedactionHit>();
        foreach (var line in OcrText.GroupLines(words))
        {
            // Rebuild the line exactly as Assemble does, tracking each word's char span.
            var sb = new StringBuilder();
            var spans = new List<(int Start, int End, RectPx Rect)>();
            foreach (var w in line)
            {
                if (sb.Length > 0) sb.Append(' ');
                int start = sb.Length;
                sb.Append(w.Text.Trim());
                spans.Add((start, sb.Length, w.Rect));
            }
            string text = sb.ToString();
            var claimed = new List<(int Start, int End)>();

            foreach (var (kind, rx, valid) in Detectors)
            {
                foreach (Match m in rx.Matches(text))
                {
                    int s = m.Index, e = m.Index + m.Length;
                    if (kind == "url") while (e > s && ".,;:!?)]".Contains(text[e - 1])) e--; // trailing sentence punctuation is not part of the URL
                    if (e <= s) continue;
                    if (claimed.Any(c => s < c.End && c.Start < e)) continue;
                    if (valid is not null && !valid(text[s..e])) continue;
                    claimed.Add((s, e));

                    RectPx? box = null;
                    foreach (var sp in spans)
                        if (sp.Start < e && s < sp.End)
                            box = box is { } b ? b.Union(sp.Rect) : sp.Rect;
                    if (box is { } hit) hits.Add(new RedactionHit(hit.Inflate(1), kind));
                }
            }
        }
        return hits.OrderBy(h => h.Rect.Y).ThenBy(h => h.Rect.X).ToList();
    }

    /// <summary>13–19 digits (separators ignored) passing the Luhn checksum.</summary>
    private static bool IsCardNumber(string s)
    {
        Span<int> digits = stackalloc int[19];
        int n = 0;
        foreach (char c in s)
        {
            if (c is < '0' or > '9') continue;
            if (n == 19) return false;
            digits[n++] = c - '0';
        }
        if (n < 13) return false;
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            int d = digits[n - 1 - i];
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return sum % 10 == 0;
    }

    private static bool IsIPv4(string s) =>
        s.Split('.').All(part => int.TryParse(part, out int v) && v is >= 0 and <= 255);

    /// <summary>API-key shape: a long unbroken token mixing letters and digits (plain long words do not qualify).</summary>
    private static bool IsTokenShape(string s) =>
        s.Any(char.IsLetter) && s.Any(char.IsDigit);
}
