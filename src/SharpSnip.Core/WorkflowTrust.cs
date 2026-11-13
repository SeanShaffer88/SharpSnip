using System.Security.Cryptography;
using System.Text;

namespace SharpSnip.Core;

/// <summary>
/// Tamper check for the settings that make SharpSnip launch other programs: the external editor,
/// the FFmpeg override and every workflow definition. The app stores a hash of them each time the
/// user saves in the app; if the file is changed any other way (a planted portable-mode config, an
/// edit with Notepad) the hash no longer matches and program-launching steps stay blocked until the
/// user saves Settings once. A blank hash (first run, upgrade) is accepted and recorded.
/// </summary>
public static class WorkflowTrust
{
    public static string Compute(Preferences p)
    {
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
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>True when a trust hash exists and the current settings no longer match it.</summary>
    public static bool IsTampered(Preferences p) =>
        !string.IsNullOrEmpty(p.WorkflowTrust) && !string.Equals(p.WorkflowTrust, Compute(p), StringComparison.Ordinal);

    public static void Approve(Preferences p) => p.WorkflowTrust = Compute(p);

    /// <summary>Step kinds that start another program.</summary>
    public static bool Launches(string stepKind) => stepKind is "run" or "editor";
}
