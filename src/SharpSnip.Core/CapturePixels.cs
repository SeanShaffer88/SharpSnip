namespace SharpSnip.Core;

/// <summary>
/// Post-processing of a captured BGRA frame, in place on the caller's single buffer.
/// GDI leaves alpha at 0: covered pixels become opaque, pixels in monitor-layout
/// gaps become the matte colour. (Moved out of the capture engine so the exact
/// byte behaviour is testable without Windows.)
/// </summary>
public static class CapturePixels
{
    /// <summary>A 0xAARRGGBB matte as the four buffer bytes B, G, R, A.</summary>
    public static byte[] MatteBytes(uint matteArgb) =>
        new[] { (byte)matteArgb, (byte)(matteArgb >> 8), (byte)(matteArgb >> 16), (byte)(matteArgb >> 24) };

    public static void ApplyAlpha(byte[] bgra, int width, IReadOnlyList<Placement> places, bool gaps, uint matteArgb)
    {
        if (!gaps)
        {
            for (int i = 3; i < bgra.Length; i += 4) bgra[i] = 0xFF;
            return;
        }

        int height = width > 0 ? bgra.Length / 4 / width : 0;
        var covered = new bool[width * height];
        foreach (var p in places)
            for (int row = 0; row < p.Source.Height; row++)
            {
                int start = (p.DestY + row) * width + p.DestX;
                for (int col = 0; col < p.Source.Width; col++) covered[start + col] = true;
            }

        var matte = MatteBytes(matteArgb);
        for (int px = 0; px < covered.Length; px++)
        {
            int o = px * 4;
            if (covered[px]) bgra[o + 3] = 0xFF;
            else { bgra[o] = matte[0]; bgra[o + 1] = matte[1]; bgra[o + 2] = matte[2]; bgra[o + 3] = matte[3]; }
        }
    }
}
