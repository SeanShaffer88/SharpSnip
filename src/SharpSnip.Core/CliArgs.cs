namespace SharpSnip.Core;

/// <summary>
/// Command-line lookups that cannot be fooled by values: a token that follows a value-taking flag is
/// that flag's value, never a flag of its own (so <c>--out --clean</c> saves to a file named "--clean"
/// instead of wiping settings). Flags are case-insensitive.
/// </summary>
public static class CliArgs
{
    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--monitor", "--delay", "--out", "--file", "--export", "--run-workflow", "--record", "--interval", "--count",
        "--fps", "--quality", "--size", "--region", "--max-mb", "--at", "--webcam-corner",
    };

    public static bool TakesValue(string flag) => ValueFlags.Contains(flag);

    /// <summary>Index of the flag as a real flag (not sitting in a value slot), or -1.</summary>
    private static int IndexOf(string[] args, string flag)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals(flag, StringComparison.OrdinalIgnoreCase)) continue;
            if (i > 0 && ValueFlags.Contains(args[i - 1])) continue;
            return i;
        }
        return -1;
    }

    public static bool Has(string[] args, string flag) => IndexOf(args, flag) >= 0;

    /// <summary>The token after the flag; null when the flag is absent, last, or followed by another "--" flag.</summary>
    public static string? Value(string[] args, string flag)
    {
        int i = IndexOf(args, flag);
        if (i < 0 || i + 1 >= args.Length) return null;
        string v = args[i + 1];
        return v.StartsWith("--", StringComparison.Ordinal) ? null : v;
    }
}
