using System.Text.Json;

namespace SharpSnip.Core;

/// <summary>User settings. Stored as human-readable, versioned JSON; never tied to hardware or SID.</summary>
public sealed class Preferences
{
    public const int CurrentSchema = 1;

    public int SchemaVersion { get; set; } = CurrentSchema;
    public string OutputFolder { get; set; } = "";
    public string ImageFormat { get; set; } = "png";
    public string HotkeyMenu { get; set; } = "Win+Shift+X";
    public string HotkeyFullScreen { get; set; } = "Win+Shift+F";
    public string HotkeyActiveWindow { get; set; } = "Win+Shift+W";
    public string HotkeyRepeatLast { get; set; } = "Win+Shift+R";
    public string HotkeyMonitors { get; set; } = HotkeyList.Default;
    public int FixedWidth { get; set; } = 1280;
    public int FixedHeight { get; set; } = 720;
    public int IntervalSeconds { get; set; } = 5;
    public string FullScreenTarget { get; set; } = "CurrentMonitor";
    public int DefaultDelaySeconds { get; set; }
    public bool OpenEditorAfterCapture { get; set; } = true;
    public bool ShowToasts { get; set; } = true;
    public double DimmerOpacity { get; set; } = 0.47;
    public bool HighContrastHighlight { get; set; }
    public string MatteColor { get; set; } = "Transparent";
    /// <summary>Editor window x,y,width,height in physical pixels; null = never saved.</summary>
    public int[]? EditorBounds { get; set; }
    /// <summary>Last capture rectangle x,y,width,height in physical pixels, so "repeat last" survives a restart; null = none yet.</summary>
    public int[]? LastCaptureRect { get; set; }
    public bool EditorMaximized { get; set; }
    /// <summary>True when SharpSnip has taken the Print Screen key over from Snipping Tool.</summary>
    public bool PrintScreenTakeover { get; set; }
    /// <summary>The Windows "Print Screen opens Snipping Tool" value recorded when takeover was switched on: -1 = the value did not exist, 0/1 = its DWORD. Written back when takeover is switched off.</summary>
    public int PrintScreenSnippingPrior { get; set; } = -1;
    /// <summary>Menu hotkey from before takeover, restored when takeover is switched off; empty = none recorded.</summary>
    public string HotkeyMenuBeforeTakeover { get; set; } = "";
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
        if (!HotkeySpec.TryParse(HotkeyMenu, out _)) HotkeyMenu = "Win+Shift+X";
        if (!HotkeySpec.TryParse(HotkeyFullScreen, out _)) HotkeyFullScreen = "Win+Shift+F";
        if (!HotkeySpec.TryParse(HotkeyActiveWindow, out _)) HotkeyActiveWindow = "Win+Shift+W";
        if (!HotkeySpec.TryParse(HotkeyRepeatLast, out _)) HotkeyRepeatLast = "Win+Shift+R";
        if (!HotkeyList.TryParse(HotkeyMonitors, out _)) HotkeyMonitors = HotkeyList.Default;
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
        PrintScreenSnippingPrior = Math.Clamp(PrintScreenSnippingPrior, -1, 1);
        HotkeyMenuBeforeTakeover ??= "";
        if (HotkeyMenuBeforeTakeover.Length > 0 && !HotkeySpec.TryParse(HotkeyMenuBeforeTakeover, out _)) HotkeyMenuBeforeTakeover = "";
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
