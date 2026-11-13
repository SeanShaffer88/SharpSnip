using System.Globalization;

namespace SharpSnip.Core;

/// <summary>Daily log files ("sharpsnip-yyyyMMdd.log") older than the retention window may be deleted.</summary>
public static class LogRetention
{
    public const int KeepDays = 30;

    /// <summary>True only for a SharpSnip daily log whose date is at least <paramref name="keepDays"/> before <paramref name="today"/>.</summary>
    public static bool IsExpired(string fileName, DateOnly today, int keepDays = KeepDays)
    {
        const string prefix = "sharpsnip-";
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal) || !fileName.EndsWith(".log", StringComparison.Ordinal))
            return false;
        string stamp = fileName[prefix.Length..^4];
        if (!DateOnly.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        return today.DayNumber - date.DayNumber >= keepDays;
    }
}
