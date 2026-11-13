using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpSnip.Core;

namespace SharpSnip;

internal static class Exporter
{
    public static string OutputFolder(Preferences prefs) =>
        OutputNaming.ResolveFolder(prefs.OutputFolder, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

    /// <summary>Write the image atomically (temp file, flush, move). Returns the final path.</summary>
    public static string Save(BitmapSource image, Preferences prefs, string? pathOverride = null)
    {
        // Generated names never overwrite an earlier capture (two saves can land in the
        // same millisecond); a path the user named explicitly may overwrite, as asked.
        string path = pathOverride ?? OutputNaming.UniquePath(
            Path.Combine(OutputFolder(prefs), OutputNaming.FileName(DateTime.Now, prefs.Extension)), File.Exists);
        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);

        // The bytes written always match the file name: the format comes from the
        // path's extension (falling back to the configured format), never silently PNG.
        string format = ImageFormats.FromPath(path, prefs.ImageFormat);

        if (format == "pdf")
        {
            // One image per PDF, pixels embedded losslessly as RGB (no alpha).
            var bgr = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
            int w = bgr.PixelWidth, h = bgr.PixelHeight, stride = w * 3;
            var bgrBytes = new byte[stride * h];
            bgr.CopyPixels(bgrBytes, stride, 0);
            var rgb = new byte[stride * h];
            for (int i = 0; i < rgb.Length; i += 3) { rgb[i] = bgrBytes[i + 2]; rgb[i + 1] = bgrBytes[i + 1]; rgb[i + 2] = bgrBytes[i]; }
            WriteAtomic(path, fs => fs.Write(PdfWriter.Build(rgb, w, h, "SharpSnip capture")));
            Log.Info($"Saved {path}");
            return path;
        }

        BitmapSource src = ImageFormats.FlattensAlpha(format)
            ? new FormatConvertedBitmap(image, PixelFormats.Bgr32, null, 0) // JPEG/BMP/GIF have no usable alpha
            : image;
        BitmapEncoder enc = format switch
        {
            "jpg" => new JpegBitmapEncoder { QualityLevel = 92 },
            "bmp" => new BmpBitmapEncoder(),
            "gif" => new GifBitmapEncoder(),
            "tif" => new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw },
            _ => new PngBitmapEncoder(),
        };
        enc.Frames.Add(BitmapFrame.Create(src));

        WriteAtomic(path, fs => enc.Save(fs));
        Log.Info($"Saved {path}");
        return path;
    }

    private static void WriteAtomic(string path, Action<FileStream> write)
    {
        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            write(fs);
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}
