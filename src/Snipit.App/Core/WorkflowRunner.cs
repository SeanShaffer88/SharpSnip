using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snipit.Core;

namespace Snipit;

/// <summary>
/// Executes a workflow (Core WorkflowPlan) on a finished capture or recording.
/// Steps run in order against a "working file": copy leaves it unchanged,
/// format and effect produce a sibling and move the working file to it, run
/// hands it to a program, editor opens it in the configured external editor.
/// The first failing step stops the chain — later steps may depend on the
/// working file — and the failure is reported, never swallowed.
/// </summary>
internal static class WorkflowRunner
{
    /// <summary>Fire-and-forget entry for the app: runs the workflow assigned to this product kind, then toasts the outcome.</summary>
    public static void MaybeRun(Preferences prefs, string productKind, string file, Action<string, string, string?> notify)
    {
        string assigned = productKind == "Capture" ? prefs.CaptureWorkflow : prefs.RecordingWorkflow;
        var def = WorkflowPlan.Find(prefs.Workflows, assigned);
        if (def is null || !WorkflowPlan.AppliesTo(def.ApplyTo, productKind)) return;
        var dispatcher = Application.Current?.Dispatcher;
        void Tell(string title, string message, string? path)
        {
            if (dispatcher is not null && !dispatcher.CheckAccess())
                _ = dispatcher.InvokeAsync(() => notify(title, message, path));
            else notify(title, message, path);
        }
        _ = Task.Run(() =>
        {
            var (done, error, working) = Run(def, file, prefs);
            if (error is not null) Tell("Workflow " + def.Name, $"Stopped after {done} step(s): {error}", file);
            else if (done > 0) Tell("Workflow " + def.Name, $"{done} step(s) done.", working);
        });
    }

    /// <summary>Runs the whole chain synchronously. Returns steps completed, the first error (null = success) and the final working file.</summary>
    public static (int Done, string? Error, string Working) Run(WorkflowDef def, string file, Preferences prefs)
    {
        string working = file;
        int done = 0;
        foreach (string line in def.Steps)
        {
            if (!WorkflowPlan.TryParseStep(line, out var step)) return (done, "Unreadable step: " + line, working);
            try
            {
                working = Execute(step, working, prefs);
                done++;
                Log.Info($"Workflow {def.Name}: {step.Kind} done -> {working}");
            }
            catch (Exception ex)
            {
                Log.Error($"Workflow {def.Name}: step {step.Kind} failed", ex);
                return (done, $"{step.Kind}: {ex.Message}", working);
            }
        }
        return (done, null, working);
    }

    private static string Execute(WorkflowStep step, string working, Preferences prefs)
    {
        if (prefs.WorkflowsLocked && WorkflowTrust.Launches(step.Kind))
            throw new InvalidOperationException("blocked: the workflow or editor settings were changed outside Snipit. Open Settings and press Save to approve them");
        switch (step.Kind)
        {
            case "copy":
            {
                string dir = WorkflowPlan.Expand(step.Arg, working);
                Directory.CreateDirectory(dir);
                string dest = OutputNaming.UniquePath(Path.Combine(dir, Path.GetFileName(working)), File.Exists);
                File.Copy(working, dest);
                return working;
            }
            case "format":
            {
                if (WorkflowPlan.IsFfmpegFormat(step) || WorkflowPlan.IsRecordingFile(working))
                {
                    var preset = FfmpegPlan.Find(step.Arg)
                        ?? throw new InvalidOperationException($"'{step.Arg}' is a still-image format; recordings convert with an FFmpeg preset (webm, av1, h264, mkv, gifhq, mp3, m4a)");
                    if (!WorkflowPlan.IsRecordingFile(working))
                        throw new InvalidOperationException("FFmpeg preset steps apply to recordings; this workflow is running on a still image");
                    string ffmpeg = FfmpegLocator.Find(prefs)
                        ?? throw new InvalidOperationException("FFmpeg not found (no bundled copy, no override in Settings)");
                    string output = OutputNaming.UniquePath(FfmpegPlan.DefaultOutputPath(preset, working), File.Exists);
                    if (!FfmpegPlan.TryBuildArgs(preset.Name, working, output, out var ffArgs))
                        throw new InvalidOperationException("unknown FFmpeg preset " + preset.Name);
                    var (exit, tail) = FfmpegExport.RunAsync(ffmpeg, ffArgs).GetAwaiter().GetResult();
                    if (exit != 0 || !File.Exists(output))
                        throw new InvalidOperationException($"FFmpeg failed (exit {exit}): {tail.Split('\n')[^1].Trim()}");
                    return output;
                }
                string sibling = OutputNaming.UniquePath(Path.ChangeExtension(working, "." + step.Arg), File.Exists);
                var img = LoadImage(working);
                Exporter.Save(img, prefs, sibling);
                return sibling;
            }
            case "effect":
            {
                RequireStill(working, "effect");
                var img = LoadImage(working);
                var src = img.Format == PixelFormats.Bgra32 ? img : new FormatConvertedBitmap(img, PixelFormats.Bgra32, null, 0);
                int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
                var px = new byte[stride * h];
                src.CopyPixels(px, stride, 0);
                if (step.Arg == "Grayscale") PixelAdjust.Grayscale(px);
                else PixelAdjust.Border(px, w, h, Math.Max(2, Math.Min(w, h) / 120), 0, 0, 0);
                var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
                bmp.Freeze();
                string sibling = OutputNaming.UniquePath(
                    Path.Combine(Path.GetDirectoryName(working)!, Path.GetFileNameWithoutExtension(working) + "-fx" + Path.GetExtension(working)),
                    File.Exists);
                Exporter.Save(bmp, prefs, sibling);
                return sibling;
            }
            case "editor":
            {
                if (string.IsNullOrWhiteSpace(prefs.ExternalEditorPath))
                    throw new InvalidOperationException("no external editor is set in Settings");
                using (Process.Start(new ProcessStartInfo(prefs.ExternalEditorPath.Trim()) { Arguments = WorkflowPlan.QuoteArg(working), UseShellExecute = true })) { }
                return working;
            }
            case "run":
            {
                var (program, args) = WorkflowPlan.SplitRun(step.Arg);
                // Arguments are quoted per value, so a capture path with spaces stays one argument. The
                // program keeps running on its own; only our handle to it is released.
                using (Process.Start(new ProcessStartInfo(WorkflowPlan.Expand(program, working))
                {
                    Arguments = WorkflowPlan.ExpandArgs(args, working),
                    UseShellExecute = false,
                })) { }
                return working;
            }
            default:
                throw new InvalidOperationException("unknown step kind " + step.Kind);
        }
    }

    private static void RequireStill(string file, string kind)
    {
        if (file.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{kind} steps apply to still images — this file is a recording");
    }

    private static BitmapSource LoadImage(string path)
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
        return img;
    }
}
