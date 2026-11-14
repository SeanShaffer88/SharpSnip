using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using Snipit.Core;
using WinForms = System.Windows.Forms;

namespace Snipit;

/// <summary>Owns tray icon, hotkeys, preferences and the router for the process lifetime.</summary>
internal sealed class AppHost : IDisposable
{
    private readonly HotkeyManager _hotkeys = new();
    private readonly UiaWorker _uia = new();
    private readonly WinForms.NotifyIcon _tray;
    private readonly ModeRouter _router;
    private Preferences _prefs;
    private string? _lastPath;
    private SettingsWindow? _settings;
    private RecorderWindow? _recorderWindow;
    private IRecording? _recorder;
    private DrawOverlayWindow? _drawOverlay;
    private string? _recordPath;
    private System.Windows.Threading.DispatcherTimer? _schedTimer;
    private RecordRequest? _schedReq;
    private DateTime _schedStart;
    private DateTime? _schedStop;
    private bool _schedStarted;
    private int _schedDurationMinutes;
    private WinForms.ToolStripMenuItem? _intervalItem;
    private WinForms.ToolStripMenuItem? _stopRecordItem;
    private WinForms.ToolStripMenuItem? _historyItem;
    private CancellationTokenSource? _interval;
    private readonly EventWaitHandle _showSignal = new(false, EventResetMode.AutoReset, Program.ShowEventName);
    private readonly RegisteredWaitHandle _showWait;

    public AppHost()
    {
        _prefs = PrefsStore.Load(AppPaths.PrefsPath, out string? warn);
        if (WorkflowTrust.IsTampered(_prefs, TrustKey.Get()))
        {
            _prefs.WorkflowsLocked = true;
            warn = (warn is null ? "" : warn + " ") + "Workflow and editor settings changed outside Snipit, so they are paused. Open Settings and press Save to approve them.";
            Log.Warn("Workflow settings changed outside Snipit; program-launching steps are blocked until the user saves Settings.");
        }
        else WorkflowTrust.Approve(_prefs, TrustKey.Get());
        _router = new ModeRouter(_prefs, _uia, Notify, SavePrefs);
        Program.UiErrorReported += msg => Application.Current?.Dispatcher.InvokeAsync(() => Notify("Snipit", "Something went wrong and was contained: " + msg + ". Details are in the log.", null));

        using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
        _tray = new WinForms.NotifyIcon
        {
            Icon = iconStream is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(iconStream),
            Text = "Snipit", Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.MouseClick += (_, e) => { if (e.Button == WinForms.MouseButtons.Left) _ = _router.RunSessionAsync(); };
        _tray.BalloonTipClicked += (_, _) => OpenLastFolder();
        _tray.Visible = _prefs.ShowTrayIcon;
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) =>
            Application.Current?.Dispatcher.InvokeAsync(() => { RestoreTrayIcon(); ShowSettings(); }),
            null, Timeout.Infinite, executeOnlyOnce: false);
        // Print Screen is Snipit's only global hotkey, unconditionally (owner
        // direction, 2026-11-13 — no other hotkeys, no setting, no conflict errors):
        // the Windows "Print Screen opens Snipping Tool" switch is turned off for
        // as long as Snipit runs, and Dispose restores it on exit. The switch's
        // original state is recorded once and kept across runs, so a crash cannot
        // make a later start mistake the leftover override for the original state.
        try
        {
            if (!_prefs.PrintScreenPriorRecorded)
            {
                _prefs.PrintScreenSnippingPrior = PrintScreenKey.Read() ?? -1;
                _prefs.PrintScreenPriorRecorded = true;
                SavePrefs();
            }
            PrintScreenKey.Write(0);   // assert every start: the switch may have been flipped back outside Snipit
        }
        catch (Exception ex) { Log.Error("Could not apply the Print Screen override", ex); }

        _hotkeys.HotkeyPressed += id =>
        {
            if (id == HotkeyManager.IdMenu) _ = _router.RunSessionAsync();
        };
        _hotkeys.RegisterPrintScreen();
        if (warn is not null) Notify("Snipit", warn, null);
        MonitorEnumerator.Refresh();
        Log.Info("Snipit started");
        _ = Task.Run(WgcCapture.Prewarm);   // §1.4: warm the WGC pipeline so the first capture is not the slow one
    }

    private WinForms.ContextMenuStrip BuildMenu()
    {
        var m = new WinForms.ContextMenuStrip();
        WinForms.ToolStripMenuItem Item(string text, string tip, Action a)
        {
            var i = new WinForms.ToolStripMenuItem(text) { ToolTipText = tip };
            i.Click += (_, _) => a();
            m.Items.Add(i);
            return i;
        }
        Item("Capture…", "Open the mode menu (same as Print Screen).", () => _ = _router.RunSessionAsync());
        Item("Scrolling capture…", "Pick a scrollable element and capture its full content as one tall image.", () => _ = _router.RunSessionAsync(scroll: true));
        Item("Pick colour…", "Pick any pixel on screen: shows its colour in a magnifier and copies it as HEX, RGB, HTML, C++ or Delphi text (1-5 or Tab changes the format).", () => _ = _router.RunColorPickAsync());
        Item("Full screen now", "Capture the configured monitor straight to the clipboard.", () => _ = _router.RunInstantFullScreenAsync());
        _intervalItem = Item("Start interval capture", "Capture the configured monitor every N seconds into a new folder. Click again to stop.", () => _ = ToggleIntervalAsync());
        Item("Record screen…", "Record a monitor (or the last capture region) to an MP4 video.", ShowRecorder);
        Item("Draw on screen", "Draw over the screen with the mouse (1-5 change colour, C clears, Esc finishes). Strokes are captured and recorded like anything else on screen.", () => ToggleDrawOverlay());
        _stopRecordItem = Item("Stop recording", "Finish the current recording and save the MP4.", () => _ = StopRecordingAsync());
        Item("Export with FFmpeg…", "Convert a recording or GIF to WebM, AV1, MKV, a palette GIF or audio-only, using the bundled FFmpeg.", () =>
        {
            var win = new ExportWindow(_recordPath, _prefs, Notify);
            win.Show();
            win.Activate();
        });
        _stopRecordItem.Enabled = false;
        Item("Repeat last capture", "Capture the same screen rectangle as the previous capture.", () => _ = _router.RunRepeatLastAsync());
        Item("Capture preset region", "Capture the preset region set in Settings.", () => _ = _router.RunPresetAsync());
        m.Items.Add(new WinForms.ToolStripSeparator());
        _historyItem = new WinForms.ToolStripMenuItem("Recent captures") { ToolTipText = "Reopen a recent capture in the editor (recordings play in your default player)." };
        m.Items.Add(_historyItem);
        Item("Open output folder", "Open the folder where captures are saved.", () => OpenFolder(Exporter.OutputFolder(_prefs)));
        Item("Open log folder", "Logs stay on this computer.", () => OpenFolder(AppPaths.LogDir));
        Item("Settings…", "Hotkeys, output folder, delay and more.", ShowSettings);
        Item("Hide tray icon", "Hide the tray icon. Run Snipit again (for example from the Start menu) to bring it back.", HideTrayIcon);
        m.Items.Add(new WinForms.ToolStripSeparator());
        Item("Give Print Screen back", "Hand the Print Screen key back to Windows now (Snipping Tool). Snipit takes it again the next time it starts.", GivePrintScreenBack);
        Item("Exit", "Quit Snipit. Print Screen goes back to what it did before.", () => Application.Current.Shutdown());
        m.Opening += (_, _) => RebuildHistory();
        return m;
    }

    private static readonly string[] HistoryExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".mp4" };

    /// <summary>Rebuilds the Recent captures submenu from the output folder (timestamp-named stills and recordings, newest first).</summary>
    private void RebuildHistory()
    {
        if (_historyItem is null) return;
        _historyItem.DropDownItems.Clear();
        var files = new List<FileInfo>();
        try
        {
            var dir = new DirectoryInfo(Exporter.OutputFolder(_prefs));
            if (dir.Exists)
                foreach (var f in dir.EnumerateFiles())
                {
                    string n = f.Name;
                    bool ours = n.StartsWith("recording-", StringComparison.OrdinalIgnoreCase)
                        || (n.Length > 15 && char.IsDigit(n[0]) && char.IsDigit(n[7]) && n[8] == '-');
                    if (ours && HistoryExtensions.Contains(f.Extension.ToLowerInvariant())) files.Add(f);
                }
        }
        catch (Exception ex) { Log.Warn("History scan failed: " + ex.Message); }
        files.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
        if (files.Count == 0)
        {
            _historyItem.DropDownItems.Add(new WinForms.ToolStripMenuItem("No captures yet") { Enabled = false });
            return;
        }
        foreach (var f in files.Take(10))
        {
            string path = f.FullName;
            var item = new WinForms.ToolStripMenuItem($"{f.Name} · {f.LastWriteTime:HH:mm}") { ToolTipText = path };
            item.Click += (_, _) => OpenHistoryFile(path);
            _historyItem.DropDownItems.Add(item);
        }
    }

    private void OpenHistoryFile(string path)
    {
        bool recording = Path.GetFileName(path).StartsWith("recording-", StringComparison.OrdinalIgnoreCase)
                         || path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        if (recording)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { Notify("Snipit", "Could not open that recording: " + ex.Message, null); }
            return;
        }
        try
        {
            var img = new BitmapImage();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = fs;
                img.EndInit();
            }
            img.Freeze();
            var cap = new CaptureResult(img, new RectPx(0, 0, img.PixelWidth, img.PixelHeight), false,
                img.PixelWidth * (double)img.PixelHeight / 1_000_000, 1);
            var win = new EditorWindow(cap, new CaptureMeta("History"), 1, 0, _prefs, SavePrefs, Notify);
            win.Show();
            win.Activate();
        }
        catch (Exception ex)
        {
            Log.Error("Could not reopen " + path, ex);
            Notify("Snipit", "Could not open that capture: " + ex.Message, null);
        }
    }

    private void ShowSettings()
    {
        if (_settings is not null) { _settings.Activate(); return; }
        _settings = new SettingsWindow(_prefs.Sanitize(), Apply, msg => Notify("Snipit", msg, null));
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    private void ShowRecorder()
    {
        if (_recorderWindow is not null) { _recorderWindow.Activate(); return; }
        _recorderWindow = new RecorderWindow(MonitorEnumerator.Refresh(), _prefs,
            StartRecordingAsync, StopRecordingAsync, paused => _recorder?.SetPaused(paused), RecorderState,
            ScheduleRecording, CancelSchedule, () => _schedReq is not null,
            m => ToggleDrawOverlay(m),
            () => _router.PickRegionAsync(),
            r => { _prefs.RecordingRegion = new[] { r.X, r.Y, r.Width, r.Height }; SavePrefs(); });
        _recorderWindow.Closed += (_, _) => _recorderWindow = null;
        _recorderWindow.Show();
    }

    /// <summary>Starts the recorder; returns an error line for the window, or null on success.</summary>
    private async Task<string?> StartRecordingAsync(RecordRequest req)
    {
        if (_recorder is not null) return "A recording is already running.";
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            return "Recording needs Windows 10 version 2004 (build 19041) or later.";
        MonitorInfo target = req.Monitor;
        RectPx? crop = null;
        if (req.UseRegion)
        {
            if (req.RegionRect is not { } region) return "No recording region — pick one in the Recorder first.";
            var monitorsNow = MonitorEnumerator.Refresh();
            if (RegionCrop.Resolve(region, monitorsNow) is not { } hit) return "The recording region is not on any current monitor.";
            target = monitorsNow[hit.MonitorIndex]; crop = hit.Crop;
        }
        string path = Path.Combine(Exporter.OutputFolder(_prefs),
            req.Gif ? RecordingPlan.GifFileName(DateTime.Now) : RecordingPlan.FileName(DateTime.Now));
        IRecording? rec = req.Gif
            ? await GifRecorder.StartAsync(target, path, req.Fps, crop,
                new WebcamOptions(req.Webcam, req.WebcamCorner), req.MaxMinutes * 60, req.MaxMb * 1_000_000L, req.SizeName)
            : await ScreenRecorder.StartAsync(target, path, req.Fps, crop, new AudioOptions(req.SystemAudio, req.Mic), req.Quality,
                new WebcamOptions(req.Webcam, req.WebcamCorner), req.MaxMinutes * 60, req.MaxMb * 1_000_000L, req.SizeName);
        if (rec is null) return "Recording could not start (capture or encoder unavailable).";
        rec.LimitReached += OnRecordingLimit;
        _recorder = rec; _recordPath = path;
        _prefs.RecordingFps = RecordingPlan.ClampFps(req.Fps);
        _prefs.RecordSystemAudio = req.SystemAudio; _prefs.RecordMic = req.Mic;
        _prefs.RecordingQualityName = RecordingQuality.Sanitize(req.Quality);
        _prefs.RecordingSizeName = RecordingSize.Sanitize(req.SizeName);
        _prefs.RecordWebcam = req.Webcam; _prefs.WebcamCorner = PipLayout.ParseCorner(req.WebcamCorner).ToString();
        _prefs.RecordMaxMinutes = Math.Clamp(req.MaxMinutes, 0, 1440); _prefs.RecordMaxMb = Math.Clamp(req.MaxMb, 0, 102400); SavePrefs();
        if (_stopRecordItem is not null) _stopRecordItem.Enabled = true;
        Log.Info($"Recording started: {path}");
        return null;
    }

    private void ToggleDrawOverlay(MonitorInfo? monitor = null)
    {
        if (_drawOverlay is not null) { _drawOverlay.Close(); _drawOverlay = null; return; }
        var monitors = MonitorEnumerator.Refresh();
        var (cx, cy) = MonitorEnumerator.CursorPos();
        MonitorInfo target = monitor
            ?? VirtualDesktop.MonitorAt(monitors, cx, cy)
            ?? monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        _drawOverlay = new DrawOverlayWindow(target);
        _drawOverlay.Closed += (_, _) => _drawOverlay = null;
        _drawOverlay.Show();
        _drawOverlay.Activate();
    }

    private void OnRecordingLimit()
    {
        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            string? path = _recordPath;
            await StopRecordingAsync();
            Notify("Recording stopped", "A recording limit was reached.", path);
        });
    }

    /// <summary>Arms a one-shot scheduled recording; returns an error line for the window, or null on success.</summary>
    private string? ScheduleRecording(RecordRequest req, string atText, string durText)
    {
        if (_recorder is not null) return "A recording is already running.";
        if (_schedReq is not null) return "A scheduled recording is already armed — cancel it first.";
        if (!TimeSpan.TryParse(atText, out var at) || at < TimeSpan.Zero || at >= TimeSpan.FromDays(1))
            return "Start time must be HH:MM (24-hour).";
        if (!int.TryParse(durText, out int dur) || dur is < 0 or > 600)
            return "Duration must be 0 to 600 minutes (0 = until you stop it).";
        var now = DateTime.Now;
        var start = now.Date + at;
        if (start <= now.AddSeconds(5)) start = start.AddDays(1);
        _schedReq = req; _schedStart = start; _schedStop = null; _schedStarted = false; _schedDurationMinutes = dur;
        _schedTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _schedTimer.Tick += OnScheduleTick;
        _schedTimer.Start();
        Log.Info($"Recording scheduled for {start:yyyy-MM-dd HH:mm} ({dur} min)");
        return null;
    }

    private void CancelSchedule()
    {
        _schedTimer?.Stop(); _schedTimer = null;
        _schedReq = null; _schedStop = null; _schedStarted = false;
    }

    private async void OnScheduleTick(object? sender, EventArgs e)
    {
        if (_schedReq is null) { CancelSchedule(); return; }
        var now = DateTime.Now;
        if (!_schedStarted && now >= _schedStart)
        {
            _schedStarted = true;
            var req = _schedReq;
            string? err = await StartRecordingAsync(req);
            if (err is not null) { Log.Error("Scheduled recording failed to start: " + err, null); Notify("Scheduled recording failed", err, null); CancelSchedule(); return; }
            Notify("Recording started", "The scheduled recording is running.", _recordPath);
            _schedStop = _schedDurationMinutes > 0 ? now.AddMinutes(_schedDurationMinutes) : null;
            if (_schedStop is null) CancelScheduleKeepRecording();
        }
        else if (_schedStarted && _schedStop is { } stop && now >= stop)
        {
            await StopRecordingAsync();
            CancelSchedule();
        }
    }

    private void CancelScheduleKeepRecording()
    {
        _schedTimer?.Stop(); _schedTimer = null;
        _schedReq = null; _schedStop = null; _schedStarted = false;
    }

    private (bool Recording, string? Path, TimeSpan Elapsed, bool Paused) RecorderState()
    {
        if (_recorder is null) return (false, _recordPath, TimeSpan.Zero, false);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) return (true, _recordPath, TimeSpan.Zero, false);
        return (true, _recordPath, _recorder.Elapsed, _recorder.IsPaused);
    }

    private async Task StopRecordingAsync()
    {
        var rec = _recorder;
        if (rec is null) return;
        _recorder = null;
        if (_stopRecordItem is not null) _stopRecordItem.Enabled = false;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) return; // unreachable: a recorder can only exist on 19041+
        string path = rec.OutputPath;
        try { await rec.StopAsync(); }
        catch (Exception ex)
        {
            Log.Error("Stopping the recording failed", ex);
            rec.Dispose();
            Notify("Recording problem", "Stopping the recording failed. Check " + path + " and the log.", File.Exists(path) ? path : null);
            return;
        }
        string? warning = rec.Warning;
        rec.Dispose();
        if (warning is not null) Notify("Recording saved with a problem", warning, File.Exists(path) ? path : null);
        else Notify("Recording saved", Path.GetFileName(path), path);
        WorkflowRunner.MaybeRun(_prefs, "Recording", path, Notify);
    }

    private void GivePrintScreenBack()
    {
        _hotkeys.UnregisterPrintScreen();
        try
        {
            if (PrintScreenKey.Restore(_prefs)) SavePrefs();
            Notify("Snipit", "Print Screen is back with Windows. Snipit takes it again next time it starts.", null);
        }
        catch (Exception ex)
        {
            Log.Error("Could not give Print Screen back", ex);
            Notify("Snipit", "Could not change the Print Screen switch. See the log.", null);
        }
    }

    private void HideTrayIcon()
    {
        _tray.Visible = false;
        _prefs.ShowTrayIcon = false;
        SavePrefs();
    }

    private void RestoreTrayIcon()
    {
        _tray.Visible = true;
        if (!_prefs.ShowTrayIcon) { _prefs.ShowTrayIcon = true; SavePrefs(); }
    }

    /// <summary>
    /// Persists the saved preferences. There is nothing left to conflict: the
    /// hotkeys are gone (Print Screen is unconditional and owned by startup/exit),
    /// so a save can no longer fail partway the way the old hotkey registration did
    /// (owner field report, 2026-11-13: a hotkey owned by another program silently
    /// ate every Settings save). Returns null — no warning path remains.
    /// </summary>
    private string? Apply(Preferences p)
    {
        _prefs = p.Sanitize();
        _prefs.WorkflowsLocked = false;
        WorkflowTrust.Approve(_prefs, TrustKey.Get());   // the user just reviewed and saved these settings
        _router.Prefs = _prefs;
        SavePrefs();
        return null;
    }

    // ---- interval capture ----------------------------------------------------------------------

    private async Task ToggleIntervalAsync()
    {
        if (_interval is not null) { _interval.Cancel(); return; }
        if (_router.IsBusy) { Notify("Snipit", "Finish the current capture first.", null); return; }

        var prefs = _prefs;
        int seconds = IntervalPlan.SanitizeSeconds(prefs.IntervalSeconds);
        var target = IntervalCapture.ResolveStartTarget(FullScreenTarget.Parse(prefs.FullScreenTarget));
        string folder = Path.Combine(Exporter.OutputFolder(prefs), IntervalPlan.FolderName(DateTime.Now));
        _interval = new CancellationTokenSource();
        var ct = _interval.Token;
        if (_intervalItem is not null) _intervalItem.Text = "Stop interval capture";
        Notify("Interval capture started", $"Every {seconds} s into {folder}. Stop it from the tray menu.", folder);

        int shots = 0, failures = 0;
        // GIF/APNG runs become ONE animated file (looping, frame delay = the interval), not stills.
        // Disposed explicitly in finally, before the completion notice, so the file is final by then.
        var gif = AnimationPlan.Kind(prefs.ImageFormat) == "gif" ? new AnimatedGifSink(folder) : null;
        var apng = AnimationPlan.Kind(prefs.ImageFormat) == "apng" ? new AnimatedApngSink(folder) : null;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
            do
            {
                if (!_router.IsBusy)   // never fight an interactive capture; that tick is skipped
                {
                    try
                    {
                        if (gif is not null)
                        {
                            var cap = IntervalCapture.CaptureBitmap(prefs, target);
                            if (!gif.Add(cap, GifPlan.DelayHundredths(seconds)))
                                throw new InvalidOperationException("Frame size changed during the run (display layout changed).");
                        }
                        else if (apng is not null)
                        {
                            var cap = IntervalCapture.CaptureBitmap(prefs, target);
                            if (!apng.Add(cap, GifPlan.DelayHundredths(seconds)))
                                throw new InvalidOperationException("Frame size changed during the run (display layout changed).");
                        }
                        else IntervalCapture.CaptureOnce(prefs, target, folder, shots + 1);
                        shots++; failures = 0;
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Log.Error("Interval frame failed", ex);
                        if (++failures >= 3) { Notify("Interval capture stopped", "Three frames in a row failed. See the log.", null); break; }
                    }
                }
                if (shots >= IntervalPlan.MaxShots) { Notify("Interval capture stopped", $"Reached {IntervalPlan.MaxShots} frames.", folder); break; }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { /* user stopped it */ }
        finally
        {
            gif?.Dispose();
            apng?.Dispose();
            _interval.Dispose();
            _interval = null;
            if (_intervalItem is not null) _intervalItem.Text = "Start interval capture";
            string? animPath = gif is not null && gif.Frames > 0 ? gif.FinalPath : apng is not null && apng.Frames > 0 ? apng.FinalPath : null;
            Notify("Interval capture finished",
                   animPath is not null ? $"{shots} frame(s) animated in {animPath}" : $"{shots} frame(s) in {folder}",
                   animPath ?? folder);
        }
    }

    private void SavePrefs()
    {
        try
        {
            if (!_prefs.WorkflowsLocked) WorkflowTrust.Approve(_prefs, TrustKey.Get());   // while locked the old hash stays, so the file keeps reading as changed
            PrefsStore.Save(AppPaths.PrefsPath, _prefs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Error("Could not save settings", ex); }
    }

    private void Notify(string title, string text, string? path)
    {
        _lastPath = path ?? _lastPath;
        Log.Info($"{title}: {text}");
        if (!_prefs.ShowToasts) return;
        _tray.ShowBalloonTip(3000, title, text, WinForms.ToolTipIcon.Info);
    }

    private void OpenLastFolder()
    {
        if (_lastPath is null) return;
        if (File.Exists(_lastPath)) Process.Start("explorer.exe", $"/select,\"{_lastPath}\"");
        else OpenFolder(Exporter.OutputFolder(_prefs));
    }

    private static void OpenFolder(string dir)
    {
        try { Directory.CreateDirectory(dir); Process.Start("explorer.exe", $"\"{dir}\""); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Warn("Could not open " + dir + ": " + ex.Message); }
    }

    public void Dispose()
    {
        _schedTimer?.Stop();
        if (_recorder is not null && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            try { _recorder.StopAsync().GetAwaiter().GetResult(); } catch (Exception ex) { Log.Error("Stopping the recording on exit failed", ex); }
            _recorder.Dispose(); _recorder = null;
        }
        _interval?.Cancel();
        _showWait.Unregister(null);
        _showSignal.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _hotkeys.Dispose();
        // Give Print Screen back (owner direction, 2026-11-13): restore the Windows
        // switch to the state recorded before the override, and clear the record so
        // the next start records fresh. A crash skips this — the record survives, so
        // the next clean exit still restores the true original state.
        try { if (PrintScreenKey.Restore(_prefs)) SavePrefs(); }
        catch (Exception ex) { Log.Error("Could not restore the Print Screen switch on exit", ex); }
        _uia.Dispose();
        Log.Info("Snipit stopped");
    }
}
