namespace SharpSnip.Core;

/// <summary>
/// Numbers for screen recording: file naming, frame-rate bounds, even frame
/// sizes (H.264 requires both dimensions even) and a bitrate heuristic —
/// roughly 0.12 bits per pixel per frame, clamped to a sane band. Pure math,
/// shared by the recorder engine and every surface that drives it.
/// </summary>
public static class RecordingPlan
{
    public const int MinFps = 1;
    public const int MaxFps = 60;
    public const int DefaultFps = 30;
    public const int MaxSeconds = 4 * 60 * 60;

    public static int ClampFps(int fps) => Math.Clamp(fps, MinFps, MaxFps);

    public static string FileName(DateTime local) => $"recording-{local:yyyyMMdd-HHmmss}.mp4";

    public static string GifFileName(DateTime local) => $"recording-{local:yyyyMMdd-HHmmss}.gif";

    /// <summary>GIF frame rate ceiling — past this the file balloons and players struggle.</summary>
    public const int MaxGifFps = 20;

    /// <summary>True when a recording limit has been reached; a zero limit disables that check.</summary>
    public static bool LimitHit(TimeSpan elapsed, int maxSeconds, long fileBytes, long maxBytes) =>
        (maxSeconds > 0 && elapsed.TotalSeconds >= maxSeconds) || (maxBytes > 0 && fileBytes >= maxBytes);

    /// <summary>Rounds both dimensions down to even numbers (minimum 2).</summary>
    public static (int Width, int Height) EvenSize(int width, int height) =>
        (Math.Max(2, width - width % 2), Math.Max(2, height - height % 2));

    /// <summary>Target bitrate in bits per second for the given frame size and rate.</summary>
    public static uint Bitrate(int width, int height, int fps, double bitsPerPixel = RecordingQuality.DefaultFactor)
    {
        double bits = (double)width * height * ClampFps(fps) * bitsPerPixel;
        return (uint)Math.Clamp(bits, 1_000_000, 80_000_000);
    }
}

/// <summary>Output height caps for recordings: the frame is scaled to fit, never upscaled, dimensions kept even.</summary>
public static class RecordingSize
{
    public static readonly string[] All = { "Original", "1080p", "720p", "480p" };
    public const string Default = "Original";

    public static string Sanitize(string? name) =>
        name is not null && Array.IndexOf(All, name) >= 0 ? name : Default;

    public static (int Width, int Height) Target(int width, int height, string? name)
    {
        int cap = Sanitize(name) switch { "1080p" => 1080, "720p" => 720, "480p" => 480, _ => 0 };
        if (cap == 0 || height <= cap) return RecordingPlan.EvenSize(width, height);
        double scale = (double)cap / height;
        return RecordingPlan.EvenSize((int)Math.Round(width * scale), cap);
    }
}

/// <summary>Named recording quality presets — a bits-per-pixel-per-frame factor behind a friendly label.</summary>
public static class RecordingQuality
{
    public static readonly string[] All = { "Low", "Medium", "High", "Ultra" };
    public const string Default = "Medium";
    public const double DefaultFactor = 0.12;

    public static double Factor(string? name) => name switch
    {
        "Low" => 0.05,
        "High" => 0.20,
        "Ultra" => 0.35,
        _ => DefaultFactor,
    };

    public static string Sanitize(string? name) =>
        name is not null && Array.IndexOf(All, name) >= 0 ? name : Default;
}
