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
        var ctx = new PickerContext { Overlays = overlays, Hook = hook, Prefs = Prefs, Uia = _uia, DelaySeconds = Prefs.DefaultDelaySeconds, FullTarget = FullScreenTarget.Parse(Prefs.FullScreenTarget), ScrollCapture = scroll };

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
                    _ => new MenuPicker(ctx),
                };
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
        }
        RememberRect(rect);
        ClipboardWriter.SetImage(cap.Image);
        if (cap.Megapixels > 100) _notify("SharpSnip", $"Large capture ({cap.Megapixels:F0} MP). Saving may be slow.", null);

        // Tear down input before the editor opens so no hook outlives the capture.
        hook.Dispose();
        overlays.Dispose();

        if (Prefs.OpenEditorAfterCapture)
        {
            _sm.TryTransition(AppState.Editing);
            var tcs = new TaskCompletionSource();
            var win = new EditorWindow(cap, result.Meta, monitors.Count, delay, Prefs, _persistPrefs, _notify);
            win.Closed += (_, _) => tcs.TrySetResult();
            win.Show();
            win.Activate();
            await tcs.Task;
        }
        else
        {
            _sm.TryTransition(AppState.Exporting);
            string path = Exporter.Save(cap.Image, Prefs);
            _notify("Copied to clipboard", "Saved " + path, path);
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
        }
        catch (Exception ex)
        {
            Log.Error(label + " failed", ex);
            _notify("SharpSnip", "Capture failed: " + ex.Message, null);
        }
        finally { _sm.ForceIdle(); }
    }
}
