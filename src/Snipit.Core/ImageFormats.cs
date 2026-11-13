namespace Snipit.Core;

/// <summary>
/// Save formats Snipit can write: the encoders built into WPF (PNG, JPEG,
/// BMP, GIF, TIFF) plus two hand-rolled writers — "pdf" (<see cref="PdfWriter"/>)
/// and "apng" (<see cref="ApngWriter"/>, which wraps WPF's PNG output).
/// Canonical names are the file extensions: "jpeg" canonicalises to "jpg" and
/// "tiff" to "tif". WebP is deliberately absent: WPF has no WebP encoder (see AUDIT.md).
/// </summary>
public static class ImageFormats
{
    public static readonly IReadOnlyList<string> All = new[] { "png", "jpg", "bmp", "gif", "tif", "pdf", "apng" };

    public const string Default = "png";

    /// <summary>Canonical name for a format or extension, or null when it is not supported.</summary>
    public static string? Normalize(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return null;
        string f = format.Trim().TrimStart('.').ToLowerInvariant();
        return f switch
        {
            "jpeg" => "jpg",
            "tiff" => "tif",
            _ => All.Contains(f) ? f : null,
        };
    }

    public static bool IsSupported(string? format) => Normalize(format) is not null;

    /// <summary>
    /// Format implied by a file path's extension; when the extension is missing or
    /// unsupported, the (normalised) fallback wins, and an invalid fallback means PNG.
    /// </summary>
    public static string FromPath(string? path, string? fallback)
    {
        string? ext = path is null ? null : Path.GetExtension(path);
        return Normalize(ext) ?? Normalize(fallback) ?? Default;
    }

    /// <summary>
    /// True for formats that cannot carry the captured alpha: pixels are
    /// converted to an opaque format before encoding (the PDF writer is RGB-only).
    /// PNG and TIFF keep alpha.
    /// </summary>
    public static bool FlattensAlpha(string? format) => Normalize(format) is "jpg" or "bmp" or "gif" or "pdf";
}
