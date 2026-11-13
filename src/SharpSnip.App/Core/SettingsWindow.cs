using System.Windows;
using System.Windows.Controls;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>Every user-facing option (charter: toggles for everything, tooltip on every control).</summary>
internal sealed class SettingsWindow : Window
{
    private readonly Preferences _prefs;
    private readonly Func<Preferences, string?> _apply;
    private readonly TextBox _folder = new(), _hkMenu = new(), _hkFull = new(), _hkWin = new(), _hkRepeat = new(), _hkMon = new(), _fixW = new(), _fixH = new(), _interval = new(), _hkPreset = new(), _extEditor = new();
    private readonly TextBlock _presetInfo = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _useLast = new() { Content = "Use last capture region", Padding = new Thickness(8, 3, 8, 3) };
    private int[]? _presetRect;
    private readonly ComboBox _format = new(), _target = new(), _delay = new(), _matte = new(), _effect = new();
    private readonly CheckBox _editor = new() { Content = "Open the editor after a capture" };
    private readonly CheckBox _toasts = new() { Content = "Show notifications" };
    private readonly CheckBox _contrast = new() { Content = "High-contrast highlight" };
    private readonly CheckBox _prtsc = new() { Content = "Print Screen replaces Snipping Tool" };
    private readonly Slider _dim = new() { Minimum = 20, Maximum = 90, Width = 200, TickFrequency = 10, IsSnapToTickEnabled = false };
    private readonly TextBlock _error = new() { Foreground = TesselTheme.Danger, TextWrapping = TextWrapping.Wrap };

    public SettingsWindow(Preferences prefs, Func<Preferences, string?> apply)
    {
        _prefs = prefs; _apply = apply;
        Title = "SharpSnip settings";
        SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _format.ItemsSource = ImageFormats.All;
        _target.ItemsSource = new[] { "Current monitor", "All monitors" };
        _delay.ItemsSource = DelayCycle.Steps;
        _matte.ItemsSource = new[] { "Transparent", "Black", "White" };
        _effect.ItemsSource = AutoEffects.All;

        _folder.Text = prefs.OutputFolder; _folder.ToolTip = "Where captures are saved. Empty = your Pictures folder.";
        _extEditor.Text = prefs.ExternalEditorPath; _extEditor.ToolTip = "Path to an image editor program (for example paint.net or GIMP). The editor window's External editor button opens the saved capture in it. Empty = none.";
        _format.SelectedItem = prefs.ImageFormat; _format.ToolTip = "File format for saved captures. PNG and TIFF keep transparency; JPEG, BMP, GIF and PDF drop it. A still GIF is a single frame; interval captures in GIF become one animated file. PDF holds one image, losslessly.";
        _hkMenu.Text = prefs.HotkeyMenu; _hkMenu.ToolTip = "Opens the mode menu. Example: Win+Shift+X. Needs at least one modifier.";
        _hkFull.Text = prefs.HotkeyFullScreen; _hkFull.ToolTip = "One-shot full-screen capture straight to clipboard. Print Screen alone is allowed.";
        _hkWin.Text = prefs.HotkeyActiveWindow; _hkWin.ToolTip = "Captures the foreground window straight to clipboard and file.";
        _hkRepeat.Text = prefs.HotkeyRepeatLast; _hkRepeat.ToolTip = "Captures the same screen rectangle as the previous capture.";
        _hkMon.Text = prefs.HotkeyMonitors; _hkMon.ToolTip = "Capture monitor 1, 2, 3... one hotkey each, separated by semicolons. Empty = none. Up to 9.";
        _hkPreset.Text = prefs.HotkeyPreset; _hkPreset.ToolTip = "One-shot capture of the preset region below. Empty = no hotkey.";
        _presetRect = prefs.PresetRect;
        _useLast.ToolTip = "Sets the preset region to the rectangle of your most recent capture.";
        _useLast.Click += (_, _) =>
        {
            if (_prefs.LastCaptureRect is { Length: 4 } r) { _presetRect = (int[])r.Clone(); }
            UpdatePresetLabel();
        };
        UpdatePresetLabel();
        _fixW.Text = prefs.FixedWidth.ToString(); _fixW.ToolTip = "Width in pixels of the fixed-size region (16 to 16384).";
        _fixH.Text = prefs.FixedHeight.ToString(); _fixH.ToolTip = "Height in pixels of the fixed-size region (16 to 16384).";
        _interval.Text = prefs.IntervalSeconds.ToString(); _interval.ToolTip = "Seconds between frames for interval capture (1 to 86400). Start it from the tray menu.";
        _target.SelectedIndex = prefs.FullScreenTarget == "AllMonitors" ? 1 : 0; _target.ToolTip = "What the one-shot hotkey captures.";
        _delay.SelectedItem = prefs.DefaultDelaySeconds; _delay.ToolTip = "Countdown before every capture, in seconds. Useful for menus and tooltips.";
        _matte.SelectedItem = prefs.MatteColor; _matte.ToolTip = "Fill for empty corners when monitors form an L-shape.";
        _effect.SelectedItem = AutoEffects.Sanitize(prefs.AutoEffect); _effect.ToolTip = "Applied automatically when a capture opens in the editor (undoable there). Border draws a thin black frame; Grayscale converts to grey.";
        _editor.IsChecked = prefs.OpenEditorAfterCapture; _editor.ToolTip = "Off = capture goes straight to clipboard and file.";
        _toasts.IsChecked = prefs.ShowToasts; _toasts.ToolTip = "Balloon notifications after saving or on errors.";
        _contrast.IsChecked = prefs.HighContrastHighlight; _contrast.ToolTip = "Thicker yellow highlight and opaque hint bars.";
        _prtsc.IsChecked = prefs.PrintScreenTakeover; _prtsc.ToolTip = "Gives the Print Screen key to SharpSnip instead of Snipping Tool: the Windows switch for it is turned off and Print Screen becomes the menu hotkey. Unticking restores both the switch and your old menu hotkey.";
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
        Add("Output folder", _folder); Add("Image format", _format); Add("External editor", _extEditor);
        Add("Menu hotkey", _hkMenu); Add("Full-screen hotkey", _hkFull);
        Add("Active-window hotkey", _hkWin); Add("Repeat-last hotkey", _hkRepeat);
        Add("Monitor hotkeys (1;2;3…)", _hkMon);
        Add("Preset hotkey", _hkPreset); Add("Preset region", _presetInfo); Add("", _useLast);
        Add("Fixed region width", _fixW); Add("Fixed region height", _fixH); Add("Interval (seconds)", _interval);
        Add("Full-screen target", _target); Add("Default delay (s)", _delay);
        Add("Gap fill", _matte); Add("Dimmer darkness", _dim); Add("Auto effect", _effect);
        Add("", _editor); Add("", _toasts); Add("", _contrast); Add("", _prtsc);
        Add("", _error);

        var ok = new Button { Content = "Save", IsDefault = true, Width = 80, Margin = new Thickness(4), ToolTip = "Apply and store settings." };
        var cancel = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(4), ToolTip = "Close without changes." };
        ok.Click += (_, _) => Commit();
        // The window is shown modeless (Show, not ShowDialog), where IsCancel and
        // DialogResult do nothing — Cancel and Esc close explicitly.
        cancel.Click += (_, _) => Close();
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 12) };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        var root = new StackPanel(); root.Children.Add(grid); root.Children.Add(buttons);
        Content = root;
        TesselTheme.Apply(this);
    }

    private void UpdatePresetLabel() =>
        _presetInfo.Text = _presetRect is { Length: 4 } r ? $"{r[0]}, {r[1]} · {r[2]} × {r[3]} px" : "Not set";

    private void Commit()
    {
        var p = new Preferences
        {
            OutputFolder = _folder.Text.Trim(), ImageFormat = (string)_format.SelectedItem,
            HotkeyMenu = _hkMenu.Text.Trim(), HotkeyFullScreen = _hkFull.Text.Trim(),
            HotkeyActiveWindow = _hkWin.Text.Trim(), HotkeyRepeatLast = _hkRepeat.Text.Trim(),
            HotkeyMonitors = _hkMon.Text.Trim(),
            HotkeyPreset = _hkPreset.Text.Trim(), PresetRect = _presetRect,
            ExternalEditorPath = _extEditor.Text.Trim(),
            FullScreenTarget = _target.SelectedIndex == 1 ? "AllMonitors" : "CurrentMonitor",
            DefaultDelaySeconds = (int)_delay.SelectedItem, MatteColor = (string)_matte.SelectedItem,
            OpenEditorAfterCapture = _editor.IsChecked == true, ShowToasts = _toasts.IsChecked == true,
            HighContrastHighlight = _contrast.IsChecked == true, DimmerOpacity = _dim.Value / 100,
            EditorBounds = _prefs.EditorBounds, EditorMaximized = _prefs.EditorMaximized,
            LastCaptureRect = _prefs.LastCaptureRect,
            PrintScreenTakeover = _prtsc.IsChecked == true,
            PrintScreenSnippingPrior = _prefs.PrintScreenSnippingPrior,
            ShowTrayIcon = _prefs.ShowTrayIcon,
            AutoEffect = (string)_effect.SelectedItem,
            // Recording preferences are edited in the Recorder window; carry them over untouched.
            RecordingFps = _prefs.RecordingFps, RecordSystemAudio = _prefs.RecordSystemAudio,
            RecordMic = _prefs.RecordMic, RecordingQualityName = _prefs.RecordingQualityName,
            RecordWebcam = _prefs.RecordWebcam, WebcamCorner = _prefs.WebcamCorner,
            RecordMaxMinutes = _prefs.RecordMaxMinutes, RecordMaxMb = _prefs.RecordMaxMb,
        };
        // Print Screen takeover drives the menu hotkey: on = Print Screen (old key remembered), off = remembered key back.
        if (p.PrintScreenTakeover && !_prefs.PrintScreenTakeover)
        {
            var (menu, remembered) = PrintScreenPlan.Enable(p.HotkeyMenu, _prefs.HotkeyMenuBeforeTakeover);
            p.HotkeyMenu = menu; p.HotkeyMenuBeforeTakeover = remembered;
        }
        else if (!p.PrintScreenTakeover && _prefs.PrintScreenTakeover)
        {
            p.HotkeyMenu = PrintScreenPlan.Disable(p.HotkeyMenu, _prefs.HotkeyMenuBeforeTakeover);
            p.HotkeyMenuBeforeTakeover = "";
        }
        else p.HotkeyMenuBeforeTakeover = _prefs.HotkeyMenuBeforeTakeover;
        // Reject bad or duplicate hotkeys with a message instead of silently falling back to defaults.
        if (!int.TryParse(_fixW.Text, out int fw) || !int.TryParse(_fixH.Text, out int fh)
            || fw is < FixedRegion.MinSize or > FixedRegion.MaxSize || fh is < FixedRegion.MinSize or > FixedRegion.MaxSize)
        { _error.Text = $"Fixed region size must be {FixedRegion.MinSize} to {FixedRegion.MaxSize} pixels."; return; }
        if (!int.TryParse(_interval.Text, out int iv) || iv is < IntervalPlan.MinSeconds or > IntervalPlan.MaxSeconds)
        { _error.Text = $"Interval must be {IntervalPlan.MinSeconds} to {IntervalPlan.MaxSeconds} seconds."; return; }
        p.FixedWidth = fw; p.FixedHeight = fh; p.IntervalSeconds = iv;
        if (!HotkeyList.TryParse(p.HotkeyMonitors, out var monKeys))
        { _error.Text = "Monitor hotkeys: invalid or duplicate entry. Example: Ctrl+Alt+Shift+1;Ctrl+Alt+Shift+2"; return; }
        var keys = new List<(string, string)> { ("Menu", p.HotkeyMenu), ("Full-screen", p.HotkeyFullScreen), ("Active-window", p.HotkeyActiveWindow), ("Repeat-last", p.HotkeyRepeatLast) };
        if (!string.IsNullOrWhiteSpace(p.HotkeyPreset)) keys.Add(("Preset", p.HotkeyPreset));
        foreach (var (name, text) in keys)
            if (!HotkeySpec.TryParse(text, out _)) { _error.Text = $"{name} hotkey is not valid. Example: Win+Shift+X"; return; }
        var everyKey = keys.Select(k => { HotkeySpec.TryParse(k.Item2, out var sp); return sp; }).Concat(monKeys).ToList();
        if (everyKey.Distinct().Count() != everyKey.Count)
        { _error.Text = "Each hotkey must be different."; return; }
        string? problem = _apply(p);
        if (problem is not null) { _error.Text = problem; return; }
        Close();
    }
}
