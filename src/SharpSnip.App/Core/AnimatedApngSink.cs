using System.IO;
using System.Windows.Media.Imaging;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// Collects interval-capture frames into ONE animated PNG file (looping,
/// frame delay = the interval), the APNG counterpart of AnimatedGifSink.
/// Frames are encoded to PNG by WPF and streamed through Core ApngWriter,
/// so memory stays at one frame however long the run lasts.
/// </summary>
internal sealed class AnimatedApngSink : IDisposable
{
    private FileStream? _file;
    private ApngWriter? _writer;
    private int _w, _h;

    public AnimatedApngSink(string folder)
    {
        Directory.CreateDirectory(folder);
        FinalPath = Path.Combine(folder, ApngPlan.FileName);
    }

    public string FinalPath { get; }
    public int Frames { get; private set; }

    /// <summary>Adds one frame. Returns false when the frame size changed (the caller stops the run).</summary>
    public bool Add(CaptureResult cap, ushort delayHundredths)
    {
        var frame = cap.Image;
        if (_writer is not null && (frame.PixelWidth != _w || frame.PixelHeight != _h)) return false;
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(frame));
        using var ms = new MemoryStream();
        enc.Save(ms);
        byte[] png = ms.ToArray();
        if (_writer is null)
        {
            _w = frame.PixelWidth; _h = frame.PixelHeight;
            _file = new FileStream(FinalPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new ApngWriter(_file, png, delayHundredths);
        }
        else _writer.AddFrame(png, delayHundredths);
        Frames++;
        return true;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _file?.Dispose();
    }
}
