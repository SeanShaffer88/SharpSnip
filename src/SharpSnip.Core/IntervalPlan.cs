namespace SharpSnip.Core;

/// <summary>Rules for interval (time-lapse) capture.</summary>
public static class IntervalPlan
{
    public const int MinSeconds = 1, MaxSeconds = 86_400, MaxShots = 100_000;

    public static int SanitizeSeconds(int s) => Math.Clamp(s, MinSeconds, MaxSeconds);

    /// <summary>One sub-folder per run so runs never mix.</summary>
    public static string FolderName(DateTime local) => $"Interval-{local:yyyyMMdd-HHmmss}";

    /// <summary>Zero-padded so files sort in capture order.</summary>
    public static string FrameName(int index, string extension) =>
        $"frame-{index:D5}.{ImageFormats.Normalize(extension) ?? ImageFormats.Default}";
}
