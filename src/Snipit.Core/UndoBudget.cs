namespace Snipit.Core;

/// <summary>
/// Memory cap for the editor's undo history. Each whole-image edit keeps its full bitmap, so a few edits of a
/// large multi-monitor capture add up. Entries are costed in bytes; the newest ones are kept within a budget.
/// </summary>
public static class UndoBudget
{
    public const long DefaultBytes = 512L * 1024 * 1024;

    /// <summary>
    /// How many of the newest entries fit the budget. Costs are listed newest first. Always keeps at least
    /// one entry when there is any, even if that single entry is over budget, so the latest edit stays undoable.
    /// </summary>
    public static int KeepCount(IReadOnlyList<long> costsNewestFirst, long budgetBytes)
    {
        if (costsNewestFirst is null) throw new ArgumentNullException(nameof(costsNewestFirst));
        long total = 0; int keep = 0;
        foreach (long c in costsNewestFirst)
        {
            total += Math.Max(0, c);
            if (total > budgetBytes && keep >= 1) break;
            keep++;
        }
        return keep;
    }

    /// <summary>Bytes a 32-bit bitmap of this size occupies.</summary>
    public static long BitmapBytes(int width, int height) => (long)Math.Max(0, width) * Math.Max(0, height) * 4;
}
