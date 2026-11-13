namespace SharpSnip.Core;

/// <summary>
/// Stride-aware block copies between BGRA pixel buffers — the assembly step
/// that stitches per-monitor frames (e.g. Windows.Graphics.Capture) into one
/// canvas. Pure math so it is byte-testable without Windows.
/// </summary>
public static class PixelBlit
{
    /// <summary>Copies a widthPx×heightPx rectangle of 4-byte pixels from src at (srcX, srcY) into dst at (dstX, dstY).</summary>
    public static void Copy(byte[] src, int srcStrideBytes, int srcX, int srcY,
                            byte[] dst, int dstStrideBytes, int dstX, int dstY,
                            int widthPx, int heightPx)
    {
        if (src is null) throw new ArgumentNullException(nameof(src));
        if (dst is null) throw new ArgumentNullException(nameof(dst));
        if (widthPx <= 0 || heightPx <= 0) throw new ArgumentOutOfRangeException(nameof(widthPx), "Copy size must be positive.");
        if (srcX < 0 || srcY < 0 || dstX < 0 || dstY < 0) throw new ArgumentOutOfRangeException(nameof(srcX), "Origins must be non-negative.");
        int rowBytes = widthPx * 4;
        if (srcStrideBytes < rowBytes || dstStrideBytes < rowBytes)
            throw new ArgumentException("A stride is smaller than one row of the copy.");
        long srcEnd = (long)(srcY + heightPx - 1) * srcStrideBytes + (long)srcX * 4 + rowBytes;
        long dstEnd = (long)(dstY + heightPx - 1) * dstStrideBytes + (long)dstX * 4 + rowBytes;
        if (srcEnd > src.Length) throw new ArgumentException("The source rectangle exceeds the source buffer.", nameof(src));
        if (dstEnd > dst.Length) throw new ArgumentException("The destination rectangle exceeds the destination buffer.", nameof(dst));

        for (int row = 0; row < heightPx; row++)
        {
            int s = (srcY + row) * srcStrideBytes + srcX * 4;
            int d = (dstY + row) * dstStrideBytes + dstX * 4;
            Array.Copy(src, s, dst, d, rowBytes);
        }
    }

    /// <summary>Nearest-neighbour scaled copy of a whole source image into a rectangle of the destination (webcam overlay).</summary>
    public static void ScaleCopy(byte[] src, int srcStrideBytes, int srcWidthPx, int srcHeightPx,
                                 byte[] dst, int dstStrideBytes, int dstX, int dstY, int dstWidthPx, int dstHeightPx)
    {
        if (src is null) throw new ArgumentNullException(nameof(src));
        if (dst is null) throw new ArgumentNullException(nameof(dst));
        if (srcWidthPx <= 0 || srcHeightPx <= 0 || dstWidthPx <= 0 || dstHeightPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(dstWidthPx), "Sizes must be positive.");
        if (dstX < 0 || dstY < 0) throw new ArgumentOutOfRangeException(nameof(dstX), "Origin must be non-negative.");
        long srcNeed = (long)(srcHeightPx - 1) * srcStrideBytes + (long)srcWidthPx * 4;
        long dstNeed = (long)(dstY + dstHeightPx - 1) * dstStrideBytes + (long)(dstX + dstWidthPx) * 4;
        if (srcNeed > src.Length) throw new ArgumentException("The source image exceeds the source buffer.", nameof(src));
        if (dstNeed > dst.Length) throw new ArgumentException("The destination rectangle exceeds the destination buffer.", nameof(dst));

        for (int y = 0; y < dstHeightPx; y++)
        {
            int sy = y * srcHeightPx / dstHeightPx;
            int sRow = sy * srcStrideBytes;
            int dRow = (dstY + y) * dstStrideBytes + dstX * 4;
            for (int x = 0; x < dstWidthPx; x++)
            {
                int sx = x * srcWidthPx / dstWidthPx;
                Array.Copy(src, sRow + sx * 4, dst, dRow + x * 4, 4);
            }
        }
    }
}
