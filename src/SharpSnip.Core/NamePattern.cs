using System.Text;

namespace SharpSnip.Core;

/// <summary>
/// File and folder name patterns. Tokens: {yyyy} {MM} {dd} {HH} {mm} {ss} {ms} {date} {time} {n}.
/// {n} = lowest unused number (1, 2, ...). Unknown braces stay as text. Result is always a safe name.
/// </summary>
public static class NamePattern
{
    public const string DefaultFile = "{date}-{time}-{ms}";
    public const string DefaultFolder = "";
    public const int MaxLength = 120;

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    public static bool IsValid(string? pattern) => pattern is not null && pattern.Length <= MaxLength && !pattern.Contains('\0');

    private static string Expand(string pattern, DateTime t, int n) =>
        pattern.Replace("{yyyy}", t.ToString("yyyy")).Replace("{MM}", t.ToString("MM")).Replace("{dd}", t.ToString("dd"))
               .Replace("{HH}", t.ToString("HH")).Replace("{mm}", t.ToString("mm")).Replace("{ss}", t.ToString("ss"))
               .Replace("{ms}", t.ToString("fff")).Replace("{date}", t.ToString("yyyyMMdd")).Replace("{time}", t.ToString("HHmmss"))
               .Replace("{n}", n.ToString());

    /// <summary>One path segment made safe: bad chars to '_', trailing dots/spaces cut, reserved device names escaped, never empty.</summary>
    public static string SafeSegment(string s)
    {
        var bad = new HashSet<char>(Path.GetInvalidFileNameChars()) { ':', '*', '?', '"', '<', '>', '|', '/', '\\' };
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(bad.Contains(c) || char.IsControl(c) ? '_' : c);
        string r = sb.ToString().Trim().TrimEnd('.', ' ');
        if (r.Length == 0 || r == "." || r == "..") r = "capture";
        if (Reserved.Contains(r.Split('.')[0])) r = "_" + r;
        return r;
    }

    /// <summary>File name without extension. Blank or invalid pattern falls back to the default.</summary>
    public static string RenderFile(string? pattern, DateTime local, int n = 1) =>
        SafeSegment(Expand(IsValid(pattern) && !string.IsNullOrWhiteSpace(pattern) ? pattern! : DefaultFile, local, n));

    /// <summary>Sub-folder path under the output folder (segments split on / or \). Empty pattern = no sub-folder.</summary>
    public static string[] RenderFolder(string? pattern, DateTime local)
    {
        if (!IsValid(pattern) || string.IsNullOrWhiteSpace(pattern)) return Array.Empty<string>();
        return Expand(pattern!, local, 1).Split('/', '\\').Where(p => p.Trim().Length > 0).Select(SafeSegment).ToArray();
    }

    /// <summary>Full path; when the pattern has {n}, the lowest free number is used, else " (2)", " (3)" are appended on clash.</summary>
    public static string Resolve(string root, string? folderPattern, string? filePattern, string ext, DateTime local, Func<string, bool> exists)
    {
        string dir = root;
        foreach (var seg in RenderFolder(folderPattern, local)) dir = Path.Combine(dir, seg);
        string pat = IsValid(filePattern) && !string.IsNullOrWhiteSpace(filePattern) ? filePattern! : DefaultFile;
        ext = ext.TrimStart('.');
        if (pat.Contains("{n}"))
        {
            for (int n = 1; n < 1_000_000; n++)
            {
                string p = Path.Combine(dir, RenderFile(pat, local, n) + "." + ext);
                if (!exists(p)) return p;
            }
        }
        string baseName = RenderFile(pat, local);
        string first = Path.Combine(dir, baseName + "." + ext);
        if (!exists(first)) return first;
        for (int i = 2; i < 1_000_000; i++)
        {
            string p = Path.Combine(dir, $"{baseName} ({i})." + ext);
            if (!exists(p)) return p;
        }
        return first;
    }
}
