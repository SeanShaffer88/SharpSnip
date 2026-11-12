using System.Windows;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

public static class Program
{
    private const string MutexName = @"Local\7C1D5E2A-3B84-4F0A-9E61-2A5B8D0C4F13-SharpSnip";
    private const string Usage =
@"SharpSnip " + Version + @"
  SharpSnip.exe                         start in the tray
  SharpSnip.exe --fullscreen [options]  one-shot capture, then exit
      --all            capture every monitor (default: monitor under the cursor)
      --delay N        wait N seconds first (0, 3, 5 or 10)
      --out FILE       save to FILE (.png or .jpg); otherwise the output folder is used
      --no-clipboard   do not touch the clipboard
  SharpSnip.exe --clean                 delete settings and logs, then exit
  SharpSnip.exe --verbose               start with debug logging
  SharpSnip.exe --version | --help";

    private const string Version = "0.2.0";

    [STAThread]
    public static int Main(string[] args)
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiPerMonitorV2); // no-op when the manifest already did it
        bool Has(string flag) => args.Contains(flag, StringComparer.OrdinalIgnoreCase);
        if (Has("--verbose")) Log.Verbose = true;

        if (Has("--help") || Has("-h") || Has("/?")) return Print(Usage, 0);
        if (Has("--version")) return Print("SharpSnip " + Version, 0);
        if (Has("--clean")) return Clean();
        if (Has("--fullscreen")) return OneShot(args);

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first) return 0; // already running in the tray

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error("Unhandled UI exception", e.Exception);
            e.Handled = true; // never show a crash dialog; the session cleans itself up in finally blocks
        };
        using var host = new AppHost();
        return app.Run();
    }

    private static int OneShot(string[] args)
    {
        string? Value(string flag)
        {
            int i = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        bool all = args.Contains("--all", StringComparer.OrdinalIgnoreCase);
        bool noClip = args.Contains("--no-clipboard", StringComparer.OrdinalIgnoreCase);
        int delay = 0;
        if (Value("--delay") is { } d && (!int.TryParse(d, out delay) || DelayCycle.Sanitize(delay) != delay))
            return Print("--delay must be 0, 3, 5 or 10.", 2);

        try
        {
            var prefs = PrefsStore.Load(AppPaths.PrefsPath, out _);
            var monitors = MonitorEnumerator.Refresh();
            var (cx, cy) = MonitorEnumerator.CursorPos();
            var target = all ? FullScreenTarget.All : FullScreenTarget.Current;
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

    private static int Clean()
    {
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
