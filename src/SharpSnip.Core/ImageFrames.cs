namespace SharpSnip.Core;

/// <summary>
/// Canvas-growing effects on a BGRA buffer (4 bytes per pixel, row-major, straight alpha, no row padding).
/// Both return a new, larger buffer; the input is never modified. Pure logic, so the exact bytes are testable.
/// </summary>
public static class ImageFrames
{
    public const int MaxThickness = 512;
    public const int MaxBlur = 64;
    public const int MaxOffset = 256;

    /// <summary>Adds an opaque border OUTSIDE the image: the result is (w + 2t) x (h + 2t).</summary>
    public static (byte[] Pixels, int Width, int Height) AddBorder(byte[] bgra, int w, int h, int thickness, byte r, byte g, byte b)
    {
        Validate(bgra, w, h);
        int t = Math.Clamp(thickness, 0, MaxThickness);
        int nw = w + 2 * t, nh = h + 2 * t;
        var o = new byte[checked(nw * nh * 4)];
        for (int i = 0; i < o.Length; i += 4) { o[i] = b; o[i + 1] = g; o[i + 2] = r; o[i + 3] = 255; }
        for (int y = 0; y < h; y++)
            Buffer.BlockCopy(bgra, y * w * 4, o, ((y + t) * nw + t) * 4, w * 4);
        return (o, nw, nh);
    }

    /// <summary>Margin needed around the image so a shadow with this blur and offset is never clipped.</summary>
    public static int ShadowMargin(int blur, int dx, int dy) =>
        Math.Clamp(blur, 0, MaxBlur) * 2 + Math.Max(Math.Abs(Math.Clamp(dx, -MaxOffset, MaxOffset)), Math.Abs(Math.Clamp(dy, -MaxOffset, MaxOffset)));

    /// <summary>
    /// Drop shadow: the image's alpha silhouette, offset by (dx, dy), blurred (three box passes of the given
    /// radius ≈ Gaussian), tinted black at <paramref name="opacity"/> (0..1), under the original. Transparent
    /// margin on every side; the original sits centred in the result.
    /// </summary>
    public static (byte[] Pixels, int Width, int Height) AddShadow(byte[] bgra, int w, int h, int blur, int dx, int dy, double opacity)
    {
        Validate(bgra, w, h);
        blur = Math.Clamp(blur, 0, MaxBlur);
        dx = Math.Clamp(dx, -MaxOffset, MaxOffset); dy = Math.Clamp(dy, -MaxOffset, MaxOffset);
        opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 0.5;
        int m = ShadowMargin(blur, dx, dy);
        int nw = w + 2 * m, nh = h + 2 * m;
        var o = new byte[checked(nw * nh * 4)];

        // alpha plane of the silhouette, already offset
        var a = new float[nw * nh];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int tx = x + m + dx, ty = y + m + dy;
                a[ty * nw + tx] = bgra[(y * w + x) * 4 + 3] / 255f;
            }
        if (blur > 0) for (int pass = 0; pass < 3; pass++) { BoxBlurH(a, nw, nh, blur); BoxBlurV(a, nw, nh, blur); }

        // shadow layer (black, alpha = blurred * opacity), then the original over it
        for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
            {
                int i = (y * nw + x) * 4;
                o[i + 3] = (byte)Math.Clamp((int)Math.Round(a[y * nw + x] * opacity * 255), 0, 255);
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int s = (y * w + x) * 4, d = ((y + m) * nw + x + m) * 4;
                int fa = bgra[s + 3];
                if (fa == 255) { o[d] = bgra[s]; o[d + 1] = bgra[s + 1]; o[d + 2] = bgra[s + 2]; o[d + 3] = 255; continue; }
                if (fa == 0) continue;                          // fully transparent source pixel: shadow shows through
                int ba = o[d + 3];                              // backdrop (shadow) alpha, backdrop colour is black
                int outA = fa + ba * (255 - fa) / 255;
                if (outA == 0) continue;
                for (int c = 0; c < 3; c++) o[d + c] = (byte)(bgra[s + c] * fa / outA);   // black backdrop contributes 0
                o[d + 3] = (byte)outA;
            }
        return (o, nw, nh);
    }

    private static void BoxBlurH(float[] p, int w, int h, int r)
    {
        var row = new float[w];
        float inv = 1f / (2 * r + 1);
        for (int y = 0; y < h; y++)
        {
            int o = y * w; float sum = 0;
            for (int x = 0; x <= r && x < w; x++) sum += p[o + x];
            for (int x = 0; x < w; x++)
            {
                row[x] = sum * inv;
                int add = x + r + 1, sub = x - r;
                if (add < w) sum += p[o + add];
                if (sub >= 0) sum -= p[o + sub];
            }
            Array.Copy(row, 0, p, o, w);
        }
    }

    private static void BoxBlurV(float[] p, int w, int h, int r)
    {
        var col = new float[h];
        float inv = 1f / (2 * r + 1);
        for (int x = 0; x < w; x++)
        {
            float sum = 0;
            for (int y = 0; y <= r && y < h; y++) sum += p[y * w + x];
            for (int y = 0; y < h; y++)
            {
                col[y] = sum * inv;
                int add = y + r + 1, sub = y - r;
                if (add < h) sum += p[add * w + x];
                if (sub >= 0) sum -= p[sub * w + x];
            }
            for (int y = 0; y < h; y++) p[y * w + x] = col[y];
        }
    }

    private static void Validate(byte[] bgra, int w, int h)
    {
        if (bgra is null) throw new ArgumentNullException(nameof(bgra));
        if (w < 1 || h < 1) throw new ArgumentOutOfRangeException(nameof(w), "Image size must be positive.");
        if (bgra.Length != checked(w * h * 4)) throw new ArgumentException("Pixel buffer does not match the declared size.", nameof(bgra));
    }
}
