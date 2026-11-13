using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

/// <summary>Drives one capture session through the state machine (§1): pick → delay → capture → edit/export.</summary>
internal sealed class ModeRouter
{
    private readonly AppStateMachine _sm = new();
    private readonly UiaWorker _uia;
    private readonly Action<string, string, string?> _notify;
    private readonly Action _persistPrefs;

    public ModeRouter(Preferences prefs, UiaWorker uia, Action<string, string, string?> notify, Action persistPrefs)
    {
        Prefs = prefs; _uia = uia; _notify = notify; _persistPrefs = persistPrefs;
        if (prefs.LastCaptureRect is { Length: 4 } r) LastRect = new RectPx(r[0], r[1], r[2], r[3]);
    }

    public Preferences Prefs { get; set; }
    public bool IsBusy => _sm.State != AppState.Idle;

    /// <summary>Rectangle of the most recent capture (physical px), for "repeat last". Persisted, so it survives a restart; use re-validates it against the current desktop.</summary>
    public RectPx? LastRect { get; private set; }

    private void RememberRect(RectPx rect)
    {
        LastRect = rect;
        Prefs.LastCaptureRect = new[] { rect.X, rect.Y, rect.Width, rect.Height };
        _persistPrefs();
    }

    // ---- full session (hotkey 1 / tray) --------------------------------------------------------

    public async Task RunSessionAsync(bool scroll = false)
    {
        if (IsBusy) return;
        _sm.TryTransition(AppState.ModeMenu);
        using var cts = new CancellationTokenSource();
        var monitors = MonitorEnumerator.Refresh();
        using var overlays = new OverlayHost(monitors, OverlayStyle.From(Prefs));
        using var hook = new InputHook();
        var ctx = new PickerContext { Overlays = overlays, Hook = hook, Prefs = Prefs, Uia = _uia, SavePrefs = _persistPrefs, DelaySeconds = Prefs.DefaultDelaySeconds, FullTarget = FullScreenTarget.Parse(Prefs.FullScreenTarget), ScrollCapture = scroll };

        // Watchdog (§11): a stuck overlay must never trap the user. 60 s without input ends the session.
        var dog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        dog.Tick += (_, _) =>
        {
            if (Environment.TickCount64 - hook.LastInputTick > 60_000) { Log.Warn("Watchdog: no input for 60 s, cancelling session"); cts.Cancel(); }
        };

        try
        {
            overlays.Show();
            hook.Start();
            dog.Start();

            PickResult? result = null;
            var mode = scroll ? PickMode.Element : PickMode.Menu;
            bool viaMouse = false;   // the switch that armed the next picker came from a bar click
            if (scroll) _sm.TryTransition(AppState.ElementPicking);
            while (result is null)
            {
                PickerBase picker = mode switch
                {
                    PickMode.Element => new ElementPicker(ctx),
                    PickMode.Region => new RegionPicker(ctx),
                    PickMode.FixedRegion => new FixedRegionPicker(ctx),
                    PickMode.Window => new WindowPicker(ctx),
                    PickMode.FullScreen => new FullScreenPicker(ctx),
                    PickMode.Freehand => new FreehandPicker(ctx),
                    _ => new MenuPicker(ctx),
                };
                picker.ArmedByMouse = viaMouse;
                viaMouse = false;
                var outcome = await picker.RunAsync(cts.Token);
                switch (outcome.Kind)
                {
                    case PickKind.Cancel:
                        if (mode == PickMode.Menu || cts.IsCancellationRequested) return;
                        mode = PickMode.Menu;                       // Esc in a picker → mode menu (§3A, §3D.3)
                        _sm.TryTransition(AppState.ModeMenu);
                        break;
                    case PickKind.Switch:
                        if (outcome.Target is { } t) ctx.FullTarget = t;
                        mode = outcome.Next;
                        viaMouse = outcome.ViaMouse;
                        if (!_sm.TryTransition(AppStateMachine.ForMode(mode))) Log.Warn($"Illegal state transition to {mode}");
                        break;
                    default:
                        result = outcome.Result;
                        break;
                }
            }

            _sm.TryTransition(AppState.Captured);
            await FinishCaptureAsync(result, ctx, overlays, hook, cts.Token);
        }
        catch (Exception ex)
        {
            Log.Error("Capture session failed", ex);
            _notify("SharpSnip", "Capture failed: " + ex.Message, null);
        }
        finally
        {
            dog.Stop();
            hook.Dispose();
            overlays.Dispose();
            _sm.ForceIdle();
        }
    }

    /// <summary>Colour picker: pick a pixel anywhere on screen and copy its colour text to the clipboard.</summary>
    public async Task RunColorPickAsync()
    {
        if (IsBusy) return;
        _sm.TryTransition(AppState.ModeMenu);
        DispatcherTimer? dog = null;
        try
        {
            var monitors = MonitorEnumerator.Refresh();
            var desktop = VirtualDesktop.Union(monitors);
            // Capture can wait for the graphics backend. Finish before showing overlays or installing hooks.
            var snapshot = await Task.Run(() => CaptureEngine.Capture(desktop, monitors, 0xFF000000u));
            using var cts = new CancellationTokenSource();
            using var overlays = new OverlayHost(monitors, OverlayStyle.From(Prefs));
            using var hook = new InputHook();
            var ctx = new PickerContext { Overlays = overlays, Hook = hook, Prefs = Prefs, Uia = _uia };
            var picker = new ColorPickerPicker(ctx, snapshot, monitors);
            dog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            dog.Tick += (_, _) =>
            {
                if (Environment.TickCount64 - hook.LastInputTick > 60_000) { Log.Warn("Watchdog: no input for 60 s, cancelling colour picker"); cts.Cancel(); }
            };
            overlays.Show();
            hook.Start();
            dog.Start();
            var outcome = await picker.RunAsync(cts.Token);
            if (outcome.Kind == PickKind.Picked && picker.Chosen is { } text)
            {
                if (ClipboardWriter.SetText(text)) _notify("Colour copied", text, null);
                else _notify("SharpSnip", $"Could not reach the clipboard. The colour is {text}.", null);
                _persistPrefs();   // remembers the chosen format
            }
        }
        catch (Exception ex)
        {
            Log.Error("Colour picker failed", ex);
            _notify("SharpSnip", "Colour picker failed: " + ex.Message, null);
        }
        finally
        {
            dog?.Stop();
            _sm.ForceIdle();
        }
    }

    private async Task FinishCaptureAsync(PickResult result, PickerContext ctx, OverlayHost overlays, InputHook hook, CancellationToken ct)
    {
        int delay = ctx.DelaySeconds;
        if (delay > 0)
        {
            overlays.HideAll();
            var cursor = MonitorEnumerator.CursorPos();
            var where = VirtualDesktop.MonitorAt(MonitorEnumerator.Current, cursor.X, cursor.Y) ?? MonitorEnumerator.Current[0];
            if (!await DelayTimer.RunAsync(delay, hook, where, ct)) { Log.Info("Countdown cancelled"); return; }
        }

        // Display layout may have changed during the countdown; recompute and continue (§3D.6).
        var monitors = MonitorEnumerator.Refresh();
        RectPx rect = result.Resolve is null
            ? result.Rect
            : result.Resolve(monitors) ?? (monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0]).Bounds; // target vanished → primary
        rect = rect.Intersect(VirtualDesktop.Union(monitors));
        if (rect.IsEmpty) { _notify("SharpSnip", "Nothing to capture: the target is no longer on screen.", null); return; }

        await overlays.HideForCaptureAsync();
        CaptureResult cap;
        if (ctx.ScrollCapture && ctx.ScrollTarget is { } scrollEl)
        {
            // Scrolling capture: the picked rect is the element's viewport; the result is its full content.
            cap = await ScrollCapture.CaptureAsync(scrollEl, rect, _uia, monitors, Prefs.MatteArgb, ct);
        }
        else
        {
            cap = CaptureEngine.Capture(rect, monitors, Prefs.MatteArgb);
            if (result.Polygon is { } polygon) cap = MaskOutside(cap, polygon);
        }
        RememberRect(rect);
        // Captures never open the editor (owner direction, 2026-11-13): clipboard
        // + file + workflow, always. The editor remains for deliberate opens only
        // (a past capture from the tray history). The Settings auto-effect, which
        // the editor used to apply on open, is applied to the capture itself here.
        var image = ApplyAutoEffect(cap.Image);
        ClipboardWriter.SetImage(image);
        if (cap.Megapixels > 100) _notify("SharpSnip", $"Large capture ({cap.Megapixels:F0} MP). Saving may be slow.", null);

        // Tear down input now the capture is done, so no hook outlives the session.
        hook.Dispose();
        overlays.Dispose();

        _sm.TryTransition(AppState.Exporting);
        string path = Exporter.Save(image, Prefs);
        _notify("Copied to clipboard", "Saved " + path, path);
        WorkflowRunner.MaybeRun(Prefs, "Capture", path, _notify);
    }

    /// <summary>The Settings auto-effect (Border / Grayscale) applied to a fresh capture. Any failure keeps the original image.</summary>
    private BitmapSource ApplyAutoEffect(BitmapSource image)
    {
        string effect = AutoEffects.Sanitize(Prefs.AutoEffect);
        if (effect == AutoEffects.Default) return image;
        try
        {
            var src = image.Format == PixelFormats.Bgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            src.CopyPixels(px, stride, 0);
            switch (effect)
            {
                case "Grayscale": PixelAdjust.Grayscale(px); break;
                case "Border": PixelAdjust.Border(px, w, h, Math.Max(2, Math.Min(w, h) / 120), 0, 0, 0); break;
                default: return image;
            }
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) { Log.Error("Auto effect failed", ex); return image; }
    }

    /// <summary>
    /// A bare region pick with no capture behind it — the Recorder's "Pick region…".
    /// Runs only the region picker over the usual overlays/hook and returns the
    /// dragged rectangle, or null on cancel / while another session is busy.
    /// </summary>
    public async Task<RectPx?> PickRegionAsync()
    {
        if (IsBusy) return null;
        _sm.TryTransition(AppStateMachine.ForMode(PickMode.Region));
        using var cts = new CancellationTokenSource();
        var monitors = MonitorEnumerator.Refresh();
        using var overlays = new OverlayHost(monitors, OverlayStyle.From(Prefs));
        using var hook = new InputHook();
        var ctx = new PickerContext { Overlays = overlays, Hook = hook, Prefs = Prefs, Uia = _uia, DelaySeconds = 0, FullTarget = FullScreenTarget.Current };
        var dog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        dog.Tick += (_, _) =>
        {
            if (Environment.TickCount64 - hook.LastInputTick > 60_000) { Log.Warn("Watchdog: no input for 60 s, cancelling region pick"); cts.Cancel(); }
        };
        try
        {
            overlays.Show();
            hook.Start();
            dog.Start();
            var outcome = await new RegionPicker(ctx).RunAsync(cts.Token);
            return outcome.Kind == PickKind.Picked ? outcome.Result?.Rect : null;
        }
        catch (Exception ex)
        {
            Log.Error("Region pick failed", ex);
            return null;
        }
        finally
        {
            dog.Stop();
            _sm.TryTransition(AppState.Idle);
        }
    }

    // ---- instant full screen (hotkey 2, §3D.A) ----------------------------------------------------

    public async Task RunInstantFullScreenAsync(FullScreenTarget? overrideTarget = null, int? overrideDelay = null)
    {
        if (IsBusy) return;
        var monitors = MonitorEnumerator.Current;
        var (cx, cy) = MonitorEnumerator.CursorPos();
        var target = overrideTarget ?? FullScreenTarget.Parse(Prefs.FullScreenTarget);
        // "Current monitor" is fixed now, at hotkey time, not after the countdown.
        if (target.Kind == FullScreenKind.CurrentMonitor && VirtualDesktop.MonitorAt(monitors, cx, cy) is { } cur)
            target = FullScreenTarget.Monitor(cur.Index);

        await RunInstantAsync(ms => VirtualDesktop.ResolveTarget(target, ms, 0, 0)
                                    ?? (ms.FirstOrDefault(m => m.IsPrimary) ?? ms[0]).Bounds,
                              overrideDelay ?? Prefs.DefaultDelaySeconds, "Instant full-screen capture");
    }

    /// <summary>Capture monitor N (1-based) straight to clipboard + file; refuses if it is not connected.</summary>
    public async Task RunMonitorAsync(int n)
    {
        if (IsBusy) return;
        if (VirtualDesktop.ByIndex(MonitorEnumerator.Refresh(), n) is null)
        {
            _notify("SharpSnip", $"Monitor {n} is not connected.", null);
            return;
        }
        await RunInstantAsync(ms => VirtualDesktop.ByIndex(ms, n)?.Bounds, Prefs.DefaultDelaySeconds, $"Monitor {n} capture");
    }

    /// <summary>Capture the foreground window (frame bounds) straight to clipboard + file.</summary>
    public async Task RunActiveWindowAsync()
    {
        if (IsBusy) return;
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (hwnd == IntPtr.Zero || pid == (uint)Environment.ProcessId)
        {
            _notify("SharpSnip", "No active window to capture.", null);
            return;
        }
        // Re-read the frame after any countdown: the window may have moved or opened a menu.
        await RunInstantAsync(_ => WindowEnumerator.FrameOf(hwnd) is { IsEmpty: false } f ? f : null,
                              Prefs.DefaultDelaySeconds, "Active-window capture");
    }

    /// <summary>Applies the freehand mask: pixels outside the drawn outline become transparent.</summary>
    private static CaptureResult MaskOutside(CaptureResult cap, IReadOnlyList<(int X, int Y)> polygon)
    {
        var src = cap.Image.Format == System.Windows.Media.PixelFormats.Bgra32
            ? cap.Image
            : new System.Windows.Media.Imaging.FormatConvertedBitmap(cap.Image, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        src.CopyPixels(px, stride, 0);
        PolygonMask.ApplyAlpha(px, w, h, polygon, cap.Rect);
        var bmp = System.Windows.Media.Imaging.BitmapSource.Create(w, h, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, px, stride);
        bmp.Freeze();
        return cap with { Image = bmp };
    }

    /// <summary>Capture the preset region (Settings) in one shot.</summary>
    public async Task RunPresetAsync()
    {
        if (IsBusy) return;
        if (Prefs.PresetRect is not { Length: 4 } r)
        {
            _notify("SharpSnip", "No preset region yet. Set one in Settings — it can take your last capture region.", null);
            return;
        }
        var rect = new RectPx(r[0], r[1], r[2], r[3]);
        await RunInstantAsync(_ => rect, Prefs.DefaultDelaySeconds, "Preset-region capture");
    }

    /// <summary>Capture the same screen rectangle as the previous capture.</summary>
    public async Task RunRepeatLastAsync()
    {
        if (IsBusy) return;
        if (LastRect is not { } last)
        {
            _notify("SharpSnip", "Nothing to repeat yet. Take a capture first.", null);
            return;
        }
        await RunInstantAsync(_ => last, Prefs.DefaultDelaySeconds, "Repeat-last capture");
    }

    /// <summary>Shared one-shot pipeline: countdown → resolve → capture → clipboard → file → toast.</summary>
    private async Task RunInstantAsync(Func<IReadOnlyList<MonitorInfo>, RectPx?> resolve, int delay, string label)
    {
        _sm.TryTransition(AppState.FullScreenInstant);
        try
        {
            var monitors = MonitorEnumerator.Current;
            if (delay > 0)
            {
                var (cx, cy) = MonitorEnumerator.CursorPos();
                using var hook = new InputHook();
                hook.Start();
                var where = VirtualDesktop.MonitorAt(monitors, cx, cy) ?? monitors[0];
                if (!await DelayTimer.RunAsync(delay, hook, where, CancellationToken.None)) return;
            }

            monitors = MonitorEnumerator.Refresh();
            if (resolve(monitors) is not { } raw)
            {
                _notify("SharpSnip", "The capture target is gone.", null);
                return;
            }
            RectPx rect = raw.Intersect(VirtualDesktop.Union(monitors));
            if (rect.IsEmpty) { _notify("SharpSnip", "The capture target is off screen.", null); return; }

            var cap = CaptureEngine.Capture(rect, monitors, Prefs.MatteArgb);
            RememberRect(rect);
            ClipboardWriter.SetImage(cap.Image);       // clipboard first: the latency target is hotkey → clipboard

            _sm.TryTransition(AppState.Exporting);
            string path = Exporter.Save(cap.Image, Prefs);
            _notify("Copied to clipboard", "Saved " + path, path);
            WorkflowRunner.MaybeRun(Prefs, "Capture", path, _notify);
        }
        catch (Exception ex)
        {
            Log.Error(label + " failed", ex);
            _notify("SharpSnip", "Capture failed: " + ex.Message, null);
        }
        finally { _sm.ForceIdle(); }
    }
}
