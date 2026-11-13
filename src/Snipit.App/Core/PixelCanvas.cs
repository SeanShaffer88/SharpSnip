using Windows.Graphics.Capture;
using Snipit.Core;

namespace Snipit;

/// <summary>
/// Builds one composited pixel frame from a WGC frame: readback, crop to the
/// recording area, and the webcam overlay when one is running. Shared by the
/// MP4 recorder's pixel path and the GIF recorder.
/// </summary>
internal static class PixelCanvas
{
    public static byte[]? Compose(WgcStream stream, Direct3D11CaptureFrame frame, RectPx? crop,
        WebcamCapture? webcam, PipCorner corner, out int width, out int height)
    {
        width = 0; height = 0;
        var pixels = stream.ReadPixels(frame);
        if (pixels is null) return null;
        var area = crop ?? new RectPx(0, 0, stream.Size.Width, stream.Size.Height);
        var dst = new byte[area.Width * area.Height * 4];
        PixelBlit.Copy(pixels, stream.Size.Width * 4, area.X, area.Y, dst, area.Width * 4, 0, 0, area.Width, area.Height);
        if (webcam is not null && webcam.TryGetLatest(out var cam, out int camW, out int camH))
        {
            var dest = PipLayout.DestRect(area.Width, area.Height, camW, camH, corner);
            if (!dest.IsEmpty)
                PixelBlit.ScaleCopy(cam, camW * 4, camW, camH, dst, area.Width * 4, dest.X, dest.Y, dest.Width, dest.Height);
        }
        width = area.Width; height = area.Height;
        return dst;
    }
}
