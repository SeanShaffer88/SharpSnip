using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpSnip.Core;

namespace SharpSnip;

internal static class Exporter
{
    public static string OutputFolder(Preferences prefs) =>
        OutputNaming.ResolveFolder(prefs.OutputFolder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Write the image atomically (temp file, flush, move). Returns the final path.</summary>
    public static string Save(BitmapSource image, Preferences prefs, string? pathOverride = null)
    {
        string path = pathOverride ?? Path.Combine(OutputFolder(prefs), OutputNaming.FileName(DateTime.Now, prefs.Extension));
        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);

        BitmapEncoder enc;
        BitmapSource src = image;
        if (string.Equals(Path.GetExtension(path), ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(path), ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            enc = new JpegBitmapEncoder { QualityLevel = 92 };
            src = new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0); // JPEG has no alpha
        }
        else enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));

        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            enc.Save(fs);
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
        Log.Info($"Saved {path}");
        return path;
    }
}
