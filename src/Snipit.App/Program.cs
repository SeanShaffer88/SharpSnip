using System.Windows;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

public static class Program
{
    // Share the instance guard and show event with pre-rename builds so only one app owns Print Screen.
    private const string MutexName = @"Local\7C1D5E2A-3B84-4F0A-9E61-2A5B8D0C4F13-SharpSnip";
    /// <summary>A second launch sets this event; the running instance answers by restoring its tray icon and opening Settings.</summary>
    internal const string ShowEventName = @"Local\7C1D5E2A-3B84-4F0A-9E61-2A5B8D0C4F13-SharpSnipShow";
    private const string Usage =
@"Snipit " + Version + @"
  Snipit.exe                         start in the tray
  Snipit.exe --fullscreen [options]  one-shot capture, then exit
      --all            capture every monitor (default: monitor under the cursor)
      --monitor N      capture monitor N (1-9)
      --delay N        wait N seconds first (0, 3, 5 or 10)
      --out FILE       save to FILE (.png, .jpg, .bmp, .gif, .tif or .pdf); otherwise the output folder is used
      --no-clipboard   do not touch the clipboard
  Snipit.exe --interval N --count M [--all | --monitor K] [--out DIR]
                                        M frames, one every N seconds, into DIR (default: new Interval-... folder)
                                        In GIF format the frames become one looping animation.gif instead
  Snipit.exe --record N [--monitor K] [--fps F] [--quality Q] [--size S] [--region X,Y,W,H] [--mic] [--no-audio] [--webcam] [--webcam-corner C] [--gif] [--max-mb M] [--at HH:MM] [--out FILE]
                                        record monitor K (default: primary) for N seconds to an MP4
                                        --region records only that rectangle of the monitor (offsets in pixels)
                                        system sound is recorded by default; --mic adds the microphone, --no-audio mutes both
                                        --webcam overlays the default camera in a corner (default BottomRight)
                                        --gif records an animated GIF instead (silent, max 20 fps)
                                        --max-mb stops early at that file size; --at waits until the given time to start
                                        (default: a timestamped recording-....mp4 in the output folder)
  Snipit.exe --run-workflow NAME --file PATH
                                        run a named workflow (Settings) on an existing file
  Snipit.exe --export PRESET --file PATH [--out PATH]
                                        convert a recording/GIF with the bundled FFmpeg
                                        (presets: webm, av1, h264, mkv, gifhq, mp3, m4a)
  Snipit.exe --restore-printscreen   hand the Print Screen key back to Windows (after a crash or before uninstall)
  Snipit.exe --clean                 delete settings and logs, then exit
  Snipit.exe --verbose               start with debug logging
  Snipit.exe --version | --help";

    private const string Version = "0.26.0";

    [STAThread]
    public static int Main(string[] args)
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiPerMonitorV2); // no-op when the manifest already did it
        bool Has(string flag) => CliArgs.Has(args, flag);
        if (Has("--verbose")) Log.Verbose = true;

        if (Has("--help") || Has("-h") || Has("/?")) return Print(Usage, 0);
        if (Has("--version")) return Print("Snipit " + Version, 0);
        if (Has("--clean")) return Clean();
        if (Has("--restore-printscreen"))
            return Print(PrintScreenKey.RestoreFromFile()
                ? "Print Screen handed back to Windows."
                : "Nothing to restore (Snipit has no override recorded).", 0);
        if (Has("--record")) return RecordCli(args);
        if (Has("--run-workflow")) return RunWorkflowCli(args);
        if (Has("--export")) return ExportCli(args);
        if (Has("--interval")) return IntervalCli(args);
        if (Has("--fullscreen")) return OneShot(args);

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            // Already running: ask it to show itself (its tray icon may be hidden) and open Settings.
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var ev)) { ev.Set(); ev.Dispose(); }
            return 0;
        }

        // Safety nets: the Print Screen override must never outlive the process. A clean exit restores it in
        // AppHost.Dispose; these cover crashes, logoff and termination paths that skip it. All are idempotent.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => PrintScreenKey.RestoreFromFile();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Error("Unhandled exception (process is ending)", e.ExceptionObject as Exception);
            PrintScreenKey.RestoreFromFile();
        };
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("Unobserved task exception", e.Exception); e.SetObserved(); };

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("Unhandled UI exception", e.Exception);
            e.Handled = true; // never show a crash dialog; the session cleans itself up in finally blocks
        };
        using var host = new AppHost();
        return app.Run();
    }

    private static int ExportCli(string[] args)
    {
        string? Value(string flag) => CliArgs.Value(args, flag);
        string? presetName = Value("--export");
        string? file = Value("--file");
        if (presetName is null || file is null) return Print("Usage: Snipit.exe --export PRESET --file PATH [--out PATH]", 2);
        if (!File.Exists(file)) return Print("No such file: " + file, 2);
        var preset = FfmpegPlan.Find(presetName);
        if (preset is null) return Print("--export must be one of: " + string.Join(", ", FfmpegPlan.Presets.Select(p => p.Name)) + ".", 2);
        var prefs = LoadPrefsChecked();
        string? ffmpeg = FfmpegLocator.Find(prefs);
        if (ffmpeg is null) return Print("FFmpeg not found (no bundled copy, no override in Settings).", 1);
        string output = Value("--out") ?? OutputNaming.UniquePath(FfmpegPlan.DefaultOutputPath(preset, Path.GetFullPath(file)), File.Exists);
        if (!FfmpegPlan.TryBuildArgs(preset.Name, Path.GetFullPath(file), output, out var ffArgs)) return Print("Unknown preset.", 2);
        var (exit, tail) = FfmpegExport.RunAsync(ffmpeg, ffArgs).GetAwaiter().GetResult();
        if (exit == 0 && File.Exists(output)) return Print(output, 0);
        return Print($"FFmpeg failed (exit {exit}). Last log lines:\n{tail}", 1);
    }

    private static int RunWorkflowCli(string[] args)
    {
        string? Value(string flag) => CliArgs.Value(args, flag);
        string? name = Value("--run-workflow");
        string? file = Value("--file");
        if (name is null || file is null) return Print("Usage: Snipit.exe --run-workflow NAME --file PATH", 2);
        if (!File.Exists(file)) return Print("No such file: " + file, 2);
        var prefs = LoadPrefsChecked();
        var def = WorkflowPlan.Find(prefs.Workflows, name);
        if (def is null) return Print("No workflow named '" + name + "'.", 2);
        var (done, error, working) = WorkflowRunner.Run(def, Path.GetFullPath(file), prefs);
        if (error is not null) return Print($"Workflow '{def.Name}' stopped after {done} step(s): {error}", 3);
        return Print($"Workflow '{def.Name}' finished: {done} step(s). Result: {working}", 0);
    }

    private static int OneShot(string[] args)
    {
        string? Value(string flag) => CliArgs.Value(args, flag);
        bool all = CliArgs.Has(args, "--all");
        int monitorArg = 0;
        if (Value("--monitor") is { } mv && (!int.TryParse(mv, out monitorArg) || monitorArg is < 1 or > 9))
            return Print("--monitor must be 1 to 9.", 2);
        bool noClip = CliArgs.Has(args, "--no-clipboard");
        int delay = 0;
        if (Value("--delay") is { } d && (!int.TryParse(d, out delay) || DelayCycle.Sanitize(delay) != delay))
            return Print("--delay must be 0, 3, 5 or 10.", 2);

        try
        {
            var prefs = PrefsStore.Load(AppPaths.PrefsPath, out _);
            var monitors = MonitorEnumerator.Refresh();
            var (cx, cy) = MonitorEnumerator.CursorPos();
            var target = all ? FullScreenTarget.All : monitorArg > 0 ? FullScreenTarget.Monitor(monitorArg) : FullScreenTarget.Current;
            if (delay > 0) { Console.Error.WriteLine($"Capturing in {delay} s…"); Thread.Sleep(delay * 1000); } // CLI has no UI to cancel; Ctrl+C ends it
            monitors = MonitorEnumerator.Refresh();
            RectPx rect = VirtualDesktop.ResolveTarget(target, monitors, cx, cy) ?? monitors[0].Bounds;
            var cap = CaptureEngine.Capture(rect, monitors, prefs.MatteArgb);
            if (!noClip) ClipboardWriter.SetImage(cap.Image);
            string path = Exporter.Save(cap.Image, prefs, Value("--out"));
            return Print(path, 0);
        }
        catch (Exception ex)
        {
            Log.Error("One-shot capture failed", ex);
            return Print("Capture failed: " + ex.Message, 1);
        }
    }

    private static int RecordCli(string[] args)
    {
        string? Value(string flag) => CliArgs.Value(args, flag);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            return Print("Recording needs Windows 10 version 2004 (build 19041) or later.", 2);
        if (!int.TryParse(Value("--record"), out int secs) || secs is < 1 or > RecordingPlan.MaxSeconds)
            return Print($"--record must be 1 to {RecordingPlan.MaxSeconds} seconds.", 2);
        int mon = 0;
        if (Value("--monitor") is { } mv && (!int.TryParse(mv, out mon) || mon is < 1 or > 9)) return Print("--monitor must be 1 to 9.", 2);
        int fps = RecordingPlan.DefaultFps;
        if (Value("--fps") is { } fv && (!int.TryParse(fv, out fps) || fps is < RecordingPlan.MinFps or > RecordingPlan.MaxFps))
            return Print($"--fps must be {RecordingPlan.MinFps} to {RecordingPlan.MaxFps}.", 2);
        string quality = RecordingQuality.Default;
        if (Value("--quality") is { } qv)
        {
            if (Array.IndexOf(RecordingQuality.All, qv) < 0)
                return Print("--quality must be Low, Medium, High or Ultra.", 2);
            quality = qv;
        }
        long maxBytes = 0;
        if (Value("--max-mb") is { } mbv)
        {
            if (!int.TryParse(mbv, out int mb) || mb is < 0 or > 102400)
                return Print("--max-mb must be 0 to 102400.", 2);
            maxBytes = mb * 1_000_000L;
        }
        if (Value("--at") is { } atv)
        {
            if (!TimeSpan.TryParse(atv, out var at) || at < TimeSpan.Zero || at >= TimeSpan.FromDays(1))
                return Print("--at must be HH:MM (24-hour).", 2);
            var now = DateTime.Now;
            var start = now.Date + at;
            if (start <= now.AddSeconds(5)) start = start.AddDays(1);
            Console.Error.WriteLine($"Waiting until {start:yyyy-MM-dd HH:mm} to record...");
            while (DateTime.Now < start) Thread.Sleep(1000);
        }
        RectPx? crop = null;
        if (Value("--region") is { } rv)
        {
            var parts = rv.Split(',');
            if (parts.Length != 4 || !int.TryParse(parts[0], out int rx) || !int.TryParse(parts[1], out int ry)
                || !int.TryParse(parts[2], out int rw) || !int.TryParse(parts[3], out int rh) || rw < 16 || rh < 16)
                return Print("--region must be X,Y,W,H (offsets inside the monitor, at least 16x16).", 2);
            crop = new RectPx(rx, ry, rw, rh);
        }
        try
        {
            var prefs = PrefsStore.Load(AppPaths.PrefsPath, out _);
            var monitors = MonitorEnumerator.Refresh();
            var monitor = mon > 0 ? VirtualDesktop.ByIndex(monitors, mon)
                : monitors.FirstOrDefault(m => m.IsPrimary) ?? (monitors.Count > 0 ? monitors[0] : null);
            if (monitor is null) return Print(mon > 0 ? $"Monitor {mon} not found." : "No monitors found.", 2);
            bool gif = CliArgs.Has(args, "--gif");
            string path = Value("--out") ?? Path.Combine(Exporter.OutputFolder(prefs),
                gif ? RecordingPlan.GifFileName(DateTime.Now) : RecordingPlan.FileName(DateTime.Now));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var audio = new AudioOptions(System: !CliArgs.Has(args, "--no-audio"),
                                         Mic: CliArgs.Has(args, "--mic"));
            string corner = "BottomRight";
            if (Value("--webcam-corner") is { } cv)
            {
                if (!Enum.TryParse<PipCorner>(cv, ignoreCase: true, out _))
                    return Print("--webcam-corner must be BottomRight, BottomLeft, TopRight or TopLeft.", 2);
                corner = cv;
            }
            var webcam = new WebcamOptions(CliArgs.Has(args, "--webcam"), corner);
            string sizeName = RecordingSize.Default;
            if (Value("--size") is { } sv)
            {
                if (Array.IndexOf(RecordingSize.All, sv) < 0)
                    return Print("--size must be Original, 1080p, 720p or 480p.", 2);
                sizeName = sv;
            }
            IRecording? rec = gif
                ? GifRecorder.StartAsync(monitor, path, fps, crop, webcam, 0, maxBytes, sizeName).GetAwaiter().GetResult()
                : ScreenRecorder.StartAsync(monitor, path, fps, crop, audio, quality, webcam, 0, maxBytes, sizeName).GetAwaiter().GetResult();
            if (rec is null) return Print("Recording could not start (capture or encoder unavailable).", 1);
            using (rec)
            {
                Console.Error.WriteLine($"Recording monitor {monitor.Index} for {secs} s... Ctrl+C ends the process early.");
                Thread.Sleep(secs * 1000);
                rec.StopAsync().GetAwaiter().GetResult();
            }
            return Print(path, 0);
        }
        catch (Exception ex)
        {
            Log.Error("Record CLI failed", ex);
            return Print("Recording failed: " + ex.Message, 1);
        }
    }

    private static int IntervalCli(string[] args)
    {
        string? Value(string flag) => CliArgs.Value(args, flag);
        if (!int.TryParse(Value("--interval"), out int secs) || secs is < IntervalPlan.MinSeconds or > IntervalPlan.MaxSeconds)
            return Print($"--interval must be {IntervalPlan.MinSeconds} to {IntervalPlan.MaxSeconds} seconds.", 2);
        if (!int.TryParse(Value("--count"), out int count) || count is < 1 or > IntervalPlan.MaxShots)
            return Print($"--count is required and must be 1 to {IntervalPlan.MaxShots}.", 2);
        int mon = 0;
        if (Value("--monitor") is { } mv && (!int.TryParse(mv, out mon) || mon is < 1 or > 9)) return Print("--monitor must be 1 to 9.", 2);
        try
        {
            var prefs = PrefsStore.Load(AppPaths.PrefsPath, out _);
            var requested = CliArgs.Has(args, "--all") ? FullScreenTarget.All : mon > 0 ? FullScreenTarget.Monitor(mon) : FullScreenTarget.Current;
            var target = IntervalCapture.ResolveStartTarget(requested);
            string folder = Value("--out") ?? Path.Combine(Exporter.OutputFolder(prefs), IntervalPlan.FolderName(DateTime.Now));
            if (AnimationPlan.Kind(prefs.ImageFormat) == "gif")
            {
                // GIF format = one animated file for the whole run, frame delay = the interval.
                using var gif = new AnimatedGifSink(folder);
                for (int i = 1; i <= count; i++)
                {
                    var cap = IntervalCapture.CaptureBitmap(prefs, target);
                    if (!gif.Add(cap, GifPlan.DelayHundredths(secs)))
                        return Print("Interval capture failed: frame size changed during the run (display layout changed).", 1);
                    if (i < count) Thread.Sleep(secs * 1000);
                }
                gif.Dispose();
                return Print(gif.FinalPath, 0);
            }
            if (AnimationPlan.Kind(prefs.ImageFormat) == "apng")
            {
                // APNG format = one lossless animated file for the whole run.
                using var apng = new AnimatedApngSink(folder);
                for (int i = 1; i <= count; i++)
                {
                    var cap = IntervalCapture.CaptureBitmap(prefs, target);
                    if (!apng.Add(cap, GifPlan.DelayHundredths(secs)))
                        return Print("Interval capture failed: frame size changed during the run (display layout changed).", 1);
                    if (i < count) Thread.Sleep(secs * 1000);
                }
                apng.Dispose();
                return Print(apng.FinalPath, 0);
            }
            for (int i = 1; i <= count; i++)
            {
                Console.Out.WriteLine(IntervalCapture.CaptureOnce(prefs, target, folder, i));
                if (i < count) Thread.Sleep(secs * 1000); // the interval itself is the feature; Ctrl+C ends the run
            }
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Interval CLI failed", ex);
            return Print("Interval capture failed: " + ex.Message, 1);
        }
    }

    /// <summary>Loads settings for a CLI run; flags the lock when the launch-relevant settings changed outside the app.</summary>
    private static Preferences LoadPrefsChecked()
    {
        var prefs = PrefsStore.Load(AppPaths.PrefsPath, out _);
        if (WorkflowTrust.IsTampered(prefs)) { prefs.WorkflowsLocked = true; Log.Warn("Workflow settings changed outside Snipit; program-launching steps are blocked."); }
        return prefs;
    }

    private static int Clean()
    {
        // Leave no trace: if a run recorded the pre-override Print Screen state
        // (a graceful exit restores it itself; this covers a leftover record),
        // give the key back to Windows first.
        try
        {
            var prefs = PrefsStore.Load(AppPaths.PrefsPath, out _);
            if (prefs.PrintScreenPriorRecorded) PrintScreenKey.Write(PrintScreenKey.Prior(prefs.PrintScreenSnippingPrior));
        }
        catch (Exception ex) { Log.Error("Print Screen restore during --clean failed", ex); }
        try
        {
            if (Directory.Exists(AppPaths.DataDir)) Directory.Delete(AppPaths.DataDir, true);
            return Print("Removed " + AppPaths.DataDir, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Print("Could not remove " + AppPaths.DataDir + ": " + ex.Message, 1);
        }
    }

    /// <summary>A WinExe has no console; borrow the parent's so CLI output is visible.</summary>
    private static int Print(string text, int code)
    {
        NativeMethods.AttachConsole(-1);
        (code == 0 ? Console.Out : Console.Error).WriteLine(text);
        return code;
    }
}
