namespace SharpSnip.Core;

public static class OutputNaming
{
    public static string FileName(DateTime local, string extension) =>
        $"{local:yyyyMMdd-HHmmss-fff}.{ImageFormats.Normalize(extension) ?? ImageFormats.Default}";

    /// <summary>Where captures land: the configured folder, or — when none is set — the user's Pictures folder (the OS path is passed in, so a redirected Pictures folder is honoured).</summary>
    public static string ResolveFolder(string configured, string picturesFolder) =>
        string.IsNullOrWhiteSpace(configured) ? picturesFolder : configured;

    /// <summary>
    /// First free variant of an auto-generated path: the path itself, then "name-2.ext",
    /// "name-3.ext", ... Used for generated names only — a path the user named explicitly
    /// is allowed to overwrite.
    /// </summary>
    public static string UniquePath(string path, Func<string, bool> exists)
    {
        if (!exists(path)) return path;
        string dir = Path.GetDirectoryName(path) ?? "";
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for (int n = 2; ; n++)
        {
            string candidate = Path.Combine(dir, $"{name}-{n}{ext}");
            if (!exists(candidate)) return candidate;
        }
    }
}
