using System.Diagnostics;
using System.IO;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>Finds the FFmpeg to use: the Settings override first, then the bundled copy (ffmpeg\ffmpeg.exe beside SharpSnip.exe).</summary>
internal static class FfmpegLocator
{
    public static string? Find(Preferences prefs)
    {
        // A settings file changed outside the app must not redirect FFmpeg to another program (WorkflowTrust).
        if (!prefs.WorkflowsLocked && !string.IsNullOrWhiteSpace(prefs.FfmpegPath) && File.Exists(prefs.FfmpegPath.Trim()))
            return prefs.FfmpegPath.Trim();
        string bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        return File.Exists(bundled) ? bundled : null;
    }
}

/// <summary>Runs FFmpeg headlessly: argument list (never a shell string), stderr captured for its tail.</summary>
internal static class FfmpegExport
{
    /// <summary>Runs one export. Returns the exit code and the last lines of FFmpeg's log (its errors live there).</summary>
    public static async Task<(int ExitCode, string LogTail)> RunAsync(string ffmpegExe, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(ffmpegExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);
        using var proc = new Process { StartInfo = psi };
        var stderr = new List<string>();
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (stderr) stderr.Add(e.Data); } };
        proc.Start();
        proc.BeginErrorReadLine();
        _ = proc.StandardOutput.ReadToEndAsync(ct); // drained so the pipe never blocks
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); }
            throw;
        }
        string tail;
        lock (stderr) tail = string.Join("\n", stderr.TakeLast(8));
        Log.Info($"ffmpeg exited {proc.ExitCode} for: {string.Join(' ', args.Take(4))}...");
        return (proc.ExitCode, tail);
    }
}
