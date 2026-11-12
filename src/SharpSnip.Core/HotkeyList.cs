namespace SharpSnip.Core;

/// <summary>Semicolon-separated hotkeys for "capture monitor N" (first entry = monitor 1).</summary>
public static class HotkeyList
{
    public const int Max = 9;
    public const string Default = "Ctrl+Alt+Shift+1;Ctrl+Alt+Shift+2;Ctrl+Alt+Shift+3;Ctrl+Alt+Shift+4";

    /// <summary>Empty text is valid and means "no per-monitor hotkeys". Duplicates and bad entries are rejected.</summary>
    public static bool TryParse(string? text, out IReadOnlyList<HotkeySpec> specs)
    {
        var list = new List<HotkeySpec>();
        specs = list;
        if (string.IsNullOrWhiteSpace(text)) return true;
        foreach (var part in text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!HotkeySpec.TryParse(part, out var spec) || list.Contains(spec)) { list.Clear(); return false; }
            list.Add(spec);
        }
        if (list.Count > Max) { list.Clear(); return false; }
        return true;
    }
}
