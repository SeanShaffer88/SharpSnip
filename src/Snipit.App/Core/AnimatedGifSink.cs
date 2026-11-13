using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snipit.Core;

namespace Snipit;

/// <summary>
/// Collects interval frames into one animated GIF (the roadmap's "GIF from screen
/// activity"). Each frame is quantised by WPF to the 256-colour halftone palette and
/// streamed through the Core <see cref="GifWriter"/>, so memory stays at one frame
/// however long the run is. The file appears under its final name only on Dispose:
/// until then it is a ".tmp", so a killed run never leaves a truncated "animation.gif".
/// </summary>
internal sealed class AnimatedGifSink : IDisposable
{
    private FileStream? _file;
    private GifWriter? _writer;

    public AnimatedGifSink(string folder)
    {
        Directory.CreateDirectory(folder);
        FinalPath = Path.Combine(folder, GifPlan.FileName);
    }

    public string FinalPath { get; }
    public int Frames { get; private set; }

    /// <summary>The fixed 256-colour palette as 768 RGB bytes, read from WPF's halftone palette.</summary>
    internal static byte[] PaletteRgb()
    {
        var colors = BitmapPalettes.Halftone256.Colors;
        var rgb = new byte[256 * 3];
        for (int i = 0; i < Math.Min(256, colors.Count); i++)
        {
            rgb[i * 3] = colors[i].R;
            rgb[i * 3 + 1] = colors[i].G;
            rgb[i * 3 + 2] = colors[i].B;
        }
        return rgb;
    }

    /// <summary>Add one frame. Returns false when its size differs from the first frame's (display changed).</summary>
    public bool Add(CaptureResult cap, ushort delayHundredths)
    {
        if (_writer is null)
        {
            _file = new FileStream(FinalPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None);
            _writer = new GifWriter(_file, cap.Image.PixelWidth, cap.Image.PixelHeight, PaletteRgb(), loopCount: 0);
        }
        if (cap.Image.PixelWidth != _writer.Width || cap.Image.PixelHeight != _writer.Height) return false;

        var indexed = new FormatConvertedBitmap(cap.Image, PixelFormats.Indexed8, BitmapPalettes.Halftone256, 0);
        var indices = new byte[_writer.Width * _writer.Height];
        indexed.CopyPixels(indices, _writer.Width, 0);
        _writer.AddFrame(indices, delayHundredths);
        Frames++;
        return true;
    }

    public void Dispose()
    {
        _writer?.Dispose(); // writes the trailer
        _file?.Dispose();
        if (Frames > 0 && File.Exists(FinalPath + ".tmp"))
        {
            if (File.Exists(FinalPath)) File.Replace(FinalPath + ".tmp", FinalPath, null);
            else File.Move(FinalPath + ".tmp", FinalPath);
            Log.Info($"Saved {FinalPath}");
        }
    }
}
