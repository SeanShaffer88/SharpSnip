namespace SharpSnip.Core;

/// <summary>Which sides of the image an edge effect cuts.</summary>
[Flags]
public enum EdgeSides { None = 0, Top = 1, Right = 2, Bottom = 4, Left = 8, All = 15 }

/// <summary>Shape of the cut: ragged torn paper, or a smooth wave.</summary>
public enum EdgeStyle { Torn, Wave }

/// <summary>
/// Torn-edge and wave-edge effects on a BGRA buffer (4 bytes per pixel, row-major, straight alpha, no row padding).
/// The canvas size does not change: pixels outside the cut become fully transparent. The cut never goes deeper than
/// <paramref name="depth"/> pixels, so the picture is never eaten away. Deterministic for a given seed. Pure logic.
/// </summary>
public static class EdgeEffects
{
    public const int MaxDepth = 256;

    /// <summary>The deepest cut that is allowed for this image: a quarter of the shorter side, at least 1.</summary>
    public static int MaxDepthFor(int w, int h) => Math.Clamp(Math.Min(w, h) / 4, 1, MaxDepth);

    /// <summary>Returns a new buffer; the input is not modified.</summary>
    public static byte[] Apply(byte[] bgra, int w, int h, EdgeStyle style, EdgeSides sides, int depth, int seed)
    {
        if (bgra is null) throw new ArgumentNullException(nameof(bgra));
        if (w < 1 || h < 1) throw new ArgumentOutOfRangeException(nameof(w), "Image size must be positive.");
        if (bgra.Length != checked(w * h * 4)) throw new ArgumentException("Pixel buffer does not match the declared size.", nameof(bgra));
        depth = Math.Clamp(depth, 0, MaxDepthFor(w, h));
        var o = (byte[])bgra.Clone();
        if (depth == 0 || sides == EdgeSides.None) return o;

        if (sides.HasFlag(EdgeSides.Top))
        {
            var p = Profile(style, w, depth, seed);
            for (int x = 0; x < w; x++) for (int y = 0; y < p[x]; y++) Clear(o, (y * w + x) * 4);
        }
        if (sides.HasFlag(EdgeSides.Bottom))
        {
            var p = Profile(style, w, depth, seed + 1);
            for (int x = 0; x < w; x++) for (int y = 0; y < p[x]; y++) Clear(o, ((h - 1 - y) * w + x) * 4);
        }
        if (sides.HasFlag(EdgeSides.Left))
        {
            var p = Profile(style, h, depth, seed + 2);
            for (int y = 0; y < h; y++) for (int x = 0; x < p[y]; x++) Clear(o, (y * w + x) * 4);
        }
        if (sides.HasFlag(EdgeSides.Right))
        {
            var p = Profile(style, h, depth, seed + 3);
            for (int y = 0; y < h; y++) for (int x = 0; x < p[y]; x++) Clear(o, (y * w + w - 1 - x) * 4);
        }
        return o;
    }

    /// <summary>For each position along an edge, how many pixels deep the cut goes (0..depth).</summary>
    public static int[] Profile(EdgeStyle style, int length, int depth, int seed)
    {
        var p = new int[length];
        if (depth <= 0 || length <= 0) return p;
        if (style == EdgeStyle.Wave)
        {
            double period = Math.Max(24, depth * 6.0);
            for (int i = 0; i < length; i++)
                p[i] = Math.Clamp((int)Math.Round(depth * 0.5 * (1 + Math.Sin(2 * Math.PI * i / period))), 0, depth);
            return p;
        }
        // Torn: random control values every few pixels, joined by straight lines, plus a pixel of jitter.
        int step = Math.Max(3, depth / 2);
        int knots = length / step + 2;
        var rng = new Lcg(seed);
        var k = new int[knots];
        for (int i = 0; i < knots; i++) k[i] = rng.Next(depth + 1);
        for (int i = 0; i < length; i++)
        {
            int a = i / step, f = i % step;
            double v = k[a] + (k[a + 1] - k[a]) * (double)f / step;
            p[i] = Math.Clamp((int)Math.Round(v) + rng.Next(3) - 1, 0, depth);
        }
        return p;
    }

    private static void Clear(byte[] o, int i) { o[i] = o[i + 1] = o[i + 2] = o[i + 3] = 0; }

    /// <summary>Tiny deterministic generator, so the same seed gives the same edge on every machine.</summary>
    private struct Lcg
    {
        private uint _s;
        public Lcg(int seed) { _s = (uint)seed * 2654435761u + 0x9E3779B9u; }
        public int Next(int max) { _s = _s * 1664525u + 1013904223u; return (int)((_s >> 8) % (uint)max); }
    }
}
