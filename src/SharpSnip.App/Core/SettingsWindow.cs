using System.Windows;
using System.Windows.Controls;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>Every user-facing option (charter: toggles for everything, tooltip on every control).</summary>
internal sealed class SettingsWindow : Window
{
    private readonly Preferences _prefs;
    private readonly Func<Preferences, string?> _apply;
    private readonly Action<string>? _warn;
    private readonly TextBox _folder = new(), _fixW = new(), _fixH = new(), _interval = new(), _extEditor = new(), _ffmpeg = new();
    private readonly TextBlock _presetInfo = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _useLast = new() { Content = "Use last capture region", Padding = new Thickness(8, 3, 8, 3) };
    private int[]? _presetRect;
    private readonly ComboBox _wfCapture = new(), _wfRecording = new();
    private readonly Button _wfManage = new() { Content = "Manage workflows…", Padding = new Thickness(8, 3, 8, 3) };
    private List<WorkflowDef> _workflows = new();
    private readonly ComboBox _format = new(), _target = new(), _delay = new(), _matte = new(), _effect = new();
    private readonly CheckBox _toasts = new() { Content = "Show notifications" };
    private readonly CheckBox _contrast = new() { Content = "High-contrast highlight" };
    private readonly Slider _dim = new() { Minimum = 20, Maximum = 90, Width = 200, TickFrequency = 10, IsSnapToTickEnabled = false };
    private readonly TextBlock _error = new() { Foreground = TesselTheme.Danger, TextWrapping = TextWrapping.Wrap };

    public SettingsWindow(Preferences prefs, Func<Preferences, string?> apply, Action<string>? warn = null)
    {
        _prefs = prefs; _apply = apply; _warn = warn;
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
        _ffmpeg.Text = prefs.FfmpegPath; _ffmpeg.ToolTip = "Path to your own ffmpeg.exe, used instead of the bundled FFmpeg for exports. Empty = the bundled copy.";
        _format.SelectedItem = prefs.ImageFormat; _format.ToolTip = "File format for saved captures. PNG and TIFF keep transparency; JPEG, BMP, GIF and PDF drop it. A still GIF is a single frame; interval captures in GIF become one animated file. PDF holds one image, losslessly.";
        _presetRect = prefs.PresetRect;
        _useLast.ToolTip = "Sets the preset region to the rectangle of your most recent capture.";
        _useLast.Click += (_, _) =>
        {
            if (_prefs.LastCaptureRect is { Length: 4 } r) { _presetRect = (int[])r.Clone(); }
            UpdatePresetLabel();
        };
        UpdatePresetLabel();
        _workflows = prefs.Workflows.Select(w => new WorkflowDef { Name = w.Name, ApplyTo = w.ApplyTo, Steps = new List<string>(w.Steps) }).ToList();
        RefreshWorkflowCombos(prefs.CaptureWorkflow, prefs.RecordingWorkflow);
        _wfManage.ToolTip = "Create and edit the named workflows that can run after a capture or a recording.";
        _wfManage.Click += (_, _) =>
        {
            var win = new WorkflowWindow(_workflows) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _workflows = win.Result;
                RefreshWorkflowCombos(SelectedWorkflow(_wfCapture), SelectedWorkflow(_wfRecording));
            }
        };
        _fixW.Text = prefs.FixedWidth.ToString(); _fixW.ToolTip = "Width in pixels of the fixed-size region (16 to 16384).";
        _fixH.Text = prefs.FixedHeight.ToString(); _fixH.ToolTip = "Height in pixels of the fixed-size region (16 to 16384).";
        _interval.Text = prefs.IntervalSeconds.ToString(); _interval.ToolTip = "Seconds between frames for interval capture (1 to 86400). Start it from the tray menu.";
        _target.SelectedIndex = prefs.FullScreenTarget == "AllMonitors" ? 1 : 0; _target.ToolTip = "What the one-shot hotkey captures.";
        _delay.SelectedItem = prefs.DefaultDelaySeconds; _delay.ToolTip = "Countdown before every capture, in seconds. Useful for menus and tooltips.";
        _matte.SelectedItem = prefs.MatteColor; _matte.ToolTip = "Fill for empty corners when monitors form an L-shape.";
        _effect.SelectedItem = AutoEffects.Sanitize(prefs.AutoEffect); _effect.ToolTip = "Applied automatically to every capture. Border draws a thin black frame; Grayscale converts to grey.";
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
        Add("Output folder", _folder); Add("Image format", _format); Add("External editor", _extEditor); Add("FFmpeg override", _ffmpeg);
        Add("Preset region", _presetInfo); Add("", _useLast);
        Add("After capture", _wfCapture); Add("After recording", _wfRecording); Add("Workflows", _wfManage);
        Add("Fixed region width", _fixW); Add("Fixed region height", _fixH); Add("Interval (seconds)", _interval);
        Add("Full-screen target", _target); Add("Default delay (s)", _delay);
        Add("Gap fill", _matte); Add("Dimmer darkness", _dim); Add("Auto effect", _effect);
        Add("", _toasts); Add("", _contrast);
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

    private void RefreshWorkflowCombos(string? capture, string? recording)
    {
        foreach (var (combo, selected) in new[] { (_wfCapture, capture), (_wfRecording, recording) })
        {
            combo.Items.Clear();
            combo.Items.Add("(none)");
            foreach (var w in _workflows) combo.Items.Add(w.Name);
            combo.SelectedItem = selected is not null && combo.Items.Contains(selected) ? selected : "(none)";
        }
    }

    private static string SelectedWorkflow(ComboBox combo) =>
        combo.SelectedItem as string is { } s && s != "(none)" ? s : "";

    private void Commit()
    {
        var p = new Preferences
        {
            OutputFolder = _folder.Text.Trim(), ImageFormat = (string)_format.SelectedItem,
            PresetRect = _presetRect,
            ExternalEditorPath = _extEditor.Text.Trim(),
            FfmpegPath = _ffmpeg.Text.Trim(),
            Workflows = _workflows, CaptureWorkflow = SelectedWorkflow(_wfCapture), RecordingWorkflow = SelectedWorkflow(_wfRecording),
            FullScreenTarget = _target.SelectedIndex == 1 ? "AllMonitors" : "CurrentMonitor",
            DefaultDelaySeconds = (int)_delay.SelectedItem, MatteColor = (string)_matte.SelectedItem,
            ShowToasts = _toasts.IsChecked == true,
            HighContrastHighlight = _contrast.IsChecked == true, DimmerOpacity = _dim.Value / 100,
            EditorBounds = _prefs.EditorBounds, EditorMaximized = _prefs.EditorMaximized,
            LastCaptureRect = _prefs.LastCaptureRect,
            PrintScreenSnippingPrior = _prefs.PrintScreenSnippingPrior,
            PrintScreenPriorRecorded = _prefs.PrintScreenPriorRecorded,
            ShowTrayIcon = _prefs.ShowTrayIcon,
            AutoEffect = (string)_effect.SelectedItem,
            // Recording preferences are edited in the Recorder window; carry them over untouched.
            RecordingFps = _prefs.RecordingFps, RecordSystemAudio = _prefs.RecordSystemAudio,
            RecordMic = _prefs.RecordMic, RecordingQualityName = _prefs.RecordingQualityName, RecordingSizeName = _prefs.RecordingSizeName,
            RecordWebcam = _prefs.RecordWebcam, WebcamCorner = _prefs.WebcamCorner,
            RecordMaxMinutes = _prefs.RecordMaxMinutes, RecordMaxMb = _prefs.RecordMaxMb,
            RecordingRegion = _prefs.RecordingRegion,
        };
        // Per-field validation (owner field report, 2026-11-13): one bad field must
        // never discard the whole save — an invalid entry keeps its previous value
        // and is named in the warning, everything else applies and persists.
        var problems = new List<string>();
        if (!int.TryParse(_fixW.Text, out int fw) || !int.TryParse(_fixH.Text, out int fh)
            || fw is < FixedRegion.MinSize or > FixedRegion.MaxSize || fh is < FixedRegion.MinSize or > FixedRegion.MaxSize)
        { fw = _prefs.FixedWidth; fh = _prefs.FixedHeight; problems.Add($"Fixed region size must be {FixedRegion.MinSize} to {FixedRegion.MaxSize} pixels — kept {fw}×{fh}."); }
        if (!int.TryParse(_interval.Text, out int iv) || iv is < IntervalPlan.MinSeconds or > IntervalPlan.MaxSeconds)
        { iv = _prefs.IntervalSeconds; problems.Add($"Interval must be {IntervalPlan.MinSeconds} to {IntervalPlan.MaxSeconds} seconds — kept {iv}."); }
        p.FixedWidth = fw; p.FixedHeight = fh; p.IntervalSeconds = iv;
        string? warning = _apply(p);
        if (warning is not null) problems.Add(warning);
        if (problems.Count > 0)
        {
            string msg = "Settings saved, with notes: " + string.Join(" ", problems);
            if (_warn is not null) _warn(msg); else _error.Text = msg;
        }
        Close();
    }
}
