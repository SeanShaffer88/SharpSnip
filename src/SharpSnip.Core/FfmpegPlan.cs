namespace SharpSnip.Core;

/// <summary>
/// One named FFmpeg export: the arguments are a token list (never a shell
/// string) in which "{in}" and "{out}" stand for the input and output paths.
/// </summary>
public sealed record FfmpegPreset(string Name, string Label, string OutputExtension, string[] Args);

/// <summary>
/// The FFmpeg export matrix. SharpSnip bundles an LGPL build of FFmpeg
/// (owner-approved dependency, 2026-11-13), and the preset list is limited
/// to encoders that build actually contains: libx264/libx265 are GPL-only
/// and absent, so H.264 goes through Windows' own MediaFoundation encoder
/// (h264_mf) and the modern formats ride libvpx/libsvtav1. A user-supplied
/// ffmpeg at the Settings override may know more — the Custom template
/// (with %in%/%out% placeholders) covers anything the presets don't.
/// </summary>
public static class FfmpegPlan
{
    public static readonly IReadOnlyList<FfmpegPreset> Presets = new FfmpegPreset[]
    {
        new("webm", "WebM (VP9 + Opus)", "webm",
            new[] { "-i", "{in}", "-c:v", "libvpx-vp9", "-b:v", "0", "-crf", "32", "-c:a", "libopus", "-y", "{out}" }),
        new("av1", "AV1 in MP4 (SVT-AV1 + AAC)", "mp4",
            new[] { "-i", "{in}", "-c:v", "libsvtav1", "-crf", "35", "-c:a", "aac", "-b:a", "128k", "-y", "{out}" }),
        new("h264", "MP4 (H.264 via Windows encoder + AAC)", "mp4",
            new[] { "-i", "{in}", "-c:v", "h264_mf", "-c:a", "aac", "-b:a", "128k", "-y", "{out}" }),
        new("mkv", "MKV (lossless remux, no re-encode)", "mkv",
            new[] { "-i", "{in}", "-c", "copy", "-y", "{out}" }),
        new("gifhq", "GIF (palette, 15 fps, 640 wide)", "gif",
            new[] { "-i", "{in}", "-vf", "fps=15,scale=640:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse", "-y", "{out}" }),
        new("mp3", "Audio only (MP3)", "mp3",
            new[] { "-i", "{in}", "-vn", "-c:a", "libmp3lame", "-q:a", "2", "-y", "{out}" }),
        new("m4a", "Audio only (AAC)", "m4a",
            new[] { "-i", "{in}", "-vn", "-c:a", "aac", "-b:a", "160k", "-y", "{out}" }),
    };

    public static FfmpegPreset? Find(string? name)
    {
        if (name is null) return null;
        foreach (var p in Presets)
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }

    /// <summary>Builds the argument list for a preset; false when the preset is unknown.</summary>
    public static bool TryBuildArgs(string? presetName, string inputPath, string outputPath, out string[] args)
    {
        args = Array.Empty<string>();
        var preset = Find(presetName);
        if (preset is null) return false;
        args = preset.Args.Select(a => a == "{in}" ? inputPath : a == "{out}" ? outputPath : a).ToArray();
        return true;
    }

    /// <summary>The default export path for a preset: the input's folder and name, with the preset's extension.</summary>
    public static string DefaultOutputPath(FfmpegPreset preset, string inputPath) =>
        Path.ChangeExtension(inputPath, "." + preset.OutputExtension);

    /// <summary>
    /// Splits a custom argument template into tokens. Whitespace separates;
    /// double quotes group (and are removed); %in% and %out% expand to the
    /// paths, quoted or not. An unterminated quote just runs to the end.
    /// </summary>
    public static string[] ParseCustomArgs(string? template, string inputPath, string outputPath)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(template)) return tokens.ToArray();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false, hasToken = false;
        foreach (char ch in template)
        {
            if (ch == '"') { inQuotes = !inQuotes; hasToken = true; }
            else if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (hasToken || current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); hasToken = false; }
            }
            else current.Append(ch);
        }
        if (hasToken || current.Length > 0) tokens.Add(current.ToString());
        return tokens.Select(t => t
            .Replace("%in%", inputPath, StringComparison.OrdinalIgnoreCase)
            .Replace("%out%", outputPath, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
