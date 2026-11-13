using System.IO;
using System.Runtime.Versioning;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Capture;
using Snipit.Core;

namespace Snipit;

/// <summary>
/// Recording straight to an animated GIF: WGC frames ride the pixel path,
/// are quantised by WPF to the halftone palette and streamed through the
/// Core GifWriter with real per-frame delays. Silent by nature, capped at
/// RecordingPlan.MaxGifFps. The file appears under its final name only at
/// Stop — until then it is a ".tmp", so a killed run leaves no truncated GIF.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class GifRecorder : IRecording
{
    private readonly WgcStream _stream;
    private readonly RectPx? _crop;
    private readonly WebcamCapture? _webcam;
    private readonly PipCorner _corner;
    private readonly TimeSpan _interval;
    private readonly string _sizeName;
    private (int W, int H)? _target;
    private readonly object _gate = new();
    private GifWriter? _writer;
    private FileStream? _file;
    private LimitWatcher? _watcher;
    private readonly PauseClock _clock = new();
    private TimeSpan? _base;
    private TimeSpan _lastTs;
    private int _frames;
    private bool _stopping;

    public string OutputPath { get; }
    public TimeSpan Elapsed => _lastTs;
    public event Action? LimitReached;

    public bool IsPaused => _clock.IsPaused;

    /// <summary>While paused, frames are dropped and later frame delays shift by the paused span, so the pause never appears in the GIF.</summary>
    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_stopping || paused == _clock.IsPaused) return;
            var wall = ScreenRecorder.SrtNow() - (_base ?? ScreenRecorder.SrtNow());
            if (paused) _clock.Pause(wall); else _clock.Resume(wall);
        }
    }

    private GifRecorder(WgcStream stream, string outputPath, int fps, RectPx? crop, WebcamCapture? webcam, PipCorner corner, string sizeName)
    {
        _stream = stream; OutputPath = outputPath; _crop = crop; _webcam = webcam; _corner = corner; _sizeName = sizeName;
        _interval = TimeSpan.FromSeconds(1.0 / fps);
    }

    public static async Task<GifRecorder?> StartAsync(MonitorInfo monitor, string outputPath, int fps, RectPx? crop = null,
        WebcamOptions? webcam = null, int maxSeconds = 0, long maxBytes = 0, string? sizeName = null)
    {
        var stream = WgcStream.StartMonitor(monitor);
        if (stream is null) return null;
        WebcamCapture? cam = null;
        var corner = PipCorner.BottomRight;
        if (webcam is { Enabled: true })
        {
            cam = await WebcamCapture.StartAsync();
            if (cam is null) Log.Warn("Webcam overlay was requested but no camera opened; recording without it.");
            else corner = PipLayout.ParseCorner(webcam.Corner);
        }
        var rec = new GifRecorder(stream, outputPath, Math.Min(RecordingPlan.ClampFps(fps), RecordingPlan.MaxGifFps), crop, cam, corner, RecordingSize.Sanitize(sizeName));
        stream.FrameReady += rec.OnFrame;
        if (maxSeconds > 0 || maxBytes > 0)
        {
            rec._watcher = new LimitWatcher(maxSeconds, maxBytes, () => new FileInfo(outputPath + ".tmp").Length);
            rec._watcher.Reached += () => rec.LimitReached?.Invoke();
        }
        return rec;
    }

    private void OnFrame(Direct3D11CaptureFrame frame)
    {
        using (frame)
        {
            lock (_gate)
            {
                if (_stopping || _clock.IsPaused) return;
                _base ??= frame.SystemRelativeTime;
                var ts = _clock.MediaTime(frame.SystemRelativeTime - _base.Value);
                if (_writer is not null && ts - _lastTs < _interval * 0.9) return;
                var canvas = PixelCanvas.Compose(_stream, frame, _crop, _webcam, _corner, out int w, out int h);
                if (canvas is null) return;
                _target ??= RecordingSize.Target(w, h, _sizeName);
                if (_target.Value.W != w || _target.Value.H != h)
                {
                    var scaled = new byte[_target.Value.W * _target.Value.H * 4];
                    PixelBlit.ScaleCopy(canvas, w * 4, w, h, scaled, _target.Value.W * 4, 0, 0, _target.Value.W, _target.Value.H);
                    canvas = scaled; w = _target.Value.W; h = _target.Value.H;
                }
                if (_writer is null)
                {
                    _file = new FileStream(OutputPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None);
                    _writer = new GifWriter(_file, w, h, AnimatedGifSink.PaletteRgb(), loopCount: 0);
                    _lastTs = ts;
                    AddIndexed(canvas, w, h, (ushort)Math.Clamp((int)Math.Round(_interval.TotalSeconds * 100), 2, 600));
                }
                else
                {
                    if (w != _writer.Width || h != _writer.Height) return; // display changed mid-record: skip the frame
                    ushort delay = (ushort)Math.Clamp((int)Math.Round((ts - _lastTs).TotalSeconds * 100), 2, 600);
                    _lastTs = ts;
                    AddIndexed(canvas, w, h, delay);
                }
            }
        }
    }

    private void AddIndexed(byte[] bgra, int w, int h, ushort delayCs)
    {
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4);
        var indexed = new FormatConvertedBitmap(bmp, PixelFormats.Indexed8, BitmapPalettes.Halftone256, 0);
        var indices = new byte[w * h];
        indexed.CopyPixels(indices, w, 0);
        _writer!.AddFrame(indices, delayCs);
        _frames++;
    }

    public Task StopAsync()
    {
        _stream.FrameReady -= OnFrame;
        lock (_gate)
        {
            _stopping = true;
            _watcher?.Dispose(); _watcher = null;
            _writer?.Dispose(); _writer = null;
            _file?.Dispose(); _file = null;
            if (_frames > 0 && File.Exists(OutputPath + ".tmp"))
            {
                if (File.Exists(OutputPath)) File.Replace(OutputPath + ".tmp", OutputPath, null);
                else File.Move(OutputPath + ".tmp", OutputPath);
            }
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _stream.FrameReady -= OnFrame;
        _watcher?.Dispose();
        _stream.Dispose();
        _webcam?.Dispose();
        lock (_gate) { _writer?.Dispose(); _file?.Dispose(); }
    }
}
