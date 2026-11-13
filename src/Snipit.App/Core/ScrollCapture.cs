using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>
/// Scrolling capture (roadmap Phase 3): capture an element's full scrollable
/// content as one tall image. Strategy, in order:
/// 1. UIA ScrollPattern — read the view size, jump to planned offsets
///    (<see cref="ScrollStitch.Plan"/>), capture each viewport, compose exactly.
/// 2. Wheel fallback — scroll with synthetic wheel ticks and stitch strips by
///    their measured overlap (<see cref="ScrollStitch.FindOverlap"/>), stopping
///    when a strip adds no new rows.
/// All UIA calls run on the UIA worker thread; captures run on the UI thread
/// after the overlays have hidden. Bounded: at most 40 strips / 32,767 px tall.
/// </summary>
internal static class ScrollCapture
{
    private const int MaxStrips = 40;
    private const int MaxHeight = 32767;
    private const int SettleMs = 300;

    public static async Task<CaptureResult> CaptureAsync(AutomationElement element, RectPx viewport,
        UiaWorker uia, IReadOnlyList<MonitorInfo> monitors, uint matteArgb, CancellationToken ct)
    {
        var probe = await uia.RunAsync(() => Probe(element));
        if (probe is { ViewSizePercent: > 0 and < 99.9 } p)
            return await PatternCaptureAsync(p, viewport, uia, monitors, matteArgb, ct);
        return await WheelCaptureAsync(element, viewport, uia, monitors, matteArgb, ct);
    }

    private sealed record ProbeResult(AutomationElement Scrollable, double ViewSizePercent, double ScrollPercent);

    /// <summary>Find the nearest scrollable element (self or an ancestor) and its view metrics. UIA thread only.</summary>
    private static ProbeResult? Probe(AutomationElement element)
    {
        var walker = TreeWalker.ControlViewWalker;
        var el = element;
        for (int i = 0; i < 8 && el is not null; i++)
        {
            try
            {
                if (el.TryGetCurrentPattern(ScrollPattern.Pattern, out object pat) && pat is ScrollPattern sp
                    && sp.Current.VerticallyScrollable && sp.Current.VerticalViewSize > 0)
                    return new ProbeResult(el, sp.Current.VerticalViewSize, sp.Current.VerticalScrollPercent);
            }
            catch (ElementNotAvailableException) { return null; }
            el = walker.GetParent(el);
        }
        return null;
    }

    private static async Task<CaptureResult> PatternCaptureAsync(ProbeResult probe, RectPx viewport,
        UiaWorker uia, IReadOnlyList<MonitorInfo> monitors, uint matteArgb, CancellationToken ct)
    {
        int contentHeight = Math.Min(MaxHeight, (int)Math.Round(viewport.Height * 100.0 / probe.ViewSizePercent));
        int[] offsets = ScrollStitch.Plan(viewport.Height, contentHeight, 48);
        var strips = new List<ScrollStrip>(offsets.Length);
        try
        {
            foreach (int offset in offsets)
            {
                ct.ThrowIfCancellationRequested();
                double percent = contentHeight <= viewport.Height ? 0 : offset * 100.0 / (contentHeight - viewport.Height);
                await uia.RunAsync(() => { SetScroll(probe.Scrollable, percent); return true; });
                await Task.Delay(SettleMs, ct);
                var shot = CaptureEngine.Capture(viewport, monitors, matteArgb);
                strips.Add(new ScrollStrip(offset, viewport.Height, PixelsOf(shot.Image)));
            }
        }
        finally
        {
            // Leave the target scrolled the way we found it.
            try { await uia.RunAsync(() => { SetScroll(probe.Scrollable, probe.ScrollPercent); return true; }); } catch { /* target may be gone */ }
        }
        byte[] composed = ScrollStitch.Compose(viewport.Width, contentHeight, strips);
        return ToResult(composed, viewport, contentHeight);
    }

    private static void SetScroll(AutomationElement el, double percent)
    {
        if (el.TryGetCurrentPattern(ScrollPattern.Pattern, out object pat) && pat is ScrollPattern sp)
            sp.SetScrollPercent(ScrollPatternIdentifiers.NoScroll, percent);
    }

    private static async Task<CaptureResult> WheelCaptureAsync(AutomationElement element, RectPx viewport,
        UiaWorker uia, IReadOnlyList<MonitorInfo> monitors, uint matteArgb, CancellationToken ct)
    {
        try { await uia.RunAsync(() => { try { element.SetFocus(); } catch (ElementNotAvailableException) { } return true; }); }
        catch (InvalidOperationException) { }

        var first = CaptureEngine.Capture(viewport, monitors, matteArgb);
        byte[] acc = PixelsOf(first.Image);
        int accHeight = viewport.Height;
        int cx = viewport.X + viewport.Width / 2, cy = viewport.Y + viewport.Height / 2;

        for (int i = 1; i < MaxStrips && accHeight < MaxHeight; i++)
        {
            ct.ThrowIfCancellationRequested();
            NativeMethods.SetCursorPos(cx, cy);
            NativeMethods.Wheel(-5);
            await Task.Delay(SettleMs, ct);
            var shot = CaptureEngine.Capture(viewport, monitors, matteArgb);
            byte[] strip = PixelsOf(shot.Image);
            int overlap = ScrollStitch.FindOverlap(acc, accHeight, strip, viewport.Height, viewport.Width);
            int newRows = viewport.Height - overlap;
            if (newRows <= 0) break; // the view stopped moving: end of content
            int take = Math.Min(newRows, MaxHeight - accHeight);
            var grown = new byte[viewport.Width * (accHeight + take) * 4];
            Array.Copy(acc, grown, acc.Length);
            Array.Copy(strip, overlap * viewport.Width * 4, grown, acc.Length, take * viewport.Width * 4);
            acc = grown;
            accHeight += take;
        }
        return ToResult(acc, viewport, accHeight);
    }

    private static byte[] PixelsOf(BitmapSource image)
    {
        var fmt = image.Format == PixelFormats.Bgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[fmt.PixelWidth * fmt.PixelHeight * 4];
        fmt.CopyPixels(bytes, fmt.PixelWidth * 4, 0);
        return bytes;
    }

    private static CaptureResult ToResult(byte[] bgra, RectPx viewport, int height)
    {
        var bmp = BitmapSource.Create(viewport.Width, height, 96, 96, PixelFormats.Bgra32, null, bgra, viewport.Width * 4);
        bmp.Freeze();
        var rect = new RectPx(viewport.X, viewport.Y, viewport.Width, height);
        return new CaptureResult(bmp, rect, false, rect.Area / 1_000_000.0, 1);
    }
}
