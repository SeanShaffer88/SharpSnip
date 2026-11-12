using System.Windows;
using System.Windows.Controls;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>Every user-facing option (charter: toggles for everything, tooltip on every control).</summary>
internal sealed class SettingsWindow : Window
{
    private readonly Preferences _prefs;
    private readonly Func<Preferences, string?> _apply;
    private readonly TextBox _folder = new(), _hkMenu = new(), _hkFull = new();
    private readonly ComboBox _format = new(), _target = new(), _delay = new(), _matte = new();
    private readonly CheckBox _editor = new() { Content = "Open the editor after a capture" };
    private readonly CheckBox _toasts = new() { Content = "Show notifications" };
    private readonly CheckBox _contrast = new() { Content = "High-contrast highlight" };
    private readonly Slider _dim = new() { Minimum = 20, Maximum = 90, Width = 200, TickFrequency = 10, IsSnapToTickEnabled = false };
    private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public SettingsWindow(Preferences prefs, Func<Preferences, string?> apply)
    {
        _prefs = prefs; _apply = apply;
        Title = "SharpSnip settings";
        SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _format.ItemsSource = new[] { "png", "jpg" };
        _target.ItemsSource = new[] { "Current monitor", "All monitors" };
        _delay.ItemsSource = DelayCycle.Steps;
        _matte.ItemsSource = new[] { "Transparent", "Black", "White" };

        _folder.Text = prefs.OutputFolder; _folder.ToolTip = "Where captures are saved. Empty = Pictures\\SharpSnip.";
        _format.SelectedItem = prefs.ImageFormat; _format.ToolTip = "File format for saved captures. JPEG drops transparency.";
        _hkMenu.Text = prefs.HotkeyMenu; _hkMenu.ToolTip = "Opens the mode menu. Example: Win+Shift+X. Needs at least one modifier.";
        _hkFull.Text = prefs.HotkeyFullScreen; _hkFull.ToolTip = "One-shot full-screen capture straight to clipboard. Print Screen alone is allowed.";
        _target.SelectedIndex = prefs.FullScreenTarget == "AllMonitors" ? 1 : 0; _target.ToolTip = "What the one-shot hotkey captures.";
        _delay.SelectedItem = prefs.DefaultDelaySeconds; _delay.ToolTip = "Countdown before every capture, in seconds. Useful for menus and tooltips.";
        _matte.SelectedItem = prefs.MatteColor; _matte.ToolTip = "Fill for empty corners when monitors form an L-shape.";
        _editor.IsChecked = prefs.OpenEditorAfterCapture; _editor.ToolTip = "Off = capture goes straight to clipboard and file.";
        _toasts.IsChecked = prefs.ShowToasts; _toasts.ToolTip = "Balloon notifications after saving or on errors.";
        _contrast.IsChecked = prefs.HighContrastHighlight; _contrast.ToolTip = "Thicker yellow highlight and opaque hint bars.";
        _dim.Value = prefs.DimmerOpacity * 100; _dim.ToolTip = "How dark the screen gets while picking.";

        var grid = new Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        int row = 0;
        void Add(string label, UIElement c)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, row); Grid.SetRow(c, row); Grid.SetColumn(c, 1);
            if (c is FrameworkElement fe) fe.Margin = new Thickness(0, 4, 0, 4);
            grid.Children.Add(l); grid.Children.Add(c); row++;
        }
        Add("Output folder", _folder); Add("Image format", _format);
        Add("Menu hotkey", _hkMenu); Add("Full-screen hotkey", _hkFull);
        Add("Full-screen target", _target); Add("Default delay (s)", _delay);
        Add("Gap fill", _matte); Add("Dimmer darkness", _dim);
        Add("", _editor); Add("", _toasts); Add("", _contrast);
        Add("", _error);

        var ok = new Button { Content = "Save", IsDefault = true, Width = 80, Margin = new Thickness(4), ToolTip = "Apply and store settings." };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80, Margin = new Thickness(4), ToolTip = "Close without changes." };
        ok.Click += (_, _) => Commit();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 12) };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var root = new StackPanel(); root.Children.Add(grid); root.Children.Add(buttons);
        Content = root;
    }

    private void Commit()
    {
        var p = new Preferences
        {
            OutputFolder = _folder.Text.Trim(), ImageFormat = (string)_format.SelectedItem,
            HotkeyMenu = _hkMenu.Text.Trim(), HotkeyFullScreen = _hkFull.Text.Trim(),
            FullScreenTarget = _target.SelectedIndex == 1 ? "AllMonitors" : "CurrentMonitor",
            DefaultDelaySeconds = (int)_delay.SelectedItem, MatteColor = (string)_matte.SelectedItem,
            OpenEditorAfterCapture = _editor.IsChecked == true, ShowToasts = _toasts.IsChecked == true,
            HighContrastHighlight = _contrast.IsChecked == true, DimmerOpacity = _dim.Value / 100,
            EditorBounds = _prefs.EditorBounds, EditorMaximized = _prefs.EditorMaximized,
        };
        // Reject bad hotkeys with a message instead of silently falling back to defaults.
        if (!HotkeySpec.TryParse(p.HotkeyMenu, out _)) { _error.Text = "Menu hotkey is not valid. Example: Win+Shift+X"; return; }
        if (!HotkeySpec.TryParse(p.HotkeyFullScreen, out _)) { _error.Text = "Full-screen hotkey is not valid. Example: Win+Shift+F"; return; }
        if (p.HotkeyMenu.Equals(p.HotkeyFullScreen, StringComparison.OrdinalIgnoreCase)) { _error.Text = "The two hotkeys must differ."; return; }
        string? problem = _apply(p);
        if (problem is not null) { _error.Text = problem; return; }
        DialogResult = true;
    }
}
