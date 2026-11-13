using System.Globalization;

namespace Snipit.Core;

/// <summary>Daily log files ("sharpsnip-yyyyMMdd.log") older than the retention window may be deleted.</summary>
public static class LogRetention
{
    public const int KeepDays = 30;
    public const string FilePrefix = "sharpsnip-"; // Preserve log continuity across the rename.

    /// <summary>True only for a Snipit daily log whose date is at least <paramref name="keepDays"/> before <paramref name="today"/>.</summary>
    public static bool IsExpired(string fileName, DateOnly today, int keepDays = KeepDays)
    {
        if (!fileName.StartsWith(FilePrefix, StringComparison.Ordinal) || !fileName.EndsWith(".log", StringComparison.Ordinal))
            return false;
        string stamp = fileName[FilePrefix.Length..^4];
        if (!DateOnly.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        return today.DayNumber - date.DayNumber >= keepDays;
    }
}
