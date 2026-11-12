using System.Globalization;

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

    private static void Write(string level, string msg)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogDir);
                string file = Path.Combine(AppPaths.LogDir, $"sharpsnip-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{level}] {msg}{Environment.NewLine}");
            }
        }
        catch (Exception) { /* logging must never break capture */ }
    }
}
