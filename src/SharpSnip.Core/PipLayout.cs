namespace SharpSnip.Core;

public enum PipCorner { BottomRight, BottomLeft, TopRight, TopLeft }

/// <summary>Where the webcam picture-in-picture lands inside a recorded frame, and how big it is.</summary>
public static class PipLayout
{
    public static PipCorner ParseCorner(string? name) =>
        Enum.TryParse<PipCorner>(name, ignoreCase: true, out var c) ? c : PipCorner.BottomRight;

    /// <summary>
    /// The overlay rectangle: widthFraction of the frame wide (clamped to
    /// [64, half the frame]), the camera's aspect kept, both dimensions even,
    /// set into the chosen corner with a margin.
    /// </summary>
    public static RectPx DestRect(int frameWidth, int frameHeight, int camWidth, int camHeight,
        PipCorner corner, int margin = 16, double widthFraction = 0.22)
    {
        if (frameWidth < 8 || frameHeight < 8 || camWidth <= 0 || camHeight <= 0) return RectPx.Empty;
        int maxW = Math.Max(2, frameWidth / 2);
        int w = (int)Math.Round(frameWidth * widthFraction);
        w = Math.Clamp(w, Math.Min(64, maxW), maxW);
        w -= w % 2;
        int h = (int)Math.Round((double)w * camHeight / camWidth);
        h = Math.Max(2, Math.Min(h, frameHeight / 2));
        h -= h % 2;
        int x = corner is PipCorner.BottomLeft or PipCorner.TopLeft ? margin : frameWidth - margin - w;
        int y = corner is PipCorner.TopLeft or PipCorner.TopRight ? margin : frameHeight - margin - h;
        return new RectPx(Math.Max(0, x), Math.Max(0, y), w, h);
    }
}
