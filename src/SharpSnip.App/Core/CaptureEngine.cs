using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

internal sealed record CaptureResult(BitmapSource Image, RectPx Rect, bool HasGaps, double Megapixels, int MonitorsTouched);

/// <summary>
/// GDI BitBlt capture (§8). Each monitor's slice is copied 1:1 at native resolution into one
/// physical-pixel canvas, so mixed DPI needs no scaling. Gaps get the matte colour.
/// Windows.Graphics.Capture is NOT implemented yet (see AUDIT.md).
/// </summary>
internal static class CaptureEngine
{
    private const long MaxBytes = 1_500_000_000;

    public static CaptureResult Capture(RectPx rect, IReadOnlyList<MonitorInfo> monitors, uint matteArgb)
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
