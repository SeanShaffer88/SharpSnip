using System.Windows;
using System.Windows.Controls;
using Snipit.Core;

namespace Snipit;

/// <summary>Small modal dialogs for the editor's Resize / Watermark / Replace-text actions.</summary>
internal static class EditorDialogs
{
    private static Window Base(string title, UIElement content)
    {
        var w = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = content,
        };
        w.Loaded += (_, _) => TesselTheme.Apply(w);
        return w;
    }

    private static (Grid Grid, Action<string, UIElement> Add) Grid2()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        int row = 0;
        void Add(string label, UIElement c)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, row); Grid.SetRow(c, row); Grid.SetColumn(c, 1);
            grid.Children.Add(l); grid.Children.Add(c); row++;
        }
        return (grid, Add);
    }

    private static StackPanel Buttons(Window w, Action onOk)
    {
        var ok = new Button { Content = "OK", IsDefault = true, Width = 76, Margin = new Thickness(4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 76, Margin = new Thickness(4) };
        ok.Click += (_, _) => { onOk(); };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(ok); panel.Children.Add(cancel);
        return panel;
    }

    /// <summary>Returns the chosen size, or null when cancelled/invalid.</summary>
    public static (int W, int H)? Resize(Window owner, int origW, int origH)
    {
        var (grid, add) = Grid2();
        var wBox = new TextBox { Text = origW.ToString(), ToolTip = $"Width in pixels ({ResizePlan.MinDim} to {ResizePlan.MaxDim})." };
        var hBox = new TextBox { Text = origH.ToString(), ToolTip = "Height in pixels." };
        var keep = new CheckBox { Content = "Keep aspect ratio", IsChecked = true };
        bool syncing = false;
        wBox.TextChanged += (_, _) =>
        {
            if (syncing || keep.IsChecked != true || !int.TryParse(wBox.Text, out int nw)) return;
            syncing = true; hBox.Text = ResizePlan.FromWidth(origW, origH, nw).H.ToString(); syncing = false;
        };
        hBox.TextChanged += (_, _) =>
        {
            if (syncing || keep.IsChecked != true || !int.TryParse(hBox.Text, out int nh)) return;
            syncing = true; wBox.Text = ResizePlan.FromHeight(origW, origH, nh).W.ToString(); syncing = false;
        };
        add("Width", wBox); add("Height", hBox); add("", keep);
        (int, int)? result = null;
        var win = Base("Resize image", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () =>
        {
            if (int.TryParse(wBox.Text, out int nw) && int.TryParse(hBox.Text, out int nh))
            {
                result = (ResizePlan.ClampDim(nw), ResizePlan.ClampDim(nh));
                win.DialogResult = true;
            }
        }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>Returns (text, tiled, opacity) or null when cancelled.</summary>
    public static (string Text, bool Tiled, double Opacity)? Watermark(Window owner)
    {
        var (grid, add) = Grid2();
        var text = new TextBox { Text = "CONFIDENTIAL", ToolTip = "The watermark text." };
        var style = new ComboBox { ItemsSource = new[] { "Corner", "Tiled" }, SelectedIndex = 0, ToolTip = "One mark in the bottom-right corner, or a repeating diagonal pattern." };
        var opacity = new Slider { Minimum = 10, Maximum = 90, Value = 40, ToolTip = "How strongly the mark shows." };
        add("Text", text); add("Style", style); add("Opacity %", opacity);
        (string, bool, double)? result = null;
        var win = Base("Watermark", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () =>
        {
            if (!string.IsNullOrWhiteSpace(text.Text))
            {
                result = (text.Text.Trim(), style.SelectedIndex == 1, opacity.Value / 100.0);
                win.DialogResult = true;
            }
        }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>Edits the Frame / Shadow / Torn-Wave settings in place. Returns true when saved.</summary>
    public static bool Effects(Window owner, Preferences p)
    {
        var (grid, add) = Grid2();
        var thick = new TextBox { Text = p.FrameThickness.ToString(), ToolTip = "Frame border width in pixels. 0 = automatic by image size." };
        var color = new TextBox { Text = p.FrameColor, ToolTip = "Frame colour as six hex digits, e.g. 1B1B1D." };
        var blur = new TextBox { Text = p.ShadowBlur.ToString(), ToolTip = "Shadow blur radius in pixels. 0 = automatic by image size." };
        var strength = new Slider { Minimum = 0, Maximum = 100, Value = p.ShadowOpacityPercent, ToolTip = "How dark the shadow is." };
        var depth = new TextBox { Text = p.EdgeDepth.ToString(), ToolTip = "How deep the Torn and Wave cuts go, in pixels. 0 = automatic. Limited to a quarter of the image's short side." };
        var error = new TextBlock { Foreground = System.Windows.Media.Brushes.IndianRed, Margin = new Thickness(12, 0, 12, 0), TextWrapping = TextWrapping.Wrap };
        add("Frame width px (0 = auto)", thick); add("Frame colour (hex)", color);
        add("Shadow blur px (0 = auto)", blur); add("Shadow strength %", strength);
        add("Torn/Wave depth px (0 = auto)", depth);
        bool saved = false;
        var win = Base("Effect settings", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(error);
        stack.Children.Add(Buttons(win, () =>
        {
            if (!int.TryParse(thick.Text.Trim(), out int t) || t < 0 || t > ImageFrames.MaxThickness) { error.Text = $"Frame width must be 0 to {ImageFrames.MaxThickness}."; return; }
            if (!ColorFormat.TryParseHex(color.Text, out _)) { error.Text = "Frame colour must be six hex digits, like 1B1B1D."; return; }
            if (!int.TryParse(blur.Text.Trim(), out int b) || b < 0 || b > ImageFrames.MaxBlur) { error.Text = $"Shadow blur must be 0 to {ImageFrames.MaxBlur}."; return; }
            if (!int.TryParse(depth.Text.Trim(), out int d) || d < 0 || d > EdgeEffects.MaxDepth) { error.Text = $"Depth must be 0 to {EdgeEffects.MaxDepth}."; return; }
            p.FrameThickness = t; p.FrameColor = EffectSettings.SanitizeColor(color.Text);
            p.ShadowBlur = b; p.ShadowOpacityPercent = (int)Math.Round(strength.Value); p.EdgeDepth = d;
            saved = true; win.DialogResult = true;
        }));
        win.Owner = owner;
        win.ShowDialog();
        return saved;
    }

    /// <summary>Returns (find, replace) or null when cancelled.</summary>
    public static (string Find, string Replace)? ReplaceText(Window owner)
    {
        var (grid, add) = Grid2();
        var find = new TextBox { ToolTip = "Text to find in the image (read with the built-in OCR)." };
        var repl = new TextBox { ToolTip = "Text drawn in its place. The original is covered with the surrounding colour first." };
        add("Find", find); add("Replace with", repl);
        (string, string)? result = null;
        var win = Base("Find & replace text", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () =>
        {
            if (!string.IsNullOrWhiteSpace(find.Text))
            {
                result = (find.Text.Trim(), repl.Text);
                win.DialogResult = true;
            }
        }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>Returns brightness (-100..100) and contrast factor (0.5..2), or null when cancelled.</summary>
    public static (int Brightness, double Contrast)? BrightContrast(Window owner)
    {
        var (grid, add) = Grid2();
        var bright = new Slider { Minimum = -100, Maximum = 100, Value = 0, ToolTip = "Brightness. 0 = unchanged." };
        var contrast = new Slider { Minimum = 50, Maximum = 200, Value = 100, ToolTip = "Contrast in percent. 100 = unchanged." };
        add("Brightness", bright); add("Contrast", contrast);
        (int, double)? result = null;
        var win = Base("Brightness / contrast", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () => { result = ((int)bright.Value, contrast.Value / 100.0); win.DialogResult = true; }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>Returns hue shift in degrees and red/green/blue offsets, or null when cancelled.</summary>
    public static (double Hue, int Red, int Green, int Blue)? HueBalance(Window owner)
    {
        var (grid, add) = Grid2();
        Slider S(double min, double max, string tip) => new() { Minimum = min, Maximum = max, Value = 0, ToolTip = tip };
        var hue = S(-180, 180, "Rotate hues, in degrees. 0 = unchanged.");
        var red = S(-100, 100, "Add or remove red. 0 = unchanged.");
        var green = S(-100, 100, "Add or remove green. 0 = unchanged.");
        var blue = S(-100, 100, "Add or remove blue. 0 = unchanged.");
        add("Hue", hue); add("Red", red); add("Green", green); add("Blue", blue);
        (double, int, int, int)? result = null;
        var win = Base("Hue / colour balance", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () => { result = (hue.Value, (int)red.Value, (int)green.Value, (int)blue.Value); win.DialogResult = true; }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }
}
