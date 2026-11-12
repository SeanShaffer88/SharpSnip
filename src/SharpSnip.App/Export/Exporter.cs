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
        string path = pathOverride ?? NamePattern.Resolve(OutputFolder(prefs), prefs.FolderPattern, prefs.FileNamePattern,
            prefs.Extension, DateTime.Now, File.Exists);
        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);

        string fmt = ImageFormats.FromExtension(Path.GetExtension(path));
        BitmapSource src = ImageFormats.HasAlpha(fmt) ? image : new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0); // no alpha in the rest
        byte[] bytes;
        if (fmt == "pdf")
        {
            var jpg = new JpegBitmapEncoder { QualityLevel = prefs.JpegQuality };
            jpg.Frames.Add(BitmapFrame.Create(src));
            using var jm = new MemoryStream();
            jpg.Save(jm);
            bytes = PdfWriter.SingleJpegPage(jm.ToArray(), src.PixelWidth, src.PixelHeight);
        }
        else
        {
            BitmapEncoder enc = fmt switch
            {
                "jpg" => new JpegBitmapEncoder { QualityLevel = prefs.JpegQuality },
                "bmp" => new BmpBitmapEncoder(),
                "tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip },
                "gif" => new GifBitmapEncoder(),
                _ => new PngBitmapEncoder(),
            };
            enc.Frames.Add(BitmapFrame.Create(src));
            using var em = new MemoryStream();
            enc.Save(em);
            bytes = em.ToArray();
        }

        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
        Log.Info($"Saved {path}");
        return path;
    }
}
