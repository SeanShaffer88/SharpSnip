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
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>Mini editor (§9). Annotations live in a canvas above the image; undo/redo is an action stack.</summary>
internal sealed class EditorWindow : Window
{
    private enum Tool { Pen, Highlighter, Arrow, Rect, Ellipse, Text, Step, Blur, Spotlight, Crop, Cut }

    private interface IEdit { void Undo(); void Redo(); }

    private sealed class AddEdit : IEdit
    {
        private readonly Canvas _c; private readonly UIElement _e;
        public AddEdit(Canvas c, UIElement e) { _c = c; _e = e; }
        public void Undo() => _c.Children.Remove(_e);
        public void Redo() { if (!_c.Children.Contains(_e)) _c.Children.Add(_e); }
    }

    private sealed class BaseEdit : IEdit
    {
        private readonly EditorWindow _w; private readonly BitmapSource _oldBase, _newBase; private readonly List<UIElement> _oldItems;
        public BaseEdit(EditorWindow w, BitmapSource oldBase, BitmapSource newBase, List<UIElement> oldItems)
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
    private int _colorIndex;
    private readonly Slider _thickness = new() { Minimum = 1, Maximum = 20, Value = 4, Width = 110, ToolTip = "Stroke thickness." };
    private readonly EditorChrome _chrome = new(SystemParameters.HighContrast);

    private int _stepCount;
    private BitmapSource _base;
    private Tool _tool = Tool.Pen;
    private Point _start;
    private Shape? _draft;
    private Polyline? _stroke;
    private TextBox? _editing;
    private string? _savedPath;

    private static readonly Dictionary<Key, Tool> ToolKeys = new()
    {
        [Key.P] = Tool.Pen, [Key.H] = Tool.Highlighter, [Key.A] = Tool.Arrow, [Key.R] = Tool.Rect, [Key.O] = Tool.Ellipse,
        [Key.T] = Tool.Text, [Key.N] = Tool.Step, [Key.B] = Tool.Blur, [Key.S] = Tool.Spotlight, [Key.C] = Tool.Crop, [Key.X] = Tool.Cut,
    };
    private readonly Dictionary<Tool, ToggleButton> _toolCells = new();

    private void SelectTool(Tool t, ToggleButton cell)
    {
        _tool = t;
        foreach (var kv in _toolCells) kv.Value.IsChecked = kv.Value == cell;
    }

    private static readonly (string Name, Color Color)[] Palette =
    {
        ("Red", Colors.Red), ("Yellow", Colors.Gold), ("Green", Colors.LimeGreen), ("Blue", Color.FromRgb(0x2D, 0x9C, 0xFF)),
        ("Black", Colors.Black), ("White", Colors.White),
    };

    public EditorWindow(CaptureResult cap, CaptureMeta meta, int monitorCount, int delay, Preferences prefs, Action persist, Action<string, string, string?> notify)
    {
        _prefs = prefs; _persist = persist; _notify = notify; _meta = meta; _base = cap.Image;
        Title = "Snipit";
        Width = 1100; Height = 720; MinWidth = 960; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AutomationProperties_Name(this, "Snipit editor");

        var root = new DockPanel();
        root.Children.Add(BuildToolbar()); DockPanel.SetDock(root.Children[0], Dock.Top);
        Background = _chrome.CanvasBg; Foreground = _chrome.Ink; FontFamily = new FontFamily("Segoe UI"); FontSize = 12;
        _status.Foreground = _chrome.Muted; _status.Tag = EditorChrome.Keep;
        foreach (var sb in new[] { _openFolder, _copyPath }) { sb.Template = _chrome.ChipTemplate; sb.Foreground = _chrome.Ink; sb.Tag = EditorChrome.Keep; sb.Height = 22; }
        var statusBar = new DockPanel { Background = _chrome.Surface };
        var statusButtons = new StackPanel { Orientation = Orientation.Horizontal };
        statusButtons.Children.Add(_openFolder); statusButtons.Children.Add(_copyPath);
        DockPanel.SetDock(statusButtons, Dock.Right);
        statusBar.Children.Add(statusButtons); statusBar.Children.Add(_status);
        var statusEdge = new Border { BorderBrush = _chrome.Edge, BorderThickness = new Thickness(0, 1, 0, 0), Child = statusBar };
        DockPanel.SetDock(statusEdge, Dock.Bottom);
        root.Children.Add(statusEdge);
        var side = BuildMetaPanel(cap, meta, monitorCount, delay);
        DockPanel.SetDock(side, Dock.Right);
        root.Children.Add(side);

        _surface.Children.Add(_image); _surface.Children.Add(_layer);
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        SetBase(_base);
        root.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = _surface, Margin = new Thickness(8), Focusable = false });
        root.Background = _chrome.CanvasBg;
        Content = root;
        if (!SystemParameters.HighContrast) Loaded += (_, _) => TesselTheme.Apply(this);
        else { Background = _chrome.CanvasBg; Foreground = _chrome.Ink; }

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
        PreviewKeyDown += (_, e) =>
        {
            if (_editing is not null) return;
            if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
            if (Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is TextBox) return;
            if (ToolKeys.TryGetValue(e.Key, out var t) && _toolCells.TryGetValue(t, out var cell)) { SelectTool(t, cell); e.Handled = true; }
        };

        _status.Text = meta.Mode + (cap.HasGaps ? " · monitor layout has gaps (matte fill)" : "");
        Loaded += (_, _) => ApplyAutoEffect();
    }

    private void ApplyAutoEffect()
    {
        try
        {
            switch (_prefs.AutoEffect)
            {
                case "Grayscale":
                    TransformBase("Auto effect: grayscale", (px, w, h) => { PixelAdjust.Grayscale(px); return (px, w, h); });
                    break;
                case "Border":
                    TransformBase("Auto effect: border", (px, w, h) => { PixelAdjust.Border(px, w, h, Math.Max(2, Math.Min(w, h) / 120), 0, 0, 0); return (px, w, h); });
                    break;
            }
        }
        catch (Exception ex) { Log.Error("Auto effect failed", ex); }
    }

    private static void AutomationProperties_Name(DependencyObject d, string name) => System.Windows.Automation.AutomationProperties.SetName(d, name);

    // ---- UI construction -----------------------------------------------------------------------

    private UIElement BuildToolbar()
    {
        var c = _chrome;
        // ---- tools row ----
        var tools = new DockPanel { Margin = new Thickness(6, 4, 6, 4) };
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(right, Dock.Right);
        void AddTool(string label, string icon, string key, Tool t, string tip)
        {
            var b = c.ToolCell(label, icon, key, tip);
            b.IsChecked = t == _tool;
            b.Click += (_, _) => SelectTool(t, b);
            _toolCells[t] = b; left.Children.Add(b);
        }
        AddTool("Pen", "pen", "P", Tool.Pen, "Freehand pen. Drag on the image.");
        AddTool("Highlight", "highlighter", "H", Tool.Highlighter, "Translucent wide marker.");
        AddTool("Arrow", "arrow", "A", Tool.Arrow, "Drag from tail to head.");
        AddTool("Rect", "rect", "R", Tool.Rect, "Drag a rectangle outline.");
        AddTool("Ellipse", "ellipse", "O", Tool.Ellipse, "Drag an ellipse outline.");
        AddTool("Text", "text", "T", Tool.Text, "Click to type. Enter commits, Esc discards.");
        AddTool("Step", "step", "N", Tool.Step, "Click to drop the next numbered step marker (1, 2, 3…).");
        left.Children.Add(new Border { Width = 1, Margin = new Thickness(6, 8, 6, 8), Background = c.Edge });
        AddTool("Blur", "blur", "B", Tool.Blur, "Drag over sensitive content to pixelate it.");
        AddTool("Spotlight", "spotlight", "S", Tool.Spotlight, "Drag the area to keep bright; everything else is dimmed. An annotation: Undo removes it.");
        AddTool("Crop", "crop", "C", Tool.Crop, "Drag the area to keep. Applies immediately; Undo restores.");
        AddTool("Cut out", "cut", "X", Tool.Cut, "Drag across the image to remove a strip and join the two sides. A wide drag removes rows, a tall drag removes columns. Annotations are baked in; Undo restores.");

        Button Cell(string label, string icon, string tip, Action a)
        {
            var b = c.ActionCell(label, icon, tip); b.Click += (_, _) => a(); right.Children.Add(b); return b;
        }
        Cell("Undo", "undo", "Undo last edit. Key: Ctrl+Z.", DoUndo);
        Cell("Redo", "redo", "Redo. Key: Ctrl+Y.", DoRedo);
        Cell("Copy", "copy", "Copy the edited image to the clipboard. Key: Ctrl+C.", CopyImage);
        var save = c.SaveButton("Save to the output folder."); save.Click += (_, _) => Save(); right.Children.Add(save);
        tools.Children.Add(right); tools.Children.Add(left);

        // ---- options row ----
        var opts = new DockPanel { Margin = new Thickness(10, 0, 8, 0), Height = 32 };
        var optsLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var optsRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(optsRight, Dock.Right);
        TextBlock Label(string t) => new() { Text = t, Foreground = c.Muted, Tag = EditorChrome.Keep, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        optsLeft.Children.Add(Label("Colour"));
        var swatches = new List<ToggleButton>();
        for (int i = 0; i < Palette.Length; i++)
        {
            int idx = i;
            var sw = c.Swatch(Palette[i].Color, Palette[i].Name);
            sw.IsChecked = i == _colorIndex;
            sw.Click += (_, _) => { _colorIndex = idx; foreach (var o in swatches) o.IsChecked = o == sw; };
            swatches.Add(sw); optsLeft.Children.Add(sw);
        }
        var wl = Label("Width"); wl.Margin = new Thickness(18, 0, 8, 0); optsLeft.Children.Add(wl);
        _thickness.Style = c.SliderStyle; _thickness.Tag = EditorChrome.Keep; _thickness.VerticalAlignment = VerticalAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetName(_thickness, "Stroke width");
        optsLeft.Children.Add(_thickness);

        void Add(string label, string tip, Action<Func<string, string, Action, MenuItem>, Action> fill)
        {
            var (btn, menu) = c.MenuButton(label, tip);
            Func<string, string, Action, MenuItem> item = (h, t, a) => { var mi = c.Item(h, t, a); menu.Items.Add(mi); return mi; };
            fill(item, () => menu.Items.Add(c.Sep()));
            optsRight.Children.Add(btn);
        }
        Add("Text", "Recognise, redact or replace text in the image; watermark.", (item, sep) =>
        {
            item("Copy text", "Recognise the text in this image (built-in Windows OCR, offline) and copy it to the clipboard.", () => _ = CopyTextAsync());
            item("Redact", "Find emails, cards, IPs, phone numbers and key-shaped tokens in the image's text and cover them with black boxes (undoable).", () => _ = RedactAsync());
            item("Replace text…", "Find text in the image (OCR) and replace it: the original is covered with the surrounding colour and the new text drawn in (undoable).", () =>
            {
                if (EditorDialogs.ReplaceText(this) is { } rr) _ = ReplaceTextAsync(rr.Find, rr.Replace);
            });
            sep();
            item("Watermark…", "Stamp a text watermark, in the corner or tiled. Annotations are baked in; Undo restores.", () =>
            {
                if (EditorDialogs.Watermark(this) is { } wm) ApplyWatermark(wm.Text, wm.Tiled, wm.Opacity);
            });
        });
        Add("Effects", "Frame, shadow, torn and wave edges.", (item, sep) =>
        {
            item("Frame", "Add a solid border around the whole image (grows the canvas). Size and colour: Settings…. Annotations are baked in; Undo restores.", () =>
                TransformBase("Frame", (px, w, h) =>
                {
                    var col = EffectSettings.FrameRgb(_prefs.FrameColor);
                    return ImageFrames.AddBorder(px, w, h, EffectSettings.FrameThickness(_prefs.FrameThickness, w, h), col.R, col.G, col.B);
                }));
            item("Shadow", "Add a soft drop shadow under the image on a transparent margin (keep PNG to preserve it). Annotations are baked in; Undo restores.", () =>
                TransformBase("Shadow", (px, w, h) =>
                {
                    int blur = EffectSettings.ShadowBlur(_prefs.ShadowBlur, w, h), off = EffectSettings.ShadowOffset(blur);
                    return ImageFrames.AddShadow(px, w, h, blur, off, off, EffectSettings.ShadowOpacity(_prefs.ShadowOpacityPercent));
                }));
            item("Torn edge", "Rip the edges of the whole image like torn paper (cut area becomes transparent; keep PNG to preserve it). Annotations are baked in; Undo restores.", () =>
                TransformBase("Torn edge", (px, w, h) => (EdgeEffects.Apply(px, w, h, EdgeStyle.Torn, EdgeSides.All, EffectSettings.EdgeDepth(_prefs.EdgeDepth, w, h), Environment.TickCount), w, h)));
            item("Wave edge", "Give the edges of the whole image a smooth wave (cut area becomes transparent; keep PNG to preserve it). Annotations are baked in; Undo restores.", () =>
                TransformBase("Wave edge", (px, w, h) => (EdgeEffects.Apply(px, w, h, EdgeStyle.Wave, EdgeSides.All, EffectSettings.EdgeDepth(_prefs.EdgeDepth, w, h), 0), w, h)));
            sep();
            item("Settings…", "Set the Frame width and colour, Shadow blur and strength, and Torn/Wave depth. 0 = automatic by image size. Saved for next time.", () =>
            {
                if (EditorDialogs.Effects(this, _prefs)) { _persist(); _status.Text = "Effect settings saved"; }
            });
        });
        Add("Adjust", "Resize, colours, flip and rotate.", (item, sep) =>
        {
            item("Resize…", "Resize the whole image to new pixel dimensions. Annotations are baked in; Undo restores.", () =>
            {
                if (EditorDialogs.Resize(this, _base.PixelWidth, _base.PixelHeight) is { } sz && (sz.W != _base.PixelWidth || sz.H != _base.PixelHeight))
                    ResizeBase(sz.W, sz.H);
            });
            sep();
            item("Invert", "Invert the colours of the whole image. Annotations are baked in; Undo restores.", () => TransformBase("Invert", (px, w, h) => { PixelAdjust.Invert(px); return (px, w, h); }));
            item("Grayscale", "Convert the whole image to grey. Annotations are baked in; Undo restores.", () => TransformBase("Grayscale", (px, w, h) => { PixelAdjust.Grayscale(px); return (px, w, h); }));
            item("Brightness / contrast…", "Set brightness and contrast, then apply to the whole image. Annotations are baked in; Undo restores.", () =>
            {
                if (EditorDialogs.BrightContrast(this) is { } bc)
                    TransformBase("Brightness/contrast", (px, w, h) => { PixelAdjust.BrightnessContrast(px, bc.Brightness, bc.Contrast); return (px, w, h); });
            });
            item("Sharpen", "Sharpen the whole image a little. Repeat for more. Annotations are baked in; Undo restores.", () => TransformBase("Sharpen", (px, w, h) => { PixelAdjust.Sharpen(px, w, h, 0.8); return (px, w, h); }));
            item("More colour", "Make colours 30% more vivid. Annotations are baked in; Undo restores.", () => TransformBase("More colour", (px, w, h) => { PixelAdjust.Saturation(px, 1.3); return (px, w, h); }));
            item("Less colour", "Make colours 30% more muted. Annotations are baked in; Undo restores.", () => TransformBase("Less colour", (px, w, h) => { PixelAdjust.Saturation(px, 0.7); return (px, w, h); }));
            sep();
            item("Flip horizontal", "Flip the image left-to-right. Annotations are baked in; Undo restores.", () => TransformBase("Flip horizontal", (px, w, h) => { PixelAdjust.FlipHorizontal(px, w, h); return (px, w, h); }));
            item("Flip vertical", "Flip the image top-to-bottom. Annotations are baked in; Undo restores.", () => TransformBase("Flip vertical", (px, w, h) => { PixelAdjust.FlipVertical(px, w, h); return (px, w, h); }));
            item("Rotate 90°", "Rotate the image 90° clockwise. Annotations are baked in; Undo restores.", () => TransformBase("Rotate 90°", PixelAdjust.Rotate90Clockwise));
        });
        Add("More", "External editor and reveal in Explorer.", (item, sep) =>
        {
            item("External editor…", "Save, then open the file in the external editor set in Settings.", () =>
            {
                if (string.IsNullOrWhiteSpace(_prefs.ExternalEditorPath)) { _status.Text = "No external editor set — add one in Settings."; return; }
                if (_savedPath is null) Save();
                if (_savedPath is null) return;
                try
                {
                    Process.Start(new ProcessStartInfo(_prefs.ExternalEditorPath.Trim()) { Arguments = "\"" + _savedPath + "\"", UseShellExecute = true });
                    _status.Text = "Opened in the external editor.";
                }
                catch (Exception ex) { Log.Error("External editor failed", ex); _status.Text = "Could not start the external editor: " + ex.Message; }
            });
            item("Reveal in Explorer", "Save if needed, then show the file in Explorer. (The Windows share sheet is not implemented yet.)", () => { if (_savedPath is null) Save(); if (_savedPath is not null) Process.Start("explorer.exe", $"/select,\"{_savedPath}\""); });
        });
        opts.Children.Add(optsRight); opts.Children.Add(optsLeft);

        var stack = new StackPanel();
        stack.Children.Add(new Border { Background = c.Bar, BorderBrush = c.Edge, BorderThickness = new Thickness(0, 0, 0, 1), Child = tools });
        stack.Children.Add(new Border { Background = c.Surface, BorderBrush = c.Edge, BorderThickness = new Thickness(0, 0, 0, 1), Child = opts });
        return stack;
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
            BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = _chrome.Ink, Margin = new Thickness(8), Tag = EditorChrome.Keep,
            ToolTip = "Capture details, handy for bug reports. Select and copy as needed.",
        };
        System.Windows.Automation.AutomationProperties.SetName(tb, "Capture metadata");
        return new Border { Width = 260, Background = _chrome.Surface, BorderBrush = _chrome.Edge, BorderThickness = new Thickness(1, 0, 0, 0), Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = tb } };
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

    private Color CurrentColor => Palette[Math.Clamp(_colorIndex, 0, Palette.Length - 1)].Color;
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
            case Tool.Step:
                PlaceStep(_start); _layer.ReleaseMouseCapture();
                break;
            default:
                _draft = _tool == Tool.Ellipse ? new Ellipse() : new Rectangle();
                if (_tool == Tool.Spotlight) { _draft.Stroke = Brushes.White; _draft.StrokeDashArray = new DoubleCollection { 4, 3 }; _draft.Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)); }
                else if (_tool is Tool.Crop or Tool.Cut) { _draft.Stroke = _tool == Tool.Cut ? Brushes.IndianRed : Brushes.White; _draft.StrokeDashArray = new DoubleCollection { 4, 3 }; _draft.Fill = _tool == Tool.Cut ? new SolidColorBrush(Color.FromArgb(70, 220, 60, 60)) : new SolidColorBrush(Color.FromArgb(40, 45, 156, 255)); }
                else if (_tool == Tool.Blur) { _draft.Stroke = Brushes.Gray; _draft.StrokeDashArray = new DoubleCollection { 2, 2 }; }
                else if (_tool == Tool.Arrow) { _draft = null; }
                else { _draft.Stroke = Stroke(); }
                if (_draft is not null) { _draft.StrokeThickness = _tool is Tool.Crop or Tool.Cut or Tool.Blur or Tool.Spotlight ? 1 : _thickness.Value; _layer.Children.Add(_draft); }
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
            case Tool.Spotlight:
                var dim = new System.Windows.Shapes.Path
                {
                    Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, _layer.Width, _layer.Height)), new RectangleGeometry(rect)),
                    Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), IsHitTestVisible = false,
                };
                _layer.Children.Add(dim); Push(new AddEdit(_layer, dim));
                break;
            case Tool.Crop: ApplyCrop(rect); break;
            case Tool.Cut: ApplyCut(rect); break;
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

    private void ApplyCut(Rect r)
    {
        var ir = ToIntRect(r);
        TransformBase("Cut out", (px, w, h) =>
        {
            if (ImageCut.Plan(ir.X, ir.Y, ir.Width, ir.Height, w, h) is not { } band) return (px, w, h);
            return ImageCut.Remove(px, w, h, band);
        });
    }

    private void ApplyCrop(Rect r)
    {
        var ir = ToIntRect(r);
        if (ir.Width < 2 || ir.Height < 2) return;
        var oldBase = _base;
        var oldItems = _layer.Children.Cast<UIElement>().ToList();
        var cropped = new CroppedBitmap(Flatten(), ir);
        cropped.Freeze();
        var edit = new BaseEdit(this, oldBase, cropped, oldItems);
        edit.Redo();
        Push(edit);
    }

    /// <summary>
    /// Applies a whole-image pixel operation (Core <see cref="PixelAdjust"/>) to the
    /// flattened image and swaps it in as the new base, like Crop: annotations are
    /// baked in, and Undo restores the previous base and its annotation layer.
    /// </summary>
    private void TransformBase(string label, Func<byte[], int, int, (byte[] Pixels, int Width, int Height)> op)
    {
        try
        {
            var flat = Flatten();
            var src = flat.Format == PixelFormats.Bgra32 ? flat : new FormatConvertedBitmap(flat, PixelFormats.Bgra32, null, 0);
            int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            src.CopyPixels(px, stride, 0);
            var (data, nw, nh) = op(px, w, h);
            var bmp = BitmapSource.Create(nw, nh, 96, 96, PixelFormats.Bgra32, null, data, nw * 4);
            bmp.Freeze();
            SwapBase(bmp, label + " applied");
        }
        catch (Exception ex)
        {
            Log.Error(label + " failed", ex);
            _status.Text = label + " failed: " + ex.Message;
        }
    }

    /// <summary>Swaps in a new base image (annotations baked into it) as one undoable edit.</summary>
    private void SwapBase(BitmapSource newBase, string label)
    {
        var oldItems = _layer.Children.Cast<UIElement>().ToList();
        var edit = new BaseEdit(this, _base, newBase, oldItems);
        edit.Redo();
        Push(edit);
        _status.Text = label + " (annotations baked in). Undo restores the previous image.";
    }

    private void ResizeBase(int nw, int nh)
    {
        try
        {
            var flat = Flatten();
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) dc.DrawImage(flat, new Rect(0, 0, nw, nh));
            var rtb = new RenderTargetBitmap(nw, nh, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv); rtb.Freeze();
            SwapBase(rtb, $"Resized to {nw} × {nh}");
        }
        catch (Exception ex) { Log.Error("Resize failed", ex); _status.Text = "Resize failed: " + ex.Message; }
    }

    private void ApplyWatermark(string text, bool tiled, double opacity)
    {
        try
        {
            int w = _base.PixelWidth, h = _base.PixelHeight;
            var flat = Flatten();
            var brush = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0.05, 1) * 255), 255, 255, 255));
            double fontSize = Math.Max(14, w / 36.0);
            var ft = new System.Windows.Media.FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize, brush, 1.0);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(flat, new Rect(0, 0, w, h));
                if (!tiled)
                {
                    var (x, y) = WatermarkPlan.CornerOrigin(w, h, (int)Math.Ceiling(ft.Width), (int)Math.Ceiling(ft.Height));
                    dc.DrawText(ft, new Point(x, y));
                }
                else
                {
                    dc.PushTransform(new RotateTransform(-30, w / 2.0, h / 2.0));
                    int stepX = (int)(ft.Width + 90), stepY = (int)(ft.Height + 70);
                    foreach (var (x, y) in WatermarkPlan.TileOrigins(w, h, stepX, stepY))
                        dc.DrawText(ft, new Point(x, y));
                    dc.Pop();
                }
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv); rtb.Freeze();
            SwapBase(rtb, "Watermark applied");
        }
        catch (Exception ex) { Log.Error("Watermark failed", ex); _status.Text = "Watermark failed: " + ex.Message; }
    }

    private Int32Rect ToIntRect(Rect r)
    {
        int x = (int)Math.Floor(Math.Max(0, r.X)), y = (int)Math.Floor(Math.Max(0, r.Y));
        int w = (int)Math.Min(Math.Ceiling(r.Width), _base.PixelWidth - x), h = (int)Math.Min(Math.Ceiling(r.Height), _base.PixelHeight - y);
        return new Int32Rect(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    private void PlaceStep(Point at)
    {
        _stepCount++;
        const double size = 30;
        var grid = new Grid { Width = size, Height = size };
        grid.Children.Add(new Ellipse { Fill = Stroke(), Stroke = Brushes.White, StrokeThickness = 2 });
        grid.Children.Add(new TextBlock
        {
            Text = _stepCount.ToString(), Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 15,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        Canvas.SetLeft(grid, Math.Clamp(at.X - size / 2, 0, Math.Max(0, _layer.Width - size)));
        Canvas.SetTop(grid, Math.Clamp(at.Y - size / 2, 0, Math.Max(0, _layer.Height - size)));
        _layer.Children.Add(grid);
        Push(new AddEdit(_layer, grid));
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
    private async Task CopyTextAsync()
    {
        _status.Text = "Recognising text…";
        try
        {
            var (text, error) = await OcrService.RecognizeAsync(Flatten());
            if (text is null) { _status.Text = error ?? "OCR is unavailable."; return; }
            if (text.Length == 0) { _status.Text = "No text found in this image."; return; }
            try
            {
                System.Windows.Clipboard.SetText(text);
                _status.Text = $"Copied recognised text ({text.Split('\n').Length} line(s)) to the clipboard.";
            }
            catch (Exception) { _status.Text = "Clipboard busy, try again."; }
        }
        catch (Exception ex)
        {
            Log.Error("OCR failed", ex);
            _status.Text = "Text recognition failed: " + ex.Message;
        }
    }

    private async Task RedactAsync()
    {
        _status.Text = "Scanning for sensitive text…";
        try
        {
            // OCR runs on the base image, not the flattened one: annotations are not text.
            var (words, error) = await OcrService.RecognizeWordsAsync(_base);
            if (words is null) { _status.Text = error ?? "OCR is unavailable."; return; }
            var hits = Redactor.Find(words);
            if (hits.Count == 0) { _status.Text = "No sensitive text found (emails, cards, IPs, phone numbers, key-shaped tokens)."; return; }
            foreach (var hit in hits)
            {
                var box = new System.Windows.Shapes.Rectangle
                {
                    Fill = System.Windows.Media.Brushes.Black,
                    Width = hit.Rect.Width,
                    Height = hit.Rect.Height,
                };
                Canvas.SetLeft(box, hit.Rect.X);
                Canvas.SetTop(box, hit.Rect.Y);
                _layer.Children.Add(box);
                Push(new AddEdit(_layer, box));
            }
            string kinds = string.Join(", ", hits.GroupBy(h => h.Kind).Select(g => $"{g.Count()} {g.Key}"));
            _status.Text = $"Redacted {hits.Count} item(s): {kinds}. Each box is an annotation — Undo removes them.";
        }
        catch (Exception ex)
        {
            Log.Error("Redact failed", ex);
            _status.Text = "Redact failed: " + ex.Message;
        }
    }

    private async Task ReplaceTextAsync(string find, string replace)
    {
        _status.Text = "Finding text…";
        try
        {
            var (words, error) = await OcrService.RecognizeWordsAsync(_base);
            if (words is null) { _status.Text = error ?? "OCR is unavailable."; return; }
            var matches = TextReplace.Find(words, find);
            if (matches.Count == 0) { _status.Text = $"\"{find}\" was not found in the image's text."; return; }
            var flat = Flatten();
            var src = flat.Format == PixelFormats.Bgra32 ? flat : new FormatConvertedBitmap(flat, PixelFormats.Bgra32, null, 0);
            int w = src.PixelWidth, hh = src.PixelHeight, stride = w * 4;
            var px = new byte[stride * hh];
            src.CopyPixels(px, stride, 0);
            foreach (var m in matches)
            {
                var r = m.Union;
                var bg = SamplePerimeter(px, w, hh, r);
                var cover = new System.Windows.Shapes.Rectangle { Fill = new SolidColorBrush(bg), Width = r.Width, Height = r.Height };
                Canvas.SetLeft(cover, r.X); Canvas.SetTop(cover, r.Y);
                _layer.Children.Add(cover); Push(new AddEdit(_layer, cover));
                if (replace.Length > 0)
                {
                    double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
                    var t = new TextBlock
                    {
                        Text = replace, FontSize = Math.Max(8, r.Height * 0.72),
                        Foreground = new SolidColorBrush(lum > 0.5 ? Colors.Black : Colors.White),
                        FontFamily = new FontFamily("Segoe UI"),
                    };
                    Canvas.SetLeft(t, r.X); Canvas.SetTop(t, Math.Max(0, r.Y - r.Height * 0.08));
                    _layer.Children.Add(t); Push(new AddEdit(_layer, t));
                }
            }
            _status.Text = $"Replaced {matches.Count} occurrence(s) of \"{find}\". Each cover and text is an annotation — Undo steps back through them.";
        }
        catch (Exception ex)
        {
            Log.Error("Replace text failed", ex);
            _status.Text = "Replace failed: " + ex.Message;
        }
    }

    /// <summary>Average colour of the pixels just outside a rectangle — the best guess at what hides behind it.</summary>
    private static Color SamplePerimeter(byte[] px, int w, int h, RectPx r)
    {
        long sb = 0, sg = 0, sr = 0; int n = 0;
        void Take(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = (y * w + x) * 4;
            sb += px[i]; sg += px[i + 1]; sr += px[i + 2]; n++;
        }
        for (int x = r.X - 1; x <= r.Right; x += 2) { Take(x, r.Y - 1); Take(x, r.Bottom); }
        for (int y = r.Y; y < r.Bottom; y += 2) { Take(r.X - 1, y); Take(r.Right, y); }
        return n == 0 ? Colors.White : Color.FromRgb((byte)(sr / n), (byte)(sg / n), (byte)(sb / n));
    }

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
