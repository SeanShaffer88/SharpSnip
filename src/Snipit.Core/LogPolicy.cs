namespace Snipit.Core;

/// <summary>Size limit for one day's log file. Past it the file is moved to ".1" (replacing the older one) and a new file starts.</summary>
public static class LogPolicy
{
    public const long MaxFileBytes = 5L * 1024 * 1024;
    public static bool ShouldRoll(long currentBytes) => currentBytes >= MaxFileBytes;
}
