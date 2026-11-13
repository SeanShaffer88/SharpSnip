using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace SharpSnip;

/// <summary>
/// The default camera as a "latest frame" source for the recording overlay:
/// a MediaFrameReader in CPU/BGRA mode keeps the newest frame's pixels under
/// a lock; the recorder samples them when it composites. Start returns null
/// when there is no camera or access is denied — the recording just goes
/// ahead without the overlay.
/// </summary>
internal sealed class WebcamCapture : IDisposable
{
    private readonly MediaCapture _capture;
    private readonly MediaFrameReader _reader;
    private readonly object _gate = new();
    private byte[]? _latest;
    private int _w, _h;
    private bool _disposed;

    private WebcamCapture(MediaCapture capture, MediaFrameReader reader) { _capture = capture; _reader = reader; }

    public static async Task<WebcamCapture?> StartAsync()
    {
        MediaCapture? capture = null;
        try
        {
            capture = new MediaCapture();
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            });
            var source = capture.FrameSources.Values.FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
                         ?? capture.FrameSources.Values.FirstOrDefault();
            if (source is null) { capture.Dispose(); return null; }
            var reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
            var cam = new WebcamCapture(capture, reader);
            reader.FrameArrived += cam.OnFrame;
            if (await reader.StartAsync() != MediaFrameReaderStartStatus.Success) { cam.Dispose(); return null; }
            return cam;
        }
        catch { capture?.Dispose(); return null; }
    }

    private void OnFrame(MediaFrameReader sender, MediaFrameArrivedEventArgs _)
    {
        using var frame = sender.TryAcquireLatestFrame();
        var bmp = frame?.VideoMediaFrame?.SoftwareBitmap;
        if (bmp is null) return;
        var buffer = new Windows.Storage.Streams.Buffer((uint)(bmp.PixelWidth * bmp.PixelHeight * 4));
        bmp.CopyToBuffer(buffer);
        var bytes = new byte[buffer.Length];
        using (var dr = DataReader.FromBuffer(buffer)) dr.ReadBytes(bytes);
        lock (_gate) { _latest = bytes; _w = bmp.PixelWidth; _h = bmp.PixelHeight; }
    }

    public bool TryGetLatest(out byte[] pixels, out int width, out int height)
    {
        lock (_gate)
        {
            pixels = _latest ?? Array.Empty<byte>(); width = _w; height = _h;
            return _latest is not null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _reader.StopAsync().AsTask().Wait(TimeSpan.FromSeconds(1)); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); }
        _reader.Dispose();
        _capture.Dispose();
    }
}
