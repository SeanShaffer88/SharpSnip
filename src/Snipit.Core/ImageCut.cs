namespace Snipit.Core;

/// <summary>Which way a cut-out band runs.</summary>
public enum CutAxis { Rows, Columns }

/// <summary>
/// "Cut out" on a BGRA buffer (4 bytes per pixel, row-major, straight alpha, no row padding): remove a strip
/// of rows or columns and join the two sides, to drop the boring middle of a tall or wide capture.
/// Pure logic. The input is never modified, and at least one row and one column always remain.
/// </summary>
public static class ImageCut
{
    /// <summary>A band to remove: <paramref name="Length"/> rows or columns starting at <paramref name="Start"/>.</summary>
    public readonly record struct Band(CutAxis Axis, int Start, int Length);

    /// <summary>
    /// Turns a dragged rectangle (image pixels) into a band. The drag that covers the larger share of its own
    /// dimension decides: a wide drag cuts rows, a tall one cuts columns; a tie cuts rows. Returns null when
    /// nothing would be cut or the band would remove the whole image.
    /// </summary>
    public static Band? Plan(int x, int y, int rw, int rh, int w, int h)
    {
        if (w < 2 || h < 2 || rw < 1 || rh < 1) return null;
        double wideShare = (double)Math.Min(rw, w) / w, tallShare = (double)Math.Min(rh, h) / h;
        Band band;
        if (wideShare >= tallShare)
        {
            int s = Math.Clamp(y, 0, h), e = Math.Clamp(y + rh, 0, h);
            band = new Band(CutAxis.Rows, s, e - s);
        }
        else
        {
            int s = Math.Clamp(x, 0, w), e = Math.Clamp(x + rw, 0, w);
            band = new Band(CutAxis.Columns, s, e - s);
        }
        int total = band.Axis == CutAxis.Rows ? h : w;
        return band.Length <= 0 || band.Length >= total ? null : band;
    }

    /// <summary>Returns the image with the band removed: (pixels, width, height). Out-of-range bands are clamped; a band covering everything is refused.</summary>
    public static (byte[] Pixels, int Width, int Height) Remove(byte[] bgra, int w, int h, Band band)
    {
        if (bgra is null) throw new ArgumentNullException(nameof(bgra));
        if (w < 1 || h < 1) throw new ArgumentOutOfRangeException(nameof(w), "Image size must be positive.");
        if (bgra.Length != checked(w * h * 4)) throw new ArgumentException("Pixel buffer does not match the declared size.", nameof(bgra));
        int total = band.Axis == CutAxis.Rows ? h : w;
        int start = Math.Clamp(band.Start, 0, total);
        int len = Math.Clamp(band.Length, 0, total - start);
        if (len == 0 || len >= total) return ((byte[])bgra.Clone(), w, h);

        if (band.Axis == CutAxis.Rows)
        {
            int nh = h - len;
            var o = new byte[checked(w * nh * 4)];
            Buffer.BlockCopy(bgra, 0, o, 0, start * w * 4);
            Buffer.BlockCopy(bgra, (start + len) * w * 4, o, start * w * 4, (h - start - len) * w * 4);
            return (o, w, nh);
        }
        else
        {
            int nw = w - len;
            var o = new byte[checked(nw * h * 4)];
            for (int y = 0; y < h; y++)
            {
                Buffer.BlockCopy(bgra, y * w * 4, o, y * nw * 4, start * 4);
                Buffer.BlockCopy(bgra, (y * w + start + len) * 4, o, (y * nw + start) * 4, (w - start - len) * 4);
            }
            return (o, nw, h);
        }
    }
}
