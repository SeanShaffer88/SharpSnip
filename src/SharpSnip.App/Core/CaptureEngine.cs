using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

internal sealed record CaptureResult(BitmapSource Image, RectPx Rect, bool HasGaps, double Megapixels, int MonitorsTouched);

/// <summary>
/// Still capture (§8). Windows.Graphics.Capture first — it keeps working where GDI
/// BitBlt returns black (hardware overlays, DRM video, some games) — with the GDI
/// path kept as an automatic fallback. Either way each monitor's slice lands 1:1
/// at native resolution in one physical-pixel canvas, so mixed DPI needs no
/// scaling. Gaps get the matte colour.
/// </summary>
internal static class CaptureEngine
{
    private const long MaxBytes = 1_500_000_000;

    public static CaptureResult Capture(RectPx rect, IReadOnlyList<MonitorInfo> monitors, uint matteArgb)
    {
        if (WgcCapture.IsSupported)
        {
            try
            {
                var result = CaptureWgc(rect, monitors, matteArgb);
                Log.Info("capture backend: Windows.Graphics.Capture");
                return result;
            }
            catch (Exception ex) { Log.Error("WGC capture failed; falling back to GDI", ex); }
        }
        var gdi = CaptureGdi(rect, monitors, matteArgb);
        Log.Info("capture backend: GDI");
        return gdi;
    }

    private static CaptureResult CaptureWgc(RectPx rect, IReadOnlyList<MonitorInfo> monitors, uint matteArgb)
    {
        if (rect.IsEmpty) throw new ArgumentException("Capture rectangle is empty.", nameof(rect));
        long bytes = rect.Area * 4;
        if (bytes > MaxBytes) throw new InvalidOperationException($"Capture of {rect.Width}x{rect.Height} is too large.");

        var places = VirtualDesktop.Placements(rect, monitors);
        if (places.Count == 0) throw new InvalidOperationException("Capture rectangle is outside every monitor.");
        bool gaps = VirtualDesktop.HasGaps(rect, monitors);

        var frames = new Dictionary<int, (byte[] Bgra, int Width, int Height)>();
        foreach (var p in places)
        {
            if (frames.ContainsKey(p.Monitor.Index)) continue;
            var shot = WgcCapture.TryCaptureMonitor(p.Monitor)
                ?? throw new InvalidOperationException($"WGC served no frame for monitor {p.Monitor.Index}.");
            if (shot.Width != p.Monitor.Bounds.Width || shot.Height != p.Monitor.Bounds.Height)
                throw new InvalidOperationException($"WGC frame size {shot.Width}x{shot.Height} does not match monitor {p.Monitor.Index} bounds.");
            frames[p.Monitor.Index] = shot;
        }

        var pixels = new byte[rect.Width * rect.Height * 4];
        foreach (var p in places)
        {
            var shot = frames[p.Monitor.Index];
            PixelBlit.Copy(shot.Bgra, shot.Width * 4, p.Source.X - p.Monitor.Bounds.X, p.Source.Y - p.Monitor.Bounds.Y,
                           pixels, rect.Width * 4, p.DestX, p.DestY, p.Source.Width, p.Source.Height);
        }
        CapturePixels.ApplyAlpha(pixels, rect.Width, places, gaps, matteArgb);

        var bmp = BitmapSource.Create(rect.Width, rect.Height, 96, 96, PixelFormats.Bgra32, null, pixels, rect.Width * 4);
        bmp.Freeze();
        return new CaptureResult(bmp, rect, gaps, rect.Area / 1_000_000.0, places.Count);
    }

    private static CaptureResult CaptureGdi(RectPx rect, IReadOnlyList<MonitorInfo> monitors, uint matteArgb)
    {
        if (rect.IsEmpty) throw new ArgumentException("Capture rectangle is empty.", nameof(rect));
        long bytes = rect.Area * 4;
        if (bytes > MaxBytes) throw new InvalidOperationException($"Capture of {rect.Width}x{rect.Height} is too large.");

        var places = VirtualDesktop.Placements(rect, monitors);
        if (places.Count == 0) throw new InvalidOperationException("Capture rectangle is outside every monitor.");
        bool gaps = VirtualDesktop.HasGaps(rect, monitors);

        IntPtr screen = NativeMethods.GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) throw new InvalidOperationException("GetDC failed.");
        IntPtr mem = IntPtr.Zero, dib = IntPtr.Zero, old = IntPtr.Zero;
        try
        {
            mem = NativeMethods.CreateCompatibleDC(screen);
            var bmi = new NativeMethods.BITMAPINFO
            {
                bmiHeader = new NativeMethods.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                    biWidth = rect.Width, biHeight = -rect.Height, biPlanes = 1, biBitCount = 32,
                },
            };
            dib = NativeMethods.CreateDIBSection(screen, ref bmi, NativeMethods.DIB_RGB_COLORS, out IntPtr bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection failed.");
            old = NativeMethods.SelectObject(mem, dib);

            foreach (var p in places)
            {
                if (!NativeMethods.BitBlt(mem, p.DestX, p.DestY, p.Source.Width, p.Source.Height,
                        screen, p.Source.X, p.Source.Y, NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT))
                    throw new InvalidOperationException($"BitBlt failed for monitor {p.Monitor.Index}.");
            }

            // One buffer end to end: DIB bytes -> alpha/matte fix-up (Core, byte-tested) -> bitmap.
            var pixels = new byte[rect.Width * rect.Height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            CapturePixels.ApplyAlpha(pixels, rect.Width, places, gaps, matteArgb);

            var bmp = BitmapSource.Create(rect.Width, rect.Height, 96, 96, PixelFormats.Bgra32, null, pixels, rect.Width * 4);
            bmp.Freeze();
            return new CaptureResult(bmp, rect, gaps, rect.Area / 1_000_000.0, places.Count);
        }
        finally
        {
            if (old != IntPtr.Zero) NativeMethods.SelectObject(mem, old);
            if (dib != IntPtr.Zero) NativeMethods.DeleteObject(dib);
            if (mem != IntPtr.Zero) NativeMethods.DeleteDC(mem);
            NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        }
    }

}
