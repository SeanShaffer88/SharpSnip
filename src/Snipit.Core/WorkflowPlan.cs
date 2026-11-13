namespace Snipit.Core;

/// <summary>One named workflow definition as stored in preferences: an ordered list of step spec lines.</summary>
public sealed class WorkflowDef
{
    public string Name { get; set; } = "";
    /// <summary>Capture, Recording, or Both.</summary>
    public string ApplyTo { get; set; } = "Both";
    /// <summary>Step spec lines, e.g. "copy: D:\Backup", "run: C:\tools\x.exe|\"%path%\"", "effect: Grayscale", "format: jpg", "editor:".</summary>
    public List<string> Steps { get; set; } = new();
}

/// <summary>One parsed workflow step: a kind and its argument ("" when the kind takes none).</summary>
public readonly record struct WorkflowStep(string Kind, string Arg);

/// <summary>
/// Workflow parsing, validation and placeholder expansion. A workflow is a
/// short chain that runs locally on a finished capture or recording — copy it
/// somewhere, convert it, run a program on it. Everything is plain text in
/// the settings file, validated here so a hand-edited or corrupt entry can
/// never produce a half-parsed chain. Zero network anything, by charter.
/// </summary>
public static class WorkflowPlan
{
    public const int MaxWorkflows = 16;
    public const int MaxSteps = 12;

    public static readonly string[] Kinds = { "copy", "run", "editor", "effect", "format" };
    public static readonly string[] ApplyTos = { "Capture", "Recording", "Both" };

    /// <summary>Parses one spec line ("kind: arg"; the arg may itself contain colons). False = not a usable step.</summary>
    public static bool TryParseStep(string? line, out WorkflowStep step)
    {
        step = default;
        if (string.IsNullOrWhiteSpace(line)) return false;
        int colon = line.IndexOf(':');
        if (colon < 0) return false;
        string kind = line[..colon].Trim().ToLowerInvariant();
        if (!Kinds.Contains(kind)) return false;
        string arg = line[(colon + 1)..].Trim();
        switch (kind)
        {
            case "editor":
                if (arg.Length != 0) return false;
                break;
            case "copy":
                if (arg.Length == 0) return false;
                break;
            case "run": // program|arguments — the pipe separates them so paths may contain spaces
                int pipe = arg.IndexOf('|');
                if (pipe <= 0) return false;
                break;
            case "effect":
                if (arg is not ("Border" or "Grayscale")) return false; // a real, non-empty effect, named exactly
                break;
            case "format":
                if (ImageFormats.Normalize(arg) is { } img) arg = img;
                else if (FfmpegPlan.Find(arg) is { } preset) arg = preset.Name; // a recording conversion, e.g. format: webm
                else return false;
                break;
        }
        step = new WorkflowStep(kind, arg);
        return true;
    }

    /// <summary>True when a format step names an FFmpeg preset (a recording conversion) rather than a still-image format.</summary>
    public static bool IsFfmpegFormat(WorkflowStep step) =>
        step.Kind == "format" && ImageFormats.Normalize(step.Arg) is null && FfmpegPlan.Find(step.Arg) is not null;

    /// <summary>Extensions treated as recordings for workflow routing: the MP4/GIF recordings and FFmpeg export products.</summary>
    public static bool IsRecordingFile(string? path) =>
        path is not null &&
        (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase));

    /// <summary>The canonical stored line for a parsed step.</summary>
    public static string FormatStep(WorkflowStep step) => step.Arg.Length == 0 ? step.Kind + ":" : $"{step.Kind}: {step.Arg}";

    /// <summary>Keeps only the valid steps, canonicalized, capped at MaxSteps.</summary>
    public static List<string> SanitizeSteps(IEnumerable<string>? lines)
    {
        var kept = new List<string>();
        if (lines is null) return kept;
        foreach (var line in lines)
        {
            if (kept.Count >= MaxSteps) break;
            if (TryParseStep(line, out var step)) kept.Add(FormatStep(step));
        }
        return kept;
    }

    /// <summary>Validates a stored ApplyTo value; anything unknown becomes Both.</summary>
    public static string SanitizeApplyTo(string? value)
    {
        foreach (var a in ApplyTos)
            if (string.Equals(a, value, StringComparison.OrdinalIgnoreCase)) return a;
        return "Both";
    }

    /// <summary>True when a workflow with this ApplyTo should run for the given product kind ("Capture"/"Recording").</summary>
    public static bool AppliesTo(string? applyTo, string productKind) =>
        SanitizeApplyTo(applyTo) is "Both" || string.Equals(SanitizeApplyTo(applyTo), productKind, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Expands %path%, %dir%, %name% (file name without extension) and %ext%
    /// (extension without the dot) in a step argument template for a file.
    /// Unknown placeholders pass through untouched.
    /// </summary>
    public static string Expand(string template, string filePath)
    {
        string dir = Path.GetDirectoryName(filePath) ?? "";
        string name = Path.GetFileNameWithoutExtension(filePath);
        string ext = Path.GetExtension(filePath).TrimStart('.');
        return template
            .Replace("%path%", filePath, StringComparison.OrdinalIgnoreCase)
            .Replace("%dir%", dir, StringComparison.OrdinalIgnoreCase)
            .Replace("%name%", name, StringComparison.OrdinalIgnoreCase)
            .Replace("%ext%", ext, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Like Expand, for an argument string: a value that lands outside double quotes is quoted (so a path
    /// with spaces stays one argument); inside quotes the user already did it and the raw value is used.
    /// </summary>
    public static string ExpandArgs(string template, string filePath)
    {
        string dir = Path.GetDirectoryName(filePath) ?? "";
        string name = Path.GetFileNameWithoutExtension(filePath);
        string ext = Path.GetExtension(filePath).TrimStart('.');
        var sb = new System.Text.StringBuilder();
        bool inQuote = false;
        for (int i = 0; i < template.Length;)
        {
            char c = template[i];
            if (c == '"') { inQuote = !inQuote; sb.Append(c); i++; continue; }
            string? val = null; int len = 0;
            if (c == '%')
            {
                foreach (var (token, v) in new[] { ("%path%", filePath), ("%dir%", dir), ("%name%", name), ("%ext%", ext) })
                    if (string.Compare(template, i, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0) { val = v; len = token.Length; break; }
            }
            if (val is null) { sb.Append(c); i++; continue; }
            sb.Append(inQuote ? val : QuoteArg(val));
            i += len;
        }
        return sb.ToString();
    }

    /// <summary>Windows command-line quoting of one argument (only when it needs it).</summary>
    public static string QuoteArg(string v)
    {
        if (v.Length > 0 && v.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return v;
        int trailing = 0;
        while (trailing < v.Length && v[v.Length - 1 - trailing] == '\\') trailing++;
        return "\"" + v.Replace("\"", "\\\"") + new string('\\', trailing) + "\"";
    }

    /// <summary>Splits a run step's argument into program and its (unexpanded) arguments template.</summary>
    public static (string Program, string Args) SplitRun(string arg)
    {
        int pipe = arg.IndexOf('|');
        return pipe < 0 ? (arg.Trim(), "") : (arg[..pipe].Trim(), arg[(pipe + 1)..].Trim());
    }

    /// <summary>Finds a workflow by name (case-insensitive); null when absent.</summary>
    public static WorkflowDef? Find(IEnumerable<WorkflowDef>? workflows, string? name)
    {
        if (workflows is null || string.IsNullOrWhiteSpace(name)) return null;
        foreach (var w in workflows)
            if (string.Equals(w.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) return w;
        return null;
    }
}
