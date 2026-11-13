using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Snipit.Core;

namespace Snipit;

/// <summary>One overlay per monitor (§2.2) plus helpers so pickers address them in physical pixels.</summary>
internal sealed class OverlayHost : IDisposable
{
    private readonly List<OverlayWindow> _wins = new();
    private readonly OverlayStyle _style;
    private bool _disposed;

    public OverlayHost(IReadOnlyList<MonitorInfo> monitors, OverlayStyle style)
    {
        _style = style;
        foreach (var m in monitors) _wins.Add(new OverlayWindow(m, style));
    }

    public bool AllExcludedFromCapture => _wins.Count > 0 && _wins.All(w => w.ExcludedFromCapture);

    public void Show() { foreach (var w in _wins) w.Show(); }

    public void HideAll() { foreach (var w in _wins) w.Visibility = Visibility.Hidden; }

    public void ShowAll() { foreach (var w in _wins) w.Visibility = Visibility.Visible; }

    /// <summary>
    /// Hide before the frame is pulled (§4.1). When every overlay is excluded from capture by the OS
    /// there is nothing to wait for; otherwise fade 80 ms and give the compositor one frame to flush.
    /// </summary>
    public async Task HideForCaptureAsync()
    {
        if (_wins.Count == 0 || _wins.All(w => w.Visibility != Visibility.Visible)) return;
        if (AllExcludedFromCapture) { HideAll(); return; }

        var tcs = new TaskCompletionSource();
        int pending = _wins.Count;
        foreach (var w in _wins)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
            anim.Completed += (_, _) => { if (--pending == 0) tcs.TrySetResult(); };
            w.BeginAnimation(UIElement.OpacityProperty, anim);
        }
        await tcs.Task;
        HideAll();
        foreach (var w in _wins) { w.BeginAnimation(UIElement.OpacityProperty, null); w.Opacity = 1; }
        await Task.Delay(40);
    }

    public void Dim(Func<MonitorInfo, bool> dimmed, RectPx? hole)
    {
        foreach (var w in _wins) w.SetDim(dimmed(w.Monitor), hole);
    }

    public void Highlight(RectPx? r) { foreach (var w in _wins) w.SetHighlight(r); }

    public void Outline(Func<MonitorInfo, bool> on) { foreach (var w in _wins) w.SetOutline(on(w.Monitor)); }

    public void Chips(Func<MonitorInfo, string?> text) { foreach (var w in _wins) w.SetChip(text(w.Monitor)); }

    /// <summary>Hint bar goes on the monitor containing the point; all others clear it.</summary>
    public void HintAt(string? text, int x, int y)
    {
        var target = VirtualDesktop.MonitorAt(_wins.Select(w => w.Monitor).ToList(), x, y);
        foreach (var w in _wins) w.SetHint(w.Monitor == target ? text : null);
    }

    public void HintOn(string? text, MonitorInfo monitor)
    {
        foreach (var w in _wins) w.SetHint(w.Monitor.Index == monitor.Index ? text : null);
    }

    public void Hud(string? text, int x, int y) { foreach (var w in _wins) w.SetHud(text, x, y); }

    /// <summary>The mode bar lives on the monitor containing (x, y) — the menu's anchor point; all others hide theirs.</summary>
    public void ModeBar(IReadOnlyList<ModeBarButton>? buttons, int hover, int x, int y, double center = ModeBarLayout.DefaultCenter)
    {
        var target = VirtualDesktop.MonitorAt(_wins.Select(w => w.Monitor).ToList(), x, y);
        foreach (var w in _wins) w.SetModeBar(buttons is not null && w.Monitor == target ? buttons : null, hover, center);
    }

    public void Adorn(RectPx? r, bool handles, string? sizeText) { foreach (var w in _wins) w.SetAdorn(r, handles, sizeText); }

    /// <summary>Magnifier: a 15x15 px crop of the frozen frame, drawn 8x, next to the cursor, with a colour readout.</summary>
    public void Loupe(BitmapSource? frozen, RectPx frozenRect, int cx, int cy)
    {
        BitmapSource? crop = null;
        string? readout = null;
        if (frozen is not null)
        {
            const int n = 15;
            int sx = Math.Clamp(cx - frozenRect.X - n / 2, 0, Math.Max(0, frozen.PixelWidth - n));
            int sy = Math.Clamp(cy - frozenRect.Y - n / 2, 0, Math.Max(0, frozen.PixelHeight - n));
            if (frozen.PixelWidth >= n && frozen.PixelHeight >= n)
            {
                crop = new CroppedBitmap(frozen, new Int32Rect(sx, sy, n, n));
                crop.Freeze();
            }
            int px = cx - frozenRect.X, py = cy - frozenRect.Y;
            if (px >= 0 && py >= 0 && px < frozen.PixelWidth && py < frozen.PixelHeight)
            {
                try
                {
                    var src = frozen.Format == System.Windows.Media.PixelFormats.Bgra32
                        ? frozen
                        : new FormatConvertedBitmap(frozen, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    var one = new byte[4];
                    src.CopyPixels(new Int32Rect(px, py, 1, 1), one, 4, 0);
                    readout = ColorReadout.Format(one[2], one[1], one[0], cx, cy);
                }
                catch { /* the readout is a nicety; the loupe still works without it */ }
            }
        }
        foreach (var w in _wins) w.SetLoupe(crop, cx, cy, readout);
    }

    /// <summary>Freehand outline being drawn, in virtual-desktop pixels (null clears it).</summary>
    public void Trace(IReadOnlyList<(int X, int Y)>? points) { foreach (var w in _wins) w.SetTrace(points); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var w in _wins) { try { w.Close(); } catch (InvalidOperationException) { } }
        _wins.Clear();
    }
}
