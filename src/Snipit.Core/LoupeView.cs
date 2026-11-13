namespace Snipit.Core;

/// <summary>Source window for a magnifier lens.</summary>
public static class LoupeView
{
    /// <summary>
    /// The part of the image a lens at (x, y, w, h) should show to magnify by <paramref name="factor"/>:
    /// the same centre, 1/factor the size, shifted back inside the image if it would overhang.
    /// Returns (X, Y, Width, Height) in image pixels.
    /// </summary>
    public static (double X, double Y, double Width, double Height) Zoom(double x, double y, double w, double h, double imgW, double imgH, double factor)
    {
        if (!(factor >= 1)) throw new ArgumentOutOfRangeException(nameof(factor), "Magnification must be at least 1.");
        if (imgW <= 0 || imgH <= 0) throw new ArgumentOutOfRangeException(nameof(imgW), "Image size must be positive.");
        double vw = Math.Min(w / factor, imgW), vh = Math.Min(h / factor, imgH);
        double vx = Math.Clamp(x + w / 2 - vw / 2, 0, imgW - vw);
        double vy = Math.Clamp(y + h / 2 - vh / 2, 0, imgH - vh);
        return (vx, vy, vw, vh);
    }
}
