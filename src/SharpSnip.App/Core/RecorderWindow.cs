using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// The recording surface: pick a monitor (or the last capture region), a frame
/// rate, Start / Stop. The host owns the recorder itself; this window only
/// drives it through the callbacks and mirrors its state on a timer, so a stop
/// from the tray shows here too.
/// </summary>
internal sealed class RecorderWindow : Window
{
    private readonly IReadOnlyList<MonitorInfo> _monitors;
    private readonly Func<RecordRequest, Task<string?>> _start;
    private readonly Func<Task> _stop;
    private readonly Action<bool> _setPaused;
    private readonly Func<(bool Recording, string? Path, TimeSpan Elapsed, bool Paused)> _state;
    private readonly Func<RecordRequest, string, string, string?> _schedule;
    private readonly Action _cancelSchedule;
    private readonly Func<bool> _isScheduled;
    private readonly Action<MonitorInfo> _draw;
    private readonly Func<Task<RectPx?>> _pickRegion;
    private readonly Action<RectPx> _saveRegion;
    private RectPx? _regionRect;
    private string _regionSource = "";
    private readonly Button _pickBtn = new() { Content = "Pick region…", Padding = new Thickness(14, 5, 14, 5) };
    private readonly TextBlock _regionInfo = new() { TextWrapping = TextWrapping.Wrap, Foreground = TesselTheme.Muted, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _drawBtn = new() { Content = "Draw on screen", Padding = new Thickness(14, 5, 14, 5) };
    private readonly TextBox _maxMin = new(), _maxMb = new(), _at = new(), _dur = new();
    private readonly Button _scheduleBtn = new() { Content = "Schedule", Padding = new Thickness(14, 5, 14, 5) };
    private readonly ComboBox _monitor = new(), _fps = new(), _quality = new(), _format = new(), _size = new();
    private readonly CheckBox _region = new() { Content = "Record a region instead of the whole monitor" };
    private readonly CheckBox _sysAudio = new() { Content = "System sound" };
    private readonly CheckBox _mic = new() { Content = "Microphone" };
    private readonly CheckBox _webcam = new() { Content = "Webcam overlay" };
    private readonly ComboBox _corner = new();
    private readonly Button _toggle = new() { Content = "Start recording", Padding = new Thickness(14, 5, 14, 5) };
    private readonly Button _pauseBtn = new() { Content = "Pause", Padding = new Thickness(14, 5, 14, 5) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = TesselTheme.Muted };
    private readonly TextBlock _error = new() { Foreground = TesselTheme.Danger, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _regionAvailable;
    private bool _busy;

    public RecorderWindow(IReadOnlyList<MonitorInfo> monitors, Preferences prefs,
        Func<RecordRequest, Task<string?>> start, Func<Task> stop, Action<bool> setPaused,
        Func<(bool Recording, string? Path, TimeSpan Elapsed, bool Paused)> state,
        Func<RecordRequest, string, string, string?> schedule, Action cancelSchedule, Func<bool> isScheduled,
        Action<MonitorInfo> draw, Func<Task<RectPx?>> pickRegion, Action<RectPx> saveRegion)
    {
        _monitors = monitors; _start = start; _stop = stop; _setPaused = setPaused; _state = state;
        _schedule = schedule; _cancelSchedule = cancelSchedule; _isScheduled = isScheduled; _draw = draw;
        _pickRegion = pickRegion; _saveRegion = saveRegion;
        Title = "SharpSnip — Record screen";
        SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _monitor.ItemsSource = monitors.Select(m => m.Label).ToList();
        int primary = 0;
        for (int i = 0; i < monitors.Count; i++) if (monitors[i].IsPrimary) { primary = i; break; }
        _monitor.SelectedIndex = primary;
        _monitor.ToolTip = "The monitor to record.";
        _fps.ItemsSource = new[] { 15, 24, 30, 60 };
        _fps.SelectedItem = RecordingPlan.ClampFps(prefs.RecordingFps);
        _fps.ToolTip = "Frames per second for the recording.";
        _quality.ItemsSource = RecordingQuality.All;
        _quality.SelectedItem = RecordingQuality.Sanitize(prefs.RecordingQualityName);
        _quality.ToolTip = "Encoding quality. Higher looks better and makes bigger files.";
        _size.ItemsSource = RecordingSize.All;
        _size.SelectedItem = RecordingSize.Sanitize(prefs.RecordingSizeName);
        _size.ToolTip = "Output size cap. Smaller sizes make smaller files; the recording is never upscaled.";
        _format.ItemsSource = new[] { "MP4", "GIF" };
        _format.SelectedItem = "MP4";
        _format.ToolTip = "GIF is silent, limited to 20 frames per second, and much larger per second — for short clips and sharing in chat.";
        _format.SelectionChanged += (_, _) => Refresh();
        _region.IsChecked = false;
        if (prefs.RecordingRegion is { Length: 4 } pr) { _regionRect = new RectPx(pr[0], pr[1], pr[2], pr[3]); _regionSource = "picked region"; }
        else if (prefs.LastCaptureRect is { Length: 4 } lr) { _regionRect = new RectPx(lr[0], lr[1], lr[2], lr[3]); _regionSource = "last capture"; }
        _regionAvailable = _regionRect is not null;
        _region.IsEnabled = _regionAvailable;
        _region.ToolTip = "Records only a rectangle of the screen instead of a whole monitor. Pick one with the button below, or reuse the rectangle of your most recent capture.";
        _pickBtn.ToolTip = "Drag a rectangle on the screen to use as the recording area. It is remembered for next time.";
        _pickBtn.Click += async (_, _) => await PickRegionAsync();
        RefreshRegionInfo();
        _sysAudio.IsChecked = prefs.RecordSystemAudio;
        _sysAudio.ToolTip = "Records what the computer plays (system sound) into the video.";
        _mic.IsChecked = prefs.RecordMic;
        _mic.ToolTip = "Records the default microphone into the video, mixed with the system sound.";
        _webcam.IsChecked = prefs.RecordWebcam;
        _webcam.ToolTip = "Shows the default camera in a corner of the recording. Uses the (slower) pixel path for every frame.";
        _corner.ItemsSource = new[] { "BottomRight", "BottomLeft", "TopRight", "TopLeft" };
        _corner.SelectedItem = PipLayout.ParseCorner(prefs.WebcamCorner).ToString();
        _corner.ToolTip = "Which corner the webcam overlay sits in.";
        _corner.IsEnabled = _webcam.IsChecked == true;
        _webcam.Checked += (_, _) => _corner.IsEnabled = true;
        _webcam.Unchecked += (_, _) => _corner.IsEnabled = false;
        _maxMin.Text = prefs.RecordMaxMinutes.ToString();
        _maxMin.ToolTip = "Stop the recording after this many minutes. 0 = no limit.";
        _maxMb.Text = prefs.RecordMaxMb.ToString();
        _maxMb.ToolTip = "Stop the recording when the file reaches this many MB. 0 = no limit.";
        _at.ToolTip = "Start time for a scheduled recording, 24-hour HH:MM. A time already past today means tomorrow.";
        _dur.ToolTip = "How many minutes the scheduled recording runs. 0 = until you stop it.";
        _scheduleBtn.ToolTip = "Arms a one-shot scheduled recording with the settings above.";
        _scheduleBtn.Click += (_, _) => OnSchedule();
        _drawBtn.ToolTip = "Draw over the selected monitor with the mouse — the strokes appear in the recording. 1-5 change colour, C clears, Esc finishes.";
        _drawBtn.Click += (_, _) =>
        {
            int idx = Math.Clamp(_monitor.SelectedIndex, 0, _monitors.Count - 1);
            _draw(_monitors[idx]);
        };
        _toggle.ToolTip = "Starts or stops the recording. Recordings save as MP4 in the output folder.";
        _pauseBtn.ToolTip = "Pauses the recording — the paused time is cut from the video and the audio. Resume continues seamlessly.";
        _pauseBtn.Click += (_, _) => { var (_, _, _, paused) = _state(); _setPaused(!paused); Refresh(); };
        _status.Text = "Ready.";

        var grid = new Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        int row = 0;
        void Add(string label, UIElement c)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, row); Grid.SetRow(c, row); Grid.SetColumn(c, 1);
            grid.Children.Add(l); grid.Children.Add(c); row++;
        }
        Add("Monitor", _monitor);
        var regionStack = new StackPanel();
        regionStack.Children.Add(_region);
        regionStack.Children.Add(_pickBtn);
        regionStack.Children.Add(_regionInfo);
        Add("", regionStack);
        Add("Format", _format);
        Add("Frame rate", _fps);
        Add("Quality", _quality);
        Add("Output size", _size);
        Add("Audio", _sysAudio);
        Add("", _mic);
        Add("", _webcam);
        Add("Webcam corner", _corner);
        Add("Max minutes", _maxMin);
        Add("Max size (MB)", _maxMb);
        Add("", _toggle);
        Add("", _pauseBtn);
        Add("Start at", _at);
        Add("For minutes", _dur);
        Add("", _scheduleBtn);
        Add("", _drawBtn);
        Add("", _status);
        Add("", _error);
        Content = grid;
        TesselTheme.Apply(this);

        _toggle.Click += async (_, _) => await ToggleAsync();
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        Refresh();
    }

    private RecordRequest? BuildRequest(out string? error)
    {
        error = null;
        if (!int.TryParse(_maxMin.Text, out int maxMin) || maxMin is < 0 or > 1440) { error = "Max minutes must be 0 to 1440."; return null; }
        if (!int.TryParse(_maxMb.Text, out int maxMb) || maxMb is < 0 or > 102400) { error = "Max size must be 0 to 102400 MB."; return null; }
        int idx = Math.Clamp(_monitor.SelectedIndex, 0, _monitors.Count - 1);
        int fps = _fps.SelectedItem is int f ? f : RecordingPlan.DefaultFps;
        return new RecordRequest(_monitors[idx], _region.IsChecked == true, fps,
            _sysAudio.IsChecked == true, _mic.IsChecked == true,
            _quality.SelectedItem as string ?? RecordingQuality.Default,
            _webcam.IsChecked == true, _corner.SelectedItem as string ?? "BottomRight",
            maxMin, maxMb, _format.SelectedItem as string == "GIF",
            _size.SelectedItem as string ?? RecordingSize.Default,
            _region.IsChecked == true ? _regionRect : null);
    }

    private void RefreshRegionInfo()
    {
        _regionInfo.Text = _regionRect is { } r
            ? $"Region: {r.Width}×{r.Height} at {r.X},{r.Y} ({_regionSource})."
            : "No region yet — pick one, or take a region capture first.";
    }

    private async Task PickRegionAsync()
    {
        Hide();
        RectPx? picked;
        try { picked = await _pickRegion(); }
        finally { Show(); Activate(); }
        if (picked is not { } r) return;
        _saveRegion(r);
        _regionRect = r; _regionSource = "picked region";
        _regionAvailable = true;
        _region.IsEnabled = true; _region.IsChecked = true;
        RefreshRegionInfo();
        _status.Text = "Recording region set.";
    }

    private void OnSchedule()
    {
        _error.Text = "";
        if (_isScheduled())
        {
            _cancelSchedule();
            _scheduleBtn.Content = "Schedule";
            _status.Text = "Schedule cancelled.";
            return;
        }
        var req = BuildRequest(out var error);
        if (req is null) { _error.Text = error; return; }
        string? err = _schedule(req, _at.Text.Trim(), _dur.Text.Trim());
        if (err is not null) { _error.Text = err; return; }
        _scheduleBtn.Content = "Cancel schedule";
        _status.Text = $"Scheduled: starts at {_at.Text.Trim()}, runs {_dur.Text.Trim()} min.";
    }

    private async Task ToggleAsync()
    {
        if (_busy) return;
        _error.Text = "";
        var (recording, _, _, _) = _state();
        _busy = true; _toggle.IsEnabled = false;
        try
        {
            if (recording) await _stop();
            else
            {
                var req = BuildRequest(out var buildError);
                if (req is null) { _error.Text = buildError; return; }
                string? error = await _start(req);
                if (error is not null) _error.Text = error;
            }
        }
        finally { _busy = false; _toggle.IsEnabled = true; Refresh(); }
    }

    private void Refresh()
    {
        var (recording, path, elapsed, paused) = _state();
        _toggle.Content = recording ? "Stop recording" : "Start recording";
        _pauseBtn.Content = paused ? "Resume" : "Pause";
        _pauseBtn.IsEnabled = recording;
        if (recording && !_isScheduled()) _scheduleBtn.Content = "Schedule";
        bool gif = _format.SelectedItem as string == "GIF";
        _monitor.IsEnabled = !recording; _fps.IsEnabled = !recording; _quality.IsEnabled = !recording && !gif; _format.IsEnabled = !recording;
        _sysAudio.IsEnabled = !recording && !gif; _mic.IsEnabled = !recording && !gif;
        _webcam.IsEnabled = !recording; _corner.IsEnabled = !recording && _webcam.IsChecked == true;
        _region.IsEnabled = !recording && _regionAvailable;
        _status.Text = recording
            ? paused ? $"Paused — {elapsed:mm\\:ss} → {path}" : $"Recording… {elapsed:mm\\:ss} → {path}"
            : path is not null ? $"Saved: {path}" : "Ready.";
    }
}
