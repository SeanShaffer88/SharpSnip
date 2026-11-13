namespace Snipit.Core;

/// <summary>One captured viewport strip of a scrolling capture: BGRA32 pixels whose top row is content row <see cref="ContentY"/>.</summary>
public readonly record struct ScrollStrip(int ContentY, int Height, byte[] Bgra);

/// <summary>
/// Scrolling-capture mathematics (roadmap Phase 3): where to place viewport
/// strips over tall content, how to compose them into one image, and — for
/// targets scrolled by wheel, whose offsets are unknown — how much of a new
/// strip duplicates the bottom of what is already composed. All BGRA32.
/// </summary>
public static class ScrollStitch
{
    /// <summary>
    /// Strip offsets for content of <paramref name="content"/> px seen through a
    /// <paramref name="viewport"/> px window, stepping by viewport − overlap and
    /// anchoring the final strip at the content end so nothing is missed.
    /// </summary>
    public static int[] Plan(int viewport, int content, int minOverlap = 32)
    {
        if (viewport < 1) throw new ArgumentException("Viewport must be at least 1px.", nameof(viewport));
        if (content < 1) throw new ArgumentException("Content must be at least 1px.", nameof(content));
        if (content <= viewport) return new[] { 0 };
        int step = Math.Max(1, viewport - Math.Clamp(minOverlap, 0, viewport - 1));
        int last = content - viewport;
        var offsets = new List<int>();
        for (int y = 0; y < last; y += step) offsets.Add(y);
        if (offsets[^1] != last) offsets.Add(last);
        return offsets.ToArray();
    }

    /// <summary>
    /// Compose strips into the full content image. Strips must start at 0, be in
    /// ascending order, and each must begin at or before the rows already covered
    /// (no gaps); only its not-yet-covered rows are copied. A strip may extend
    /// past the content end and is clamped.
    /// </summary>
    public static byte[] Compose(int width, int contentHeight, IReadOnlyList<ScrollStrip> strips)
    {
        if (width < 1) throw new ArgumentException("Width must be at least 1px.", nameof(width));
        if (contentHeight < 1) throw new ArgumentException("Content height must be at least 1px.", nameof(contentHeight));
        if (strips is null || strips.Count == 0) throw new ArgumentException("At least one strip is required.", nameof(strips));

        var output = new byte[width * contentHeight * 4];
        int covered = 0;
        foreach (var s in strips)
        {
            if (s.Height < 1 || s.Bgra is null || s.Bgra.Length != (long)width * s.Height * 4)
                throw new ArgumentException($"Strip at {s.ContentY} must hold exactly width x height BGRA pixels.", nameof(strips));
            if (s.ContentY > covered)
                throw new ArgumentException($"Strip at {s.ContentY} leaves a gap after {covered} covered rows.", nameof(strips));
            if (s.ContentY < 0) throw new ArgumentException("Strip offset cannot be negative.", nameof(strips));

            int srcRow = covered - s.ContentY;               // rows of this strip already covered
            int rows = Math.Min(s.Height - srcRow, contentHeight - covered);
            if (rows > 0)
            {
                Array.Copy(s.Bgra, srcRow * width * 4, output, covered * width * 4, rows * width * 4);
                covered += rows;
            }
            if (covered >= contentHeight) break;
        }
        if (covered < contentHeight)
            throw new ArgumentException($"Strips cover {covered} of {contentHeight} content rows.", nameof(strips));
        return output;
    }

    /// <summary>
    /// Largest k (rows) such that the bottom k rows of the accumulated image equal
    /// the top k rows of <paramref name="strip"/> — the overlap between what is
    /// composed and a newly captured strip at an unknown scroll offset.
    /// Returns 0 when no row matches (strips abut, or content did not move).
    /// </summary>
    public static int FindOverlap(ReadOnlySpan<byte> accumulated, int accHeight, ReadOnlySpan<byte> strip, int stripHeight, int width)
    {
        int rowBytes = width * 4;
        if (width < 1 || accHeight < 1 || stripHeight < 1) return 0;
        if (accumulated.Length < accHeight * rowBytes || strip.Length < stripHeight * rowBytes) return 0;
        int max = Math.Min(accHeight, stripHeight);
        for (int k = max; k >= 1; k--)
        {
            var a = accumulated.Slice((accHeight - k) * rowBytes, k * rowBytes);
            var b = strip.Slice(0, k * rowBytes);
            if (a.SequenceEqual(b)) return k;
        }
        return 0;
    }
}
