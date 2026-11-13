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

        WriteAtomic(path, fs => enc.Save(fs));
        Log.Info($"Saved {path}");
        return path;
    }

    private static void WriteAtomic(string path, Action<FileStream> write)
    {
        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}
