using System.Text.Json;

namespace SharpSnip.Core;

/// <summary>User settings. Stored as human-readable, versioned JSON; never tied to hardware or SID.</summary>
public sealed class Preferences
{
    public const int CurrentSchema = 1;

    public int SchemaVersion { get; set; } = CurrentSchema;
    public string OutputFolder { get; set; } = "";
    public string ImageFormat { get; set; } = "png";
    public int FixedWidth { get; set; } = 1280;
    public int FixedHeight { get; set; } = 720;
    public int IntervalSeconds { get; set; } = 5;
    public string FullScreenTarget { get; set; } = "CurrentMonitor";
    public int DefaultDelaySeconds { get; set; }
    public bool ShowToasts { get; set; } = true;
    public double DimmerOpacity { get; set; } = 0.47;
    public bool HighContrastHighlight { get; set; }
    public string MatteColor { get; set; } = "Transparent";
    /// <summary>Editor window x,y,width,height in physical pixels; null = never saved.</summary>
    public int[]? EditorBounds { get; set; }
    /// <summary>Last capture rectangle x,y,width,height in physical pixels, so "repeat last" survives a restart; null = none yet.</summary>
    public int[]? LastCaptureRect { get; set; }
    /// <summary>Recording region x,y,width,height in physical pixels, set by the Recorder's "Pick region…"; null = fall back to the last capture rectangle.</summary>
    public int[]? RecordingRegion { get; set; }
    public bool EditorMaximized { get; set; }
    /// <summary>Screen-recording frame rate (RecordingPlan bounds).</summary>
    public int RecordingFps { get; set; } = RecordingPlan.DefaultFps;
    /// <summary>Record the machine's own sound (WASAPI loopback) with recordings.</summary>
    public bool RecordSystemAudio { get; set; } = true;
    /// <summary>Record the default microphone with recordings (off by default — a mic is private by nature).</summary>
    public bool RecordMic { get; set; }
    /// <summary>Recording quality preset name (RecordingQuality).</summary>
    public string RecordingQualityName { get; set; } = RecordingQuality.Default;
    /// <summary>Recording output height cap: Original, 1080p, 720p or 480p.</summary>
    public string RecordingSizeName { get; set; } = RecordingSize.Default;
    /// <summary>Overlay the webcam on recordings, picture-in-picture.</summary>
    public bool RecordWebcam { get; set; }
    /// <summary>Webcam overlay corner (PipCorner name).</summary>
    public string WebcamCorner { get; set; } = "BottomRight";
    /// <summary>Stop a recording after this many minutes (0 = no limit).</summary>
    public int RecordMaxMinutes { get; set; }
    /// <summary>Stop a recording when the file reaches this many MB (0 = no limit).</summary>
    public int RecordMaxMb { get; set; }
    /// <summary>Effect applied automatically when a capture opens in the editor (AutoEffects names).</summary>
    public string AutoEffect { get; set; } = AutoEffects.Default;
    /// <summary>Preset capture rectangle x,y,width,height in physical pixels; null = not set.</summary>
    public int[]? PresetRect { get; set; }
    /// <summary>External image editor executable for the editor's "External editor…" button. Empty = none.</summary>
    public string ExternalEditorPath { get; set; } = "";
    /// <summary>Override path to an ffmpeg.exe. Empty = use the bundled FFmpeg (owner-approved bundle, 2026-11-13).</summary>
    public string FfmpegPath { get; set; } = "";
    /// <summary>Named post-capture workflows (owner program v0.8.0); each runs locally on a finished file.</summary>
    public List<WorkflowDef> Workflows { get; set; } = new();
    /// <summary>Workflow auto-run after a still capture is saved; empty = none.</summary>
    public string CaptureWorkflow { get; set; } = "";
    /// <summary>Workflow auto-run after a recording is saved; empty = none.</summary>
    public string RecordingWorkflow { get; set; } = "";
    /// <summary>The Windows "Print Screen opens Snipping Tool" value recorded the first time SharpSnip applied its Print Screen override: -1 = the value did not exist, 0/1 = its DWORD. Written back by --clean. The override itself is unconditional (owner direction, 2026-11-13): Print Screen is SharpSnip's only global hotkey and there is no setting for it.</summary>
    public int PrintScreenSnippingPrior { get; set; } = -1;
    /// <summary>True once PrintScreenSnippingPrior holds a real recorded value (the override has been applied at least once).</summary>
    public bool PrintScreenPriorRecorded { get; set; }
    /// <summary>False when the user hid the tray icon from its context menu. Running SharpSnip again brings it back.</summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>Matte for monitor-layout gaps as 0xAARRGGBB.</summary>
    public uint MatteArgb => MatteColor switch
    {
        "Black" => 0xFF000000u,
        "White" => 0xFFFFFFFFu,
        _ => 0x00000000u,
    };

    /// <summary>Canonical file extension for <see cref="ImageFormat"/> (aliases already folded).</summary>
    public string Extension => ImageFormats.Normalize(ImageFormat) ?? ImageFormats.Default;

    /// <summary>Clamp every field to a legal value. Never throws.</summary>
    public Preferences Sanitize()
    {
        SchemaVersion = CurrentSchema;
        OutputFolder ??= "";
        ImageFormat = ImageFormats.Normalize(ImageFormat) ?? ImageFormats.Default;
        FixedWidth = FixedRegion.ClampSize(FixedWidth);
        FixedHeight = FixedRegion.ClampSize(FixedHeight);
        IntervalSeconds = IntervalPlan.SanitizeSeconds(IntervalSeconds);
        FullScreenTarget = SharpSnip.Core.FullScreenTarget.Parse(FullScreenTarget).Format();
        if (FullScreenTarget.StartsWith("Monitor:", StringComparison.Ordinal)) FullScreenTarget = "CurrentMonitor";
        DefaultDelaySeconds = DelayCycle.Sanitize(DefaultDelaySeconds);
        DimmerOpacity = double.IsFinite(DimmerOpacity) ? Math.Clamp(DimmerOpacity, 0.2, 0.9) : 0.47;
        MatteColor = MatteColor is "Transparent" or "Black" or "White" ? MatteColor : "Transparent";
        if (EditorBounds is { Length: not 4 }) EditorBounds = null;
        if (LastCaptureRect is not { Length: 4 } r || r[2] is < 1 or > 32767 || r[3] is < 1 or > 32767) LastCaptureRect = null;
        if (RecordingRegion is not { Length: 4 } rr || rr[2] is < 1 or > 32767 || rr[3] is < 1 or > 32767) RecordingRegion = null;
        PrintScreenSnippingPrior = Math.Clamp(PrintScreenSnippingPrior, -1, 1);
        RecordingFps = RecordingPlan.ClampFps(RecordingFps);
        RecordingQualityName = RecordingQuality.Sanitize(RecordingQualityName);
        RecordingSizeName = RecordingSize.Sanitize(RecordingSizeName);
        WebcamCorner = PipLayout.ParseCorner(WebcamCorner).ToString();
        RecordMaxMinutes = Math.Clamp(RecordMaxMinutes, 0, 1440);
        RecordMaxMb = Math.Clamp(RecordMaxMb, 0, 102400);
        AutoEffect = AutoEffects.Sanitize(AutoEffect);
        ExternalEditorPath = (ExternalEditorPath ?? "").Trim();
        FfmpegPath = (FfmpegPath ?? "").Trim();
        Workflows ??= new();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Workflows = Workflows
            .Where(w => w is not null)
            .Select(w => { w.Name = (w.Name ?? "").Trim(); w.ApplyTo = WorkflowPlan.SanitizeApplyTo(w.ApplyTo); w.Steps = WorkflowPlan.SanitizeSteps(w.Steps); return w; })
            .Where(w => w.Name.Length > 0 && seenNames.Add(w.Name))
            .Take(WorkflowPlan.MaxWorkflows)
            .ToList();
        CaptureWorkflow = WorkflowPlan.Find(Workflows, CaptureWorkflow)?.Name ?? "";
        RecordingWorkflow = WorkflowPlan.Find(Workflows, RecordingWorkflow)?.Name ?? "";
        if (PresetRect is not { Length: 4 } pr || pr[2] is < 1 or > 32767 || pr[3] is < 1 or > 32767) PresetRect = null;
        return this;
    }
}

/// <summary>Load/save with quarantine of unparseable files and atomic writes.</summary>
public static class PrefsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Preferences Load(string path, out string? warning)
    {
        warning = null;
        if (!File.Exists(path)) return new Preferences();
        try
        {
            var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path), Options);
            if (prefs is null) throw new JsonException("empty document");
            return prefs.Sanitize();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            string bad = path + ".bad-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            try { File.Move(path, bad); warning = $"Settings unreadable ({ex.Message}); moved to {Path.GetFileName(bad)}, defaults loaded."; }
            catch (Exception) { warning = $"Settings unreadable ({ex.Message}); defaults loaded."; }
            return new Preferences();
        }
    }

    public static void Save(string path, Preferences prefs)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        string tmp = Path.Combine(dir, Path.GetFileName(path) + ".tmp");
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(fs, prefs.Sanitize(), Options);
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}
