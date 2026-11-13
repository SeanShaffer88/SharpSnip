namespace Snipit.Core;

/// <summary>
/// Whole-image adjustments on a BGRA buffer (4 bytes per pixel, row-major, no
/// row padding). Colour operations work per pixel and never touch alpha;
/// geometry operations remap whole pixels. The buffer is validated against the
/// declared size so a mismatched caller fails loudly instead of corrupting the
/// image. (Kept in the pure-logic layer so the exact byte behaviour is testable
/// without Windows.)
/// </summary>
public static class PixelAdjust
{
    public static void Invert(byte[] bgra)
    {
        ValidatePixels(bgra);
        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = (byte)(255 - bgra[i]);
            bgra[i + 1] = (byte)(255 - bgra[i + 1]);
            bgra[i + 2] = (byte)(255 - bgra[i + 2]);
        }
    }

    /// <summary>Paints a solid frame of the given thickness (clamped to half the smaller side) inside the image edges.</summary>
    public static void Border(byte[] bgra, int width, int height, int thickness, byte r, byte g, byte b)
    {
        ValidateFrame(bgra, width, height);
        int t = Math.Clamp(thickness, 0, Math.Min(width, height) / 2);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x >= t && x < width - t && y >= t && y < height - t) continue;
                int i = (y * width + x) * 4;
                bgra[i] = b; bgra[i + 1] = g; bgra[i + 2] = r; bgra[i + 3] = 255;
            }
        }
    }

    /// <summary>Rec.601 luma: grey = (299 R + 587 G + 114 B) / 1000, rounded.</summary>
    public static void Grayscale(byte[] bgra)
    {
        ValidatePixels(bgra);
        for (int i = 0; i < bgra.Length; i += 4)
        {
            int grey = (299 * bgra[i + 2] + 587 * bgra[i + 1] + 114 * bgra[i] + 500) / 1000;
            bgra[i] = bgra[i + 1] = bgra[i + 2] = (byte)grey;
        }
    }

    /// <summary>
    /// Per channel: value' = clamp((value − 128) × contrast + 128 + brightness).
    /// <paramref name="brightness"/> is clamped to [−255, 255];
    /// <paramref name="contrast"/> is a multiplier (1 = identity, 0 = flat mid-grey).
    /// </summary>
    public static void BrightnessContrast(byte[] bgra, int brightness, double contrast)
    {
        ValidatePixels(bgra);
        if (contrast < 0) throw new ArgumentOutOfRangeException(nameof(contrast), "Contrast multiplier cannot be negative.");
        brightness = Math.Clamp(brightness, -255, 255);
        for (int i = 0; i < bgra.Length; i += 4)
            for (int c = 0; c < 3; c++)
            {
                double v = (bgra[i + c] - 128) * contrast + 128 + brightness;
                bgra[i + c] = (byte)Math.Clamp(Math.Round(v), 0, 255);
            }
    }

    /// <summary>
    /// Scale colour about the Rec.601 luma: channel' = clamp(luma + (channel − luma) × factor).
    /// 1 = identity, 0 = grayscale, above 1 = more vivid. Alpha untouched.
    /// </summary>
    public static void Saturation(byte[] bgra, double factor)
    {
        ValidatePixels(bgra);
        if (factor < 0) throw new ArgumentOutOfRangeException(nameof(factor), "Saturation factor cannot be negative.");
        for (int i = 0; i < bgra.Length; i += 4)
        {
            double luma = (299 * bgra[i + 2] + 587 * bgra[i + 1] + 114 * bgra[i]) / 1000.0;
            for (int c = 0; c < 3; c++)
                bgra[i + c] = (byte)Math.Clamp(Math.Round(luma + (bgra[i + c] - luma) * factor), 0, 255);
        }
    }

    /// <summary>
    /// Sharpen with a 3×3 kernel (edge pixels use the nearest in-image neighbour):
    /// out = clamp(centre + amount × (centre − mean of 4 neighbours)). amount 0 = identity. Alpha untouched.
    /// </summary>
    public static void Sharpen(byte[] bgra, int width, int height, double amount)
    {
        ValidatePixels(bgra);
        if (width <= 0 || height <= 0 || bgra.Length != width * height * 4) throw new ArgumentException("Pixel buffer does not match width × height.");
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Sharpen amount cannot be negative.");
        var src = (byte[])bgra.Clone();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                int l = (y * width + Math.Max(x - 1, 0)) * 4, r = (y * width + Math.Min(x + 1, width - 1)) * 4;
                int u = (Math.Max(y - 1, 0) * width + x) * 4, d = (Math.Min(y + 1, height - 1) * width + x) * 4;
                for (int c = 0; c < 3; c++)
                {
                    double mean = (src[l + c] + src[r + c] + src[u + c] + src[d + c]) / 4.0;
                    bgra[i + c] = (byte)Math.Clamp(Math.Round(src[i + c] + amount * (src[i + c] - mean)), 0, 255);
                }
            }
    }

    public static void FlipHorizontal(byte[] bgra, int width, int height)
    {
        ValidateFrame(bgra, width, height);
        for (int y = 0; y < height; y++)
        {
            int row = y * width * 4;
            for (int x = 0; x < width / 2; x++)
            {
                int a = row + x * 4, b = row + (width - 1 - x) * 4;
                for (int c = 0; c < 4; c++) (bgra[a + c], bgra[b + c]) = (bgra[b + c], bgra[a + c]);
            }
        }
    }

    public static void FlipVertical(byte[] bgra, int width, int height)
    {
        ValidateFrame(bgra, width, height);
        int stride = width * 4;
        var tmp = new byte[stride];
        for (int y = 0; y < height / 2; y++)
        {
            int a = y * stride, b = (height - 1 - y) * stride;
            Array.Copy(bgra, a, tmp, 0, stride);
            Array.Copy(bgra, b, bgra, a, stride);
            Array.Copy(tmp, 0, bgra, b, stride);
        }
    }

    /// <summary>Returns a new buffer rotated 90° clockwise; width and height swap.</summary>
    public static (byte[] Pixels, int Width, int Height) Rotate90Clockwise(byte[] bgra, int width, int height)
    {
        ValidateFrame(bgra, width, height);
        var dst = new byte[bgra.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int s = (y * width + x) * 4;
                int d = (x * height + (height - 1 - y)) * 4;
                Array.Copy(bgra, s, dst, d, 4);
            }
        return (dst, height, width);
    }

    private static void ValidatePixels(byte[] bgra)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (bgra.Length % 4 != 0) throw new ArgumentException("Buffer length must be a whole number of BGRA pixels.", nameof(bgra));
    }

    private static void ValidateFrame(byte[] bgra, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        if (bgra.Length != (long)width * height * 4)
            throw new ArgumentException($"Buffer length {bgra.Length} does not match {width}×{height} BGRA.", nameof(bgra));
    }
}
