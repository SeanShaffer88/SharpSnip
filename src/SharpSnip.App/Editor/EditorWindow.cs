using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

/// <summary>Mini editor (§9). Annotations live in a canvas above the image; undo/redo is an action stack.</summary>
internal sealed class EditorWindow : Window
{
    private enum Tool { Pen, Highlighter, Arrow, Rect, Ellipse, Text, Blur, Crop }

    private interface IEdit { void Undo(); void Redo(); }

    private sealed class AddEdit : IEdit
    {
        private readonly Canvas _c; private readonly UIElement _e;
        public AddEdit(Canvas c, UIElement e) { _c = c; _e = e; }
        public void Undo() => _c.Children.Remove(_e);
        public void Redo() { if (!_c.Children.Contains(_e)) _c.Children.Add(_e); }
    }

    private sealed class CropEdit : IEdit
    {
        private readonly EditorWindow _w; private readonly BitmapSource _oldBase, _newBase; private readonly List<UIElement> _oldItems;
        public CropEdit(EditorWindow w, BitmapSource oldBase, BitmapSource newBase, List<UIElement> oldItems)
        { _w = w; _oldBase = oldBase; _newBase = newBase; _oldItems = oldItems; }
        public void Undo() { _w.SetBase(_oldBase); _w._layer.Children.Clear(); foreach (var e in _oldItems) _w._layer.Children.Add(e); }
        public void Redo() { _w.SetBase(_newBase); _w._layer.Children.Clear(); }
    }

    private readonly Preferences _prefs;
    private readonly Action _persist;
    private readonly Action<string, string, string?> _notify;
    private readonly CaptureMeta _meta;
    private readonly Image _image = new();
    private readonly Canvas _layer = new() { Background = Brushes.Transparent };
    private readonly Grid _surface = new();
    private readonly Stack<IEdit> _undo = new(), _redo = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4, 8, 4) };
    private readonly Button _openFolder = new() { Content = "Open folder", Visibility = Visibility.Collapsed, Margin = new Thickness(4, 0, 4, 0), ToolTip = "Show the saved file in Explorer." };
    private readonly Button _copyPath = new() { Content = "Copy path", Visibility = Visibility.Collapsed, Margin = new Thickness(4, 0, 4, 0), ToolTip = "Copy the saved file's full path to the clipboard." };
    private readonly ComboBox _color = new() { Width = 90, ToolTip = "Annotation colour." };
    private readonly Slider _thickness = new() { Minimum = 1, Maximum = 20, Value = 4, Width = 100, ToolTip = "Stroke thickness." };

    private BitmapSource _base;
    private Tool _tool = Tool.Pen;
    private Point _start;
    private Shape? _draft;
    private Polyline? _stroke;
    private TextBox? _editing;
    private string? _savedPath;

    private static readonly (string Name, Color Color)[] Palette =
    {
        ("Red", Colors.Red), ("Yellow", Colors.Gold), ("Green", Colors.LimeGreen), ("Blue", Color.FromRgb(0x2D, 0x9C, 0xFF)),
        ("Black", Colors.Black), ("White", Colors.White),
    };

    public EditorWindow(CaptureResult cap, CaptureMeta meta, int monitorCount, int delay, Preferences prefs, Action persist, Action<string, string, string?> notify)
    {
        _prefs = prefs; _persist = persist; _notify = notify; _meta = meta; _base = cap.Image;
        Title = "SharpSnip";
        Width = 1100; Height = 720; MinWidth = 640; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AutomationProperties_Name(this, "SharpSnip editor");

        var root = new DockPanel();
        root.Children.Add(BuildToolbar()); DockPanel.SetDock(root.Children[0], Dock.Top);
        var statusBar = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) };
        var statusButtons = new StackPanel { Orientation = Orientation.Horizontal };
        statusButtons.Children.Add(_openFolder); statusButtons.Children.Add(_copyPath);
        DockPanel.SetDock(statusButtons, Dock.Right);
        statusBar.Children.Add(statusButtons); statusBar.Children.Add(_status);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(statusBar);
        var side = BuildMetaPanel(cap, meta, monitorCount, delay);
        DockPanel.SetDock(side, Dock.Right);
        root.Children.Add(side);

        _surface.Children.Add(_image); _surface.Children.Add(_layer);
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        SetBase(_base);
        root.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = _surface, Margin = new Thickness(8), Focusable = false });
        Content = root;

        _layer.MouseLeftButtonDown += OnDown;
        _layer.MouseMove += OnMove;
        _layer.MouseLeftButtonUp += OnUp;
        _openFolder.Click += (_, _) => { if (_savedPath is not null) Process.Start("explorer.exe", $"/select,\"{_savedPath}\""); };
        _copyPath.Click += (_, _) => { if (_savedPath is not null) { try { Clipboard.SetText(_savedPath); _status.Text = "Path copied."; } catch (Exception) { _status.Text = "Clipboard busy."; } } };

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => Save(), (_, e) => e.CanExecute = _editing is null));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, _) => CopyImage(), (_, e) => e.CanExecute = _editing is null));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (_, _) => DoUndo(), (_, e) => e.CanExecute = _editing is null && _undo.Count > 0));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (_, _) => DoRedo(), (_, e) => e.CanExecute = _editing is null && _redo.Count > 0));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _editing is null) { Close(); e.Handled = true; } };

        _status.Text = meta.Mode + (cap.HasGaps ? " · monitor layout has gaps (matte fill)" : "");
    }

    private static void AutomationProperties_Name(DependencyObject d, string name) => System.Windows.Automation.AutomationProperties.SetName(d, name);

    // ---- UI construction -----------------------------------------------------------------------

    private UIElement BuildToolbar()
    {
        var bar = new WrapPanel { Margin = new Thickness(6), Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xF7)) };
        var group = new List<ToggleButton>();
        void AddTool(string label, Tool t, string tip)
        {
            var b = new ToggleButton { Content = label, Margin = new Thickness(2), Padding = new Thickness(8, 3, 8, 3), ToolTip = tip, IsChecked = t == _tool };
            b.Click += (_, _) => { _tool = t; foreach (var g in group) g.IsChecked = g == b; };
            group.Add(b); bar.Children.Add(b);
        }
        AddTool("Pen", Tool.Pen, "Freehand pen. Drag on the image.");
        AddTool("Highlighter", Tool.Highlighter, "Translucent wide marker.");
        AddTool("Arrow", Tool.Arrow, "Drag from tail to head.");
        AddTool("Rect", Tool.Rect, "Drag a rectangle outline.");
        AddTool("Ellipse", Tool.Ellipse, "Drag an ellipse outline.");
        AddTool("Text", Tool.Text, "Click to type. Enter commits, Esc discards.");
        AddTool("Blur", Tool.Blur, "Drag over sensitive content to pixelate it.");
        AddTool("Crop", Tool.Crop, "Drag the area to keep. Applies immediately; Undo restores.");

        foreach (var (name, _) in Palette) _color.Items.Add(name);
        _color.SelectedIndex = 0;
        bar.Children.Add(new TextBlock { Text = "Colour", Margin = new Thickness(10, 4, 4, 0) }); bar.Children.Add(_color);
        bar.Children.Add(new TextBlock { Text = "Size", Margin = new Thickness(10, 4, 4, 0) }); bar.Children.Add(_thickness);

        Button Act(string label, string tip, Action a)
        {
            var b = new Button { Content = label, Margin = new Thickness(2), Padding = new Thickness(8, 3, 8, 3), ToolTip = tip };
            b.Click += (_, _) => a(); bar.Children.Add(b); return b;
        }
        bar.Children.Add(new Separator { Margin = new Thickness(6, 0, 6, 0) });
        Act("Undo", "Undo last edit (Ctrl+Z).", DoUndo);
        Act("Redo", "Redo (Ctrl+Y).", DoRedo);
        Act("Copy", "Copy the edited image to the clipboard (Ctrl+C).", CopyImage);
        Act("Save", "Save to the output folder (Ctrl+S).", Save);
        Act("Reveal", "Save if needed, then show the file in Explorer. (The Windows share sheet is not implemented yet.)", () => { if (_savedPath is null) Save(); if (_savedPath is not null) Process.Start("explorer.exe", $"/select,\"{_savedPath}\""); });
        return bar;
    }

    private UIElement BuildMetaPanel(CaptureResult cap, CaptureMeta m, int monitors, int delay)
    {
        var lines = new List<string>
        {
            $"Mode: {m.Mode}",
            $"Size: {cap.Image.PixelWidth} × {cap.Image.PixelHeight} px",
            $"Rect (physical px): {cap.Rect}",
            $"Monitors in layout: {monitors}",
            $"Delay used: {(delay > 0 ? delay + " s" : "none")}",
        };
        if (m.App is not null) lines.Add($"App: {m.App}");
        if (m.ControlType is not null) lines.Add($"UIA type: {m.ControlType}");
        if (!string.IsNullOrEmpty(m.ElementName)) lines.Add($"Name: {m.ElementName}");
        if (!string.IsNullOrEmpty(m.AutomationId)) lines.Add($"AutomationId: {m.AutomationId}");
        if (!string.IsNullOrEmpty(m.ClassName)) lines.Add($"Class: {m.ClassName}");
        if (!string.IsNullOrEmpty(m.UiaPath)) lines.Add($"Path: {m.UiaPath}");
        var tb = new TextBox
        {
            Text = string.Join(Environment.NewLine, lines), IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0), Background = Brushes.Transparent, Margin = new Thickness(8),
            ToolTip = "Capture details, handy for bug reports. Select and copy as needed.",
        };
        System.Windows.Automation.AutomationProperties.SetName(tb, "Capture metadata");
        return new Border { Width = 260, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1, 0, 0, 0), Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = tb } };
    }

    private void SetBase(BitmapSource src)
    {
        _base = src;
        _image.Source = src;
        _image.Width = src.PixelWidth; _image.Height = src.PixelHeight;
        _surface.Width = src.PixelWidth; _surface.Height = src.PixelHeight;
        _layer.Width = src.PixelWidth; _layer.Height = src.PixelHeight;
    }

    // ---- drawing -------------------------------------------------------------------------------

    private Color CurrentColor => Palette[Math.Max(0, _color.SelectedIndex)].Color;
    private Brush Stroke(double alpha = 1) => new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), CurrentColor.R, CurrentColor.G, CurrentColor.B));

    private void Push(IEdit e) { _undo.Push(e); _redo.Clear(); }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (_editing is not null) { CommitText(); return; }
        _start = e.GetPosition(_layer);
        _layer.CaptureMouse();
        switch (_tool)
        {
            case Tool.Pen:
            case Tool.Highlighter:
                bool hl = _tool == Tool.Highlighter;
                _stroke = new Polyline
                {
                    Stroke = Stroke(hl ? 0.4 : 1), StrokeThickness = hl ? Math.Max(12, _thickness.Value * 3) : _thickness.Value,
                    StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = hl ? PenLineCap.Flat : PenLineCap.Round,
                };
                _stroke.Points.Add(_start);
                _layer.Children.Add(_stroke);
                break;
            case Tool.Text:
                BeginText(_start); _layer.ReleaseMouseCapture();
                break;
            default:
                _draft = _tool == Tool.Ellipse ? new Ellipse() : new Rectangle();
                if (_tool == Tool.Crop) { _draft.Stroke = Brushes.White; _draft.StrokeDashArray = new DoubleCollection { 4, 3 }; _draft.Fill = new SolidColorBrush(Color.FromArgb(40, 45, 156, 255)); }
                else if (_tool == Tool.Blur) { _draft.Stroke = Brushes.Gray; _draft.StrokeDashArray = new DoubleCollection { 2, 2 }; }
                else if (_tool == Tool.Arrow) { _draft = null; }
                else { _draft.Stroke = Stroke(); }
                if (_draft is not null) { _draft.StrokeThickness = _tool is Tool.Crop or Tool.Blur ? 1 : _thickness.Value; _layer.Children.Add(_draft); }
                else { _arrowShaft = new Line { Stroke = Stroke(), StrokeThickness = _thickness.Value, StrokeStartLineCap = PenLineCap.Round, X1 = _start.X, Y1 = _start.Y, X2 = _start.X, Y2 = _start.Y }; _layer.Children.Add(_arrowShaft); }
                break;
        }
    }

    private Line? _arrowShaft;

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_layer.IsMouseCaptured) return;
        var p = Clamp(e.GetPosition(_layer));
        if (_stroke is not null) { _stroke.Points.Add(p); return; }
        if (_arrowShaft is not null) { _arrowShaft.X2 = p.X; _arrowShaft.Y2 = p.Y; return; }
        if (_draft is not null)
        {
            var r = new Rect(_start, p);
            Canvas.SetLeft(_draft, r.X); Canvas.SetTop(_draft, r.Y); _draft.Width = r.Width; _draft.Height = r.Height;
        }
    }

    private Point Clamp(Point p) => new(Math.Clamp(p.X, 0, _layer.Width), Math.Clamp(p.Y, 0, _layer.Height));

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_layer.IsMouseCaptured) return;
        _layer.ReleaseMouseCapture();
        var end = Clamp(e.GetPosition(_layer));

        if (_stroke is not null) { Push(new AddEdit(_layer, _stroke)); _stroke = null; return; }

        if (_arrowShaft is not null)
        {
            var shaft = _arrowShaft; _arrowShaft = null;
            _layer.Children.Remove(shaft);
            if ((end - _start).Length < 4) return;
            var arrow = MakeArrow(_start, end);
            _layer.Children.Add(arrow); Push(new AddEdit(_layer, arrow));
            return;
        }

        if (_draft is null) return;
        var d = _draft; _draft = null;
        _layer.Children.Remove(d);
        var rect = new Rect(_start, end);
        if (rect.Width < 3 || rect.Height < 3) return;

        switch (_tool)
        {
            case Tool.Rect:
            case Tool.Ellipse:
                _layer.Children.Add(d);
                Canvas.SetLeft(d, rect.X); Canvas.SetTop(d, rect.Y); d.Width = rect.Width; d.Height = rect.Height;
                Push(new AddEdit(_layer, d));
                break;
            case Tool.Blur: AddBlur(rect); break;
            case Tool.Crop: ApplyCrop(rect); break;
        }
    }

    private System.Windows.Shapes.Path MakeArrow(Point from, Point to)
    {
        var dir = to - from; dir.Normalize();
        var normal = new Vector(-dir.Y, dir.X);
        double head = Math.Max(12, _thickness.Value * 4);
        var back = to - dir * head;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(from, false, false); c.LineTo(back, true, false);
            c.BeginFigure(to, true, true); c.LineTo(back + normal * head * 0.5, true, false); c.LineTo(back - normal * head * 0.5, true, false);
        }
        g.Freeze();
        return new System.Windows.Shapes.Path { Data = g, Stroke = Stroke(), Fill = Stroke(), StrokeThickness = _thickness.Value, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round };
    }

    private void AddBlur(Rect r)
    {
        // Pixelate: shrink the region to ~1/12 size, then draw it back up with nearest-neighbour.
        var ir = ToIntRect(r);
        if (ir.Width < 1 || ir.Height < 1) return;
        var flat = Flatten(); // includes earlier annotations so blur hides them too
        var crop = new CroppedBitmap(flat, ir);
        double k = 1.0 / Math.Max(6, Math.Min(ir.Width, ir.Height) / 8.0);
        var small = new TransformedBitmap(crop, new ScaleTransform(Math.Min(1, k), Math.Min(1, k)));
        var img = new Image { Source = small, Width = ir.Width, Height = ir.Height, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
        Canvas.SetLeft(img, ir.X); Canvas.SetTop(img, ir.Y);
        _layer.Children.Add(img); Push(new AddEdit(_layer, img));
    }

    private void ApplyCrop(Rect r)
    {
        var ir = ToIntRect(r);
        if (ir.Width < 2 || ir.Height < 2) return;
        var oldBase = _base;
        var oldItems = _layer.Children.Cast<UIElement>().ToList();
        var cropped = new CroppedBitmap(Flatten(), ir);
        cropped.Freeze();
        var edit = new CropEdit(this, oldBase, cropped, oldItems);
        edit.Redo();
        Push(edit);
    }

    private Int32Rect ToIntRect(Rect r)
    {
        int x = (int)Math.Floor(Math.Max(0, r.X)), y = (int)Math.Floor(Math.Max(0, r.Y));
        int w = (int)Math.Min(Math.Ceiling(r.Width), _base.PixelWidth - x), h = (int)Math.Min(Math.Ceiling(r.Height), _base.PixelHeight - y);
        return new Int32Rect(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    private void BeginText(Point at)
    {
        var tb = new TextBox
        {
            MinWidth = 80, FontSize = Math.Max(14, _thickness.Value * 4), Foreground = Stroke(), Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderBrush = Brushes.Gray, AcceptsReturn = false,
        };
        Canvas.SetLeft(tb, at.X); Canvas.SetTop(tb, at.Y);
        _layer.Children.Add(tb);
        _editing = tb;
        tb.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CommitText(); e.Handled = true; }
            else if (e.Key == Key.Escape) { _layer.Children.Remove(tb); _editing = null; e.Handled = true; }
        };
        tb.LostKeyboardFocus += (_, _) => { if (_editing == tb) CommitText(); };
        tb.Loaded += (_, _) => tb.Focus();
    }

    private void CommitText()
    {
        var tb = _editing; if (tb is null) return;
        _editing = null;
        _layer.Children.Remove(tb);
        if (string.IsNullOrWhiteSpace(tb.Text)) return;
        var t = new TextBlock { Text = tb.Text, FontSize = tb.FontSize, Foreground = tb.Foreground, FontFamily = new FontFamily("Segoe UI") };
        Canvas.SetLeft(t, Canvas.GetLeft(tb)); Canvas.SetTop(t, Canvas.GetTop(tb));
        _layer.Children.Add(t); Push(new AddEdit(_layer, t));
    }

    // ---- undo / output -------------------------------------------------------------------------

    private void DoUndo() { if (_undo.Count == 0) return; var e = _undo.Pop(); e.Undo(); _redo.Push(e); }
    private void DoRedo() { if (_redo.Count == 0) return; var e = _redo.Pop(); e.Redo(); _undo.Push(e); }

    /// <summary>Base image + annotations as one bitmap at the image's native pixel size.</summary>
    private BitmapSource Flatten()
    {
        int w = _base.PixelWidth, h = _base.PixelHeight;
        _surface.UpdateLayout();
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawRectangle(new VisualBrush(_surface) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private void CopyImage()
    {
        _status.Text = ClipboardWriter.SetImage(Flatten()) ? "Copied to clipboard." : "Clipboard busy, try again.";
    }

    private void Save()
    {
        try
        {
            _savedPath = Exporter.Save(Flatten(), _prefs);
            _status.Text = "Saved " + _savedPath;
            _openFolder.Visibility = _copyPath.Visibility = Visibility.Visible;
            _notify("Saved", _savedPath, _savedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Save failed", ex);
            _status.Text = "Save failed: " + ex.Message;
        }
    }

    // ---- window geometry (physical px, validated against monitors) -----------------------------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (_prefs.EditorBounds is { Length: 4 } b)
        {
            var saved = new RectPx(b[0], b[1], b[2], b[3]);
            bool visible = MonitorEnumerator.Current.Any(m => m.Bounds.Intersect(saved).Width >= 100 && m.Bounds.Intersect(saved).Height >= 50);
            if (visible && saved.Width >= 300 && saved.Height >= 200)
                NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, saved.X, saved.Y, saved.Width, saved.Height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }
        if (_prefs.EditorMaximized) WindowState = WindowState.Maximized;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        _prefs.EditorMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal && NativeMethods.GetWindowRect(hwnd, out var r))
            _prefs.EditorBounds = new[] { r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top };
        try { _persist(); } catch (Exception ex) { Log.Warn("Could not persist editor geometry: " + ex.Message); }
    }
}
