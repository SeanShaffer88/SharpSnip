using System.Security.Cryptography;
using System.Text;

namespace Snipit.Core;

/// <summary>
/// Tamper check for the settings that make Snipit launch other programs: the external editor,
/// the FFmpeg override and every workflow definition. The app stores a keyed hash (HMAC-SHA256) of
/// them each time the user saves in the app. The key is per user and per machine (the app keeps it
/// in HKCU), so a settings file that arrives some other way (a planted portable-mode config, a
/// copy from another PC, an edit with Notepad) cannot carry a valid hash. Program-launching steps
/// then stay blocked until the user saves Settings once.
/// A blank hash is trusted only when there is nothing to launch: blank plus an editor, an FFmpeg
/// override or a run/editor step is treated as changed.
/// </summary>
public static class WorkflowTrust
{
    public const int KeyBytes = 32;

    public static string Compute(Preferences p, byte[] key)
    {
        if (key is null || key.Length == 0) throw new ArgumentException("A trust key is required.", nameof(key));
        var sb = new StringBuilder();
        sb.Append("editor=").Append(p.ExternalEditorPath ?? "").Append('\n');
        sb.Append("ffmpeg=").Append(p.FfmpegPath ?? "").Append('\n');
        sb.Append("capture=").Append(p.CaptureWorkflow ?? "").Append('\n');
        sb.Append("recording=").Append(p.RecordingWorkflow ?? "").Append('\n');
        foreach (var w in p.Workflows ?? new())
        {
            sb.Append("wf=").Append(w.Name).Append('|').Append(w.ApplyTo).Append('\n');
            foreach (string step in w.Steps ?? new()) sb.Append(" step=").Append(step).Append('\n');
        }
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>True when the settings can start another program: an editor, an FFmpeg override, or a run/editor step.</summary>
    public static bool HasLaunchSettings(Preferences p) =>
        !string.IsNullOrWhiteSpace(p.ExternalEditorPath) || !string.IsNullOrWhiteSpace(p.FfmpegPath) ||
        (p.Workflows ?? new()).Any(w => (w.Steps ?? new()).Any(s => WorkflowPlan.TryParseStep(s, out var st) && Launches(st.Kind)));

    /// <summary>
    /// True when the launch-relevant settings no longer match the recorded hash, or when there is no
    /// recorded hash yet but there is something to launch.
    /// </summary>
    public static bool IsTampered(Preferences p, byte[] key)
    {
        if (string.IsNullOrEmpty(p.WorkflowTrust)) return HasLaunchSettings(p);
        return !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(p.WorkflowTrust), Encoding.ASCII.GetBytes(Compute(p, key)));
    }

    public static void Approve(Preferences p, byte[] key) => p.WorkflowTrust = Compute(p, key);

    /// <summary>Step kinds that start another program.</summary>
    public static bool Launches(string stepKind) => stepKind is "run" or "editor";
}
