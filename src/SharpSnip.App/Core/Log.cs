using System.Globalization;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>Local, timestamped, level-tagged log. Never leaves the machine; never throws.</summary>
internal static class Log
{
    private static readonly object Gate = new();
    public static bool Verbose { get; set; }

    public static void Debug(string msg) { if (Verbose) Write("DEBUG", msg); }
    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex is null ? msg : $"{msg}: {ex}");

    private static bool _pruned;

    /// <summary>Delete daily logs past the retention window. Runs once per process, on first write.</summary>
    private static void PruneOnce()
    {
        if (_pruned) return;
        _pruned = true;
        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (string file in Directory.EnumerateFiles(AppPaths.LogDir))
            if (LogRetention.IsExpired(Path.GetFileName(file), today))
                try { File.Delete(file); } catch (IOException) { /* in use or gone: next run retries */ }
    }

    private static void Write(string level, string msg)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogDir);
                PruneOnce();
                string file = Path.Combine(AppPaths.LogDir, $"sharpsnip-{DateTime.Now:yyyyMMdd}.log");
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.Exists && LogPolicy.ShouldRoll(fi.Length)) File.Move(file, file + ".1", overwrite: true);
                }
                catch (IOException) { /* in use: keep appending */ }
                File.AppendAllText(file, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{level}] {msg}{Environment.NewLine}");
            }
        }
        catch (Exception) { /* logging must never break capture */ }
    }
}
