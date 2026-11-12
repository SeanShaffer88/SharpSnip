namespace SharpSnip.Core;

/// <summary>Supported save formats. All encoders are in-box WPF; PDF is a hand-written wrapper.</summary>
public static class ImageFormats
{
    public static readonly string[] All = { "png", "jpg", "bmp", "tiff", "gif", "pdf" };
    public static bool IsValid(string? f) => f is not null && Array.IndexOf(All, f) >= 0;
    public static string Sanitize(string? f) => IsValid(f) ? f! : "png";
    public static bool HasAlpha(string f) => f is "png" or "tiff";

    /// <summary>File extension for a format name; also maps ".jpeg"/".tif" back to canonical names.</summary>
    public static string Extension(string f) => f == "tiff" ? "tif" : f;
    public static string FromExtension(string ext) => ext.TrimStart('.').ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => "jpg", "tif" or "tiff" => "tiff", "bmp" => "bmp", "gif" => "gif", "pdf" => "pdf", _ => "png",
    };
}
