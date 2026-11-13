using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>
/// Transparent, click-through, topmost window covering exactly one monitor. It only draws; all input
/// arrives through <see cref="InputHook"/>. State is stored and re-rendered so DPI changes are harmless.
/// </summary>
internal sealed class OverlayWindow : Window
{
    private const int LoupePixels = 15, LoupeZoom = 8;

    private readonly OverlayStyle _style;
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly System.Windows.Shapes.Path _dim = new();
    private readonly Rectangle _highlight = new() { Visibility = Visibility.Collapsed };
    private IReadOnlyList<RectPx> _areas = Array.Empty<RectPx>();
    private readonly List<Rectangle> _areaRects = new();
    private readonly Rectangle _outline = new() { Visibility = Visibility.Collapsed };
    private readonly List<Rectangle> _handles = new();
    private readonly Border _hint, _hud, _chip, _sizeTag, _loupe;
    private readonly TextBlock _hintText, _hudText, _chipText, _sizeText;
    private readonly Image _loupeImage = new() { Width = LoupePixels * LoupeZoom, Height = LoupePixels * LoupeZoom };
    private readonly Border _bar = new() { Visibility = Visibility.Collapsed };
    private readonly StackPanel _barRow = new() { Orientation = Orientation.Horizontal };
    private IReadOnlyList<ModeBarButton>? _barButtons;
    private double _barCenter = ModeBarLayout.DefaultCenter;
    private int _barHover = -2;

    // Stored state (physical pixels).
    private bool _dimmed = true;
    private RectPx? _hole, _highlightRect, _adornRect;
    private bool _outlineOn, _handlesOn;
    private string? _hintMsg, _hudMsg, _chipMsg, _sizeMsg;
    private int _hudX, _hudY, _loupeX, _loupeY;
    private BitmapSource? _loupeSrc;
    private string? _loupeReadout;
    private readonly TextBlock _loupeText = new() { Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(4, 2, 4, 3), TextAlignment = TextAlignment.Center };
    private readonly System.Windows.Shapes.Polyline _trace = new() { StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round, Visibility = Visibility.Collapsed, IsHitTestVisible = false };

    public MonitorInfo Monitor { get; }
    public bool ExcludedFromCapture { get; private set; }

    public OverlayWindow(MonitorInfo monitor, OverlayStyle style)
    {
        Monitor = monitor;
        _style = style;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0;
        Width = monitor.Bounds.Width / monitor.Scale;
        Height = monitor.Bounds.Height / monitor.Scale;
        Title = "Snipit overlay";

        var accent = new SolidColorBrush(style.Accent);
        _dim.Fill = new SolidColorBrush(Color.FromArgb(style.DimAlpha, 0, 0, 0));
        _highlight.Stroke = accent;
        _highlight.Fill = new SolidColorBrush(Color.FromArgb(28, style.Accent.R, style.Accent.G, style.Accent.B));
        _outline.Stroke = accent;

        (_hint, _hintText) = MakeBubble(style.HighContrast);
        (_hud, _hudText) = MakeBubble(style.HighContrast);
        (_chip, _chipText) = MakeBubble(style.HighContrast);
        (_sizeTag, _sizeText) = MakeBubble(style.HighContrast);
        AutomationProperties.SetLiveSetting(_hintText, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_hintText, "Snipit hint");

        var loupeGrid = new Grid();
        RenderOptions.SetBitmapScalingMode(_loupeImage, BitmapScalingMode.NearestNeighbor);
        loupeGrid.Children.Add(_loupeImage);
        loupeGrid.Children.Add(new Rectangle
        {
            Width = LoupeZoom, Height = LoupeZoom, Stroke = Brushes.White, StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        _trace.Stroke = accent;
        var loupeStack = new StackPanel();
        loupeStack.Children.Add(loupeGrid);
        loupeStack.Children.Add(_loupeText);
        _loupe = new Border
        {
            BorderBrush = accent, BorderThickness = new Thickness(2), Background = Brushes.Black,
            Child = loupeStack, Visibility = Visibility.Collapsed,
        };

        for (int i = 0; i < 8; i++)
            _handles.Add(new Rectangle { Width = 8, Height = 8, Fill = Brushes.White, Stroke = accent, StrokeThickness = 1, Visibility = Visibility.Collapsed });

        bool hc = style.HighContrast;
        _bar.Background = hc ? Brushes.Black : Frozen(0x23, 0x23, 0x26);
        _bar.BorderBrush = hc ? Brushes.White : Frozen(0x43, 0x43, 0x4A);
        _bar.BorderThickness = new Thickness(1, 0, 1, 1);
        _bar.CornerRadius = new CornerRadius(0, 0, 12, 12);
        _bar.Padding = new Thickness(ModeBarLayout.PadX, 6, ModeBarLayout.PadX, 6);
        if (!hc) _bar.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 4, Direction = 270, Opacity = 0.4, Color = Colors.Black };
        _bar.Child = _barRow;

        _canvas.Children.Add(_dim);
        _canvas.Children.Add(_highlight);
        _canvas.Children.Add(_outline);
        _canvas.Children.Add(_trace);
        foreach (var h in _handles) _canvas.Children.Add(h);
        foreach (var b in new[] { _hint, _hud, _chip, _sizeTag, _loupe }) _canvas.Children.Add(b);
        _canvas.Children.Add(_bar);
        Content = _canvas;
    }

    private static (Border, TextBlock) MakeBubble(bool highContrast)
    {
        var tb = new TextBlock
        {
            Foreground = highContrast ? Brushes.Black : Brushes.White,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 13, TextWrapping = TextWrapping.NoWrap,
        };
        var b = new Border
        {
            Background = highContrast ? new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00)) : new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x14)),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), Child = tb, Visibility = Visibility.Collapsed,
        };
        return (b, tb);
    }

    private double Scale => IsLoaded ? VisualTreeHelper.GetDpi(this).DpiScaleX : Monitor.Scale;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.AddExStyle(hwnd, NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_LAYERED);
        // Keeps the overlay out of captured frames on Windows 10 2004+. Older builds fall back to hide-before-capture.
        ExcludedFromCapture = NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        Place();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Place();
        Render();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Place(); // WPF resizes on WM_DPICHANGED; force the exact physical bounds back.
        Render();
    }

    private void Place()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var b = Monitor.Bounds;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, NativeMethods.SWP_NOACTIVATE);
    }

    // ---- state setters -------------------------------------------------------------------------

    public void SetDim(bool dimmed, RectPx? hole) { _dimmed = dimmed; _hole = hole; Render(); }
    public void SetAreas(IReadOnlyList<RectPx> areas) { _areas = areas; Render(); }
    public void SetHighlight(RectPx? r) { _highlightRect = r; Render(); }
    public void SetOutline(bool on) { _outlineOn = on; Render(); }
    public void SetHint(string? text) { _hintMsg = text; Render(); AnnounceHint(); }
    public void SetHud(string? text, int cx, int cy) { _hudMsg = text; _hudX = cx; _hudY = cy; Render(); }
    public void SetChip(string? text) { _chipMsg = text; Render(); }
    public void SetAdorn(RectPx? r, bool handles, string? sizeText) { _adornRect = r; _handlesOn = handles; _sizeMsg = sizeText; Render(); }
    public void SetLoupe(BitmapSource? src, int cx, int cy, string? readout = null) { _loupeSrc = src; _loupeX = cx; _loupeY = cy; _loupeReadout = readout; Render(); }

    public void SetTrace(IReadOnlyList<(int X, int Y)>? points)
    {
        if (points is null || points.Count < 2) { _trace.Visibility = Visibility.Collapsed; return; }
        _trace.Points = new PointCollection(points.Select(p => Local(p.X, p.Y)));
        _trace.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// The mode bar (top-centre tab). Null hides it. Showing it after it was hidden
    /// slides it down from the top edge; hover-only updates do not re-animate.
    /// </summary>
    public void SetModeBar(IReadOnlyList<ModeBarButton>? buttons, int hover, double center = ModeBarLayout.DefaultCenter)
    {
        _barCenter = center;
        bool animateIn = buttons is not null && _barButtons is null;
        _barButtons = buttons;
        _barHover = hover;
        if (buttons is null) { _bar.Visibility = Visibility.Collapsed; return; }

        bool hc = _style.HighContrast;
        Brush ink = hc ? Brushes.White : Frozen(0xE6, 0xE3, 0xE1);
        Brush muted = hc ? Brushes.White : Frozen(0x89, 0x86, 0x8A);
        Brush hoverBg = hc ? Brushes.White : Frozen(0x28, 0x28, 0x2D);
        Brush hoverInk = hc ? Brushes.Black : ink;
        const double h = ModeBarLayout.CellHeight;

        _barRow.Children.Clear();
        bool gripHot = hover == -3;
        var dots = new Canvas { Width = 8, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (int d = 0; d < 6; d++)
        {
            var dot = new Ellipse { Width = 2.4, Height = 2.4, Fill = gripHot ? hoverInk : muted };
            Canvas.SetLeft(dot, d % 2 * 5.6); Canvas.SetTop(dot, d / 2 * 5.8);
            dots.Children.Add(dot);
        }
        var grip = new Border
        {
            Width = ModeBarLayout.GripWidth, Height = h, CornerRadius = new CornerRadius(8),
            Background = gripHot ? hoverBg : Brushes.Transparent, Child = dots,
        };
        AutomationProperties.SetName(grip, "Move capture bar: drag along the top edge");
        _barRow.Children.Add(grip);
        for (int i = 0; i < buttons.Count; i++)
        {
            bool hot = hover == i;
            var fg = hot ? hoverInk : ink;
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var icon = (FrameworkElement)ModeBarIcons.Create(buttons[i].Icon, fg);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            inner.Children.Add(icon);
            inner.Children.Add(new TextBlock
            {
                Text = buttons[i].Label, FontFamily = new FontFamily("Segoe UI"), FontSize = 11,
                Foreground = fg, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
            });
            var grid = new Grid();
            grid.Children.Add(inner);
            if (buttons[i].Key.Length > 0)
                grid.Children.Add(new TextBlock
                {
                    Text = buttons[i].Key, FontFamily = new FontFamily("Consolas"), FontSize = 10,
                    Foreground = hot ? hoverInk : muted, HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 5, 0),
                });
            var cell = new Border
            {
                Width = ModeBarLayout.ButtonWidth, Height = h, CornerRadius = new CornerRadius(8),
                Background = hot ? hoverBg : Brushes.Transparent, Child = grid,
            };
            AutomationProperties.SetName(cell, buttons[i].AutomationName);
            _barRow.Children.Add(cell);
        }
        _barRow.Children.Add(new Border
        {
            Width = 1, Height = h - 12, Margin = new Thickness(4, 6, 4, 6),
            Background = hc ? Brushes.White : Frozen(0x43, 0x43, 0x4A),
        });
        bool closeHot = hover == -1;
        var closeFg = closeHot ? hoverInk : ink;
        var closeStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var closeIcon = (FrameworkElement)ModeBarIcons.Create(ModeBarIcon.Close, closeFg);
        closeIcon.HorizontalAlignment = HorizontalAlignment.Center;
        closeStack.Children.Add(closeIcon);
        closeStack.Children.Add(new TextBlock
        {
            Text = "Esc", FontFamily = new FontFamily("Consolas"), FontSize = 10,
            Foreground = closeHot ? hoverInk : muted, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0),
        });
        var closeCell = new Border
        {
            Width = ModeBarLayout.CloseWidth, Height = h, CornerRadius = new CornerRadius(8),
            Background = closeHot ? hoverBg : Brushes.Transparent, Child = closeStack,
        };
        AutomationProperties.SetName(closeCell, "Close (Esc)");
        _barRow.Children.Add(closeCell);

        _bar.Visibility = Visibility.Visible;
        PositionBar();
        if (animateIn && SystemParameters.ClientAreaAnimation)
        {
            var slide = new TranslateTransform(0, -ModeBarLayout.BarHeight);
            _bar.RenderTransform = slide;
            slide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(-ModeBarLayout.BarHeight, 0, TimeSpan.FromMilliseconds(180))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        else _bar.RenderTransform = null;
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    private void PositionBar()
    {
        if (_barButtons is null) return;
        double w = Monitor.Bounds.Width / Scale;
        Canvas.SetLeft(_bar, ModeBarLayout.Left(_barCenter, w, ModeBarLayout.BarWidth(_barButtons.Count)));
        Canvas.SetTop(_bar, 0);
    }

    private void AnnounceHint()
    {
        if (_hintMsg is null || !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)) return;
        UIElementAutomationPeer.CreatePeerForElement(_hintText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    // ---- rendering -----------------------------------------------------------------------------

    private Rect Local(RectPx r)
    {
        double s = Scale; var b = Monitor.Bounds;
        return new Rect((r.X - b.X) / s, (r.Y - b.Y) / s, r.Width / s, r.Height / s);
    }

    private Point Local(int px, int py)
    {
        double s = Scale; var b = Monitor.Bounds;
        return new Point((px - b.X) / s, (py - b.Y) / s);
    }

    private void Render()
    {
        double s = Scale;
        var b = Monitor.Bounds;
        double w = b.Width / s, h = b.Height / s;
        double thick = _style.ThicknessPx / s;

        // dimmer with optional hole
        if (_dimmed)
        {
            Geometry g = new RectangleGeometry(new Rect(0, 0, w, h));
            if (_hole is { } hole)
            {
                var inter = hole.Intersect(b);
                if (!inter.IsEmpty) g = new CombinedGeometry(GeometryCombineMode.Exclude, g, new RectangleGeometry(Local(inter)));
            }
            foreach (var a in _areas)
            {
                var ai = a.Intersect(b);
                if (!ai.IsEmpty) g = new CombinedGeometry(GeometryCombineMode.Exclude, g, new RectangleGeometry(Local(ai)));
            }
            _dim.Data = g;
            _dim.Visibility = Visibility.Visible;
        }
        else _dim.Visibility = Visibility.Collapsed;

        // highlight rectangle
        if (_highlightRect is { } hr && hr.IntersectsWith(b))
        {
            var lr = Local(hr);
            _highlight.StrokeThickness = thick;
            _highlight.Width = Math.Max(lr.Width, 1); _highlight.Height = Math.Max(lr.Height, 1);
            Canvas.SetLeft(_highlight, lr.X); Canvas.SetTop(_highlight, lr.Y);
            _highlight.Visibility = Visibility.Visible;
        }
        else _highlight.Visibility = Visibility.Collapsed;

        // areas already added (multi-area capture)
        while (_areaRects.Count < _areas.Count)
        {
            var ar = new Rectangle { Stroke = _highlight.Stroke, Fill = Brushes.Transparent, Visibility = Visibility.Collapsed };
            _areaRects.Add(ar);
            _canvas.Children.Insert(_canvas.Children.IndexOf(_highlight), ar);
        }
        for (int i = 0; i < _areaRects.Count; i++)
        {
            var ar = _areaRects[i];
            if (i < _areas.Count && _areas[i].IntersectsWith(b))
            {
                var la = Local(_areas[i]);
                ar.StrokeThickness = thick;
                ar.Width = Math.Max(la.Width, 1); ar.Height = Math.Max(la.Height, 1);
                Canvas.SetLeft(ar, la.X); Canvas.SetTop(ar, la.Y);
                ar.Visibility = Visibility.Visible;
            }
            else ar.Visibility = Visibility.Collapsed;
        }

        // full-monitor outline (full-screen picker)
        if (_outlineOn)
        {
            _outline.StrokeThickness = thick;
            _outline.Width = Math.Max(w - thick, 1); _outline.Height = Math.Max(h - thick, 1);
            Canvas.SetLeft(_outline, thick / 2); Canvas.SetTop(_outline, thick / 2);
            _outline.Visibility = Visibility.Visible;
        }
        else _outline.Visibility = Visibility.Collapsed;

        // region handles + size tag
        RenderAdorn(s, w, h);

        // bubbles
        Place(_hint, _hintText, _hintMsg, null, w, h, bottomCenter: true);
        Place(_chip, _chipText, _chipMsg, new Point(16, 16), w, h);
        if (_hudMsg is not null && Monitor.Bounds.Contains(_hudX, _hudY))
        {
            var p = Local(_hudX, _hudY);
            Place(_hud, _hudText, _hudMsg, new Point(p.X + 18, p.Y + 22), w, h, clampFlip: true);
        }
        else _hud.Visibility = Visibility.Collapsed;

        PositionBar();

        // magnifier
        if (_loupeSrc is not null && Monitor.Bounds.Contains(_loupeX, _loupeY))
        {
            _loupeImage.Source = _loupeSrc;
            _loupeText.Text = _loupeReadout ?? "";
            _loupeText.Visibility = _loupeReadout is null ? Visibility.Collapsed : Visibility.Visible;
            var p = Local(_loupeX, _loupeY);
            _loupe.Visibility = Visibility.Visible;
            _loupe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var sz = _loupe.DesiredSize;
            double x = p.X + 24, y = p.Y + 24;
            if (x + sz.Width > w) x = p.X - 24 - sz.Width;
            if (y + sz.Height > h) y = p.Y - 24 - sz.Height;
            Canvas.SetLeft(_loupe, x); Canvas.SetTop(_loupe, y);
        }
        else _loupe.Visibility = Visibility.Collapsed;
    }

    private void RenderAdorn(double s, double w, double h)
    {
        var b = Monitor.Bounds;
        if (_adornRect is { } r && r.IntersectsWith(b))
        {
            var lr = Local(r);
            if (_handlesOn)
            {
                double[] xs = { lr.Left, lr.Left + lr.Width / 2, lr.Right };
                double[] ys = { lr.Top, lr.Top + lr.Height / 2, lr.Bottom };
                int i = 0;
                for (int yi = 0; yi < 3; yi++)
                    for (int xi = 0; xi < 3; xi++)
                    {
                        if (xi == 1 && yi == 1) continue;
                        var hd = _handles[i++];
                        Canvas.SetLeft(hd, xs[xi] - 4); Canvas.SetTop(hd, ys[yi] - 4);
                        hd.Visibility = Visibility.Visible;
                    }
            }
            else foreach (var hd in _handles) hd.Visibility = Visibility.Collapsed;

            if (_sizeMsg is not null)
            {
                _sizeText.Text = _sizeMsg;
                _sizeTag.Visibility = Visibility.Visible;
                _sizeTag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var sz = _sizeTag.DesiredSize;
                double x = Math.Clamp(lr.Left, 0, Math.Max(0, w - sz.Width));
                double y = lr.Top - sz.Height - 6;
                if (y < 0) y = Math.Min(lr.Top + 6, Math.Max(0, h - sz.Height));
                Canvas.SetLeft(_sizeTag, x); Canvas.SetTop(_sizeTag, y);
            }
            else _sizeTag.Visibility = Visibility.Collapsed;
        }
        else
        {
            foreach (var hd in _handles) hd.Visibility = Visibility.Collapsed;
            _sizeTag.Visibility = Visibility.Collapsed;
        }
    }

    private static void Place(Border bubble, TextBlock tb, string? msg, Point? at, double w, double h,
                              bool bottomCenter = false, bool clampFlip = false)
    {
        if (msg is null) { bubble.Visibility = Visibility.Collapsed; return; }
        tb.Text = msg;
        bubble.Visibility = Visibility.Visible;
        bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var sz = bubble.DesiredSize;
        double x, y;
        if (bottomCenter) { x = (w - sz.Width) / 2; y = h - sz.Height - 48; }
        else
        {
            x = at!.Value.X; y = at.Value.Y;
            if (clampFlip)
            {
                if (x + sz.Width > w) x = Math.Max(0, at.Value.X - 36 - sz.Width);
                if (y + sz.Height > h) y = Math.Max(0, at.Value.Y - 44 - sz.Height);
            }
        }
        Canvas.SetLeft(bubble, Math.Max(0, x)); Canvas.SetTop(bubble, Math.Max(0, y));
    }
}
