using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>Which audio sources ride along with a recording (each is best-effort: a missing device just drops out).</summary>
internal sealed record AudioOptions(bool System, bool Mic);

/// <summary>Everything the Recorder window asks the host to start — one object so the surface and the host stay in step.</summary>
internal sealed record RecordRequest(MonitorInfo Monitor, bool UseRegion, int Fps, bool SystemAudio, bool Mic, string Quality, bool Webcam, string WebcamCorner, int MaxMinutes, int MaxMb, bool Gif, string SizeName);

/// <summary>Webcam picture-in-picture: the overlay forces the pixel path so frames can be composited.</summary>
internal sealed record WebcamOptions(bool Enabled, string Corner);

/// <summary>
/// Monitor recording to MP4 (H.264) on the WGC stream + MediaTranscoder —
/// in-box Windows.Media only, hardware acceleration on. Frames flow
/// WGC surface → MediaStreamSample → transcoder → file. Video only in this
/// slice; audio and webcam join in the next recording slice. The sample
/// pump is pull-based: the transcoder asks, the recorder answers with the
/// newest completed frame; a frame's duration is set when its successor
/// arrives, and Stop flushes the last one.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class ScreenRecorder : IRecording
{
    private readonly WgcStream _stream;
    private readonly MediaStreamSource _source;
    private readonly FileStream _file;
    private readonly int _fps;
    private readonly object _gate = new();
    private MediaStreamSample? _ready, _pending;
    private TimeSpan _pendingTs;
    private MediaStreamSourceSampleRequest? _waiting;
    private MediaStreamSourceSampleRequestDeferral? _waitingDeferral;
    private TimeSpan? _base;
    private bool _stopping;
    private Task? _transcode;
    private readonly RectPx? _crop;
    private readonly TimeSpan _frameInterval;
    private TimeSpan _lastPixelTs = TimeSpan.MinValue;
    private readonly string? _audioDir;
    private readonly List<WasapiCapture> _captures = new();
    private MediaEncodingProfile? _profile;
    private readonly WebcamCapture? _webcam;
    private readonly PipCorner _webcamCorner;
    private LimitWatcher? _watcher;

    /// <summary>Raised once when a time/size limit trips; the owner stops the recording through the normal path.</summary>
    public event Action? LimitReached;

    public string OutputPath { get; }

    /// <summary>Timestamp of the newest accepted frame — the recording's length so far.</summary>
    public TimeSpan Elapsed { get { lock (_gate) return _pendingTs; } }

    private ScreenRecorder(WgcStream stream, MediaStreamSource source, string outputPath, string videoPath, int fps, RectPx? crop, string? audioDir, WebcamCapture? webcam, PipCorner webcamCorner)
    {
        _stream = stream; _source = source; _fps = fps; OutputPath = outputPath; _crop = crop; _audioDir = audioDir;
        _webcam = webcam; _webcamCorner = webcamCorner;
        _frameInterval = TimeSpan.FromSeconds(1.0 / fps);
        _file = new FileStream(videoPath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    /// <summary>crop, when given, is a monitor-relative rectangle recorded through the pixel path.</summary>
    public static async Task<ScreenRecorder?> StartAsync(MonitorInfo monitor, string outputPath, int fps, RectPx? crop = null, AudioOptions? audio = null, string? qualityName = null, WebcamOptions? webcam = null, int maxSeconds = 0, long maxBytes = 0, string? sizeName = null)
    {
        var stream = WgcStream.StartMonitor(monitor);
        if (stream is null) return null;

        WebcamCapture? cam = null;
        var camCorner = PipCorner.BottomRight;
        if (webcam is { Enabled: true })
        {
            cam = await WebcamCapture.StartAsync();
            if (cam is null) Log.Warn("Webcam overlay was requested but no camera opened; recording without it.");
            else
            {
                camCorner = PipLayout.ParseCorner(webcam.Corner);
                crop ??= new RectPx(0, 0, stream.Size.Width, stream.Size.Height); // compositing needs the pixel path
            }
        }

        // Audio first: whether any source actually opened decides if the video
        // detours through a temp file for the mux at Stop.
        string? audioDir = null;
        var captures = new List<WasapiCapture>();
        if (audio is { } want && (want.System || want.Mic))
        {
            audioDir = Path.Combine(Path.GetTempPath(), "SharpSnip", "rec-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(audioDir);
            if (want.System && WasapiCapture.Start(loopback: true, Path.Combine(audioDir, "system.wav")) is { } sys) captures.Add(sys);
            if (want.Mic && WasapiCapture.Start(loopback: false, Path.Combine(audioDir, "mic.wav")) is { } mic) captures.Add(mic);
            if (captures.Count == 0)
            {
                Log.Warn("Recording audio was requested but no audio endpoint opened; recording video only.");
                try { Directory.Delete(audioDir, true); } catch { }
                audioDir = null;
            }
        }
        string videoPath = audioDir is not null ? Path.Combine(audioDir, "video.mp4") : outputPath;
        if (crop is { } c)
        {
            var clipped = new RectPx(Math.Max(0, c.X), Math.Max(0, c.Y),
                Math.Min(c.Width, stream.Size.Width - Math.Max(0, c.X)), Math.Min(c.Height, stream.Size.Height - Math.Max(0, c.Y)));
            var (ew, eh) = RecordingPlan.EvenSize(clipped.Width, clipped.Height);
            if (ew < 16 || eh < 16) { cam?.Dispose(); stream.Dispose(); return null; }
            crop = new RectPx(clipped.X, clipped.Y, ew, eh);
        }
        var (w, h) = crop is { } cp ? (cp.Width, cp.Height) : RecordingPlan.EvenSize(stream.Size.Width, stream.Size.Height);
        var (tw, th) = RecordingSize.Target(w, h, sizeName);
        // The stream descriptor declares the SOURCE size; the transcode profile
        // carries the TARGET size, so the transcoder scales either path (GPU
        // surfaces or composed pixel frames) down to the requested output size.
        var props = VideoEncodingProperties.CreateH264();
        props.Width = (uint)w; props.Height = (uint)h;
        props.FrameRate.Numerator = (uint)RecordingPlan.ClampFps(fps); props.FrameRate.Denominator = 1;
        props.Bitrate = RecordingPlan.Bitrate(tw, th, fps, RecordingQuality.Factor(qualityName));
        var outProps = VideoEncodingProperties.CreateH264();
        outProps.Width = (uint)tw; outProps.Height = (uint)th;
        outProps.FrameRate.Numerator = (uint)RecordingPlan.ClampFps(fps); outProps.FrameRate.Denominator = 1;
        outProps.Bitrate = props.Bitrate;
        var descriptor = new VideoStreamDescriptor(props);
        var source = new MediaStreamSource(descriptor) { BufferTime = TimeSpan.Zero };
        var recorder = new ScreenRecorder(stream, source, outputPath, videoPath, RecordingPlan.ClampFps(fps), crop, audioDir, cam, camCorner);
        recorder._captures.AddRange(captures);
        source.SampleRequested += recorder.OnSampleRequested;
        stream.FrameReady += recorder.OnFrame;
        try
        {
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
            profile.Video = outProps;
            profile.Audio = null;
            recorder._profile = profile;
            var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
            var prep = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, recorder._file.AsRandomAccessStream(), profile);
            if (!prep.CanTranscode) { recorder.Dispose(); return null; }
            recorder._transcode = Task.Run(async () => await prep.TranscodeAsync());
            if (maxSeconds > 0 || maxBytes > 0)
            {
                recorder._watcher = new LimitWatcher(maxSeconds, maxBytes, () => new FileInfo(videoPath).Length);
                recorder._watcher.Reached += () => recorder.LimitReached?.Invoke();
            }
            return recorder;
        }
        catch
        {
            foreach (var cap in captures) cap.Dispose();
            cam?.Dispose();
            if (audioDir is not null) { try { Directory.Delete(audioDir, true); } catch { } }
            recorder.Dispose();
            return null;
        }
    }

    private void OnFrame(Direct3D11CaptureFrame frame)
    {
        MediaStreamSourceSampleRequest? deliver = null;
        MediaStreamSourceSampleRequestDeferral? deliverDeferral = null;
        MediaStreamSample? deliverSample = null;
        using (frame)
        {
            TimeSpan ts;
            lock (_gate) { if (_stopping) return; _base ??= frame.SystemRelativeTime; ts = frame.SystemRelativeTime - _base.Value; }
            var sample = MakeSample(frame, ts);
            if (sample is null) return; // throttled or unreadable frame: dropped, the pump is undisturbed
            lock (_gate)
            {
                if (_stopping) return;
                if (_pending is not null)
                {
                    _pending.Duration = ts - _pendingTs;
                    _ready = _pending; // an unconsumed ready sample is replaced: the encoder is slower than the screen
                    if (_waiting is not null)
                    {
                        deliver = _waiting; deliverDeferral = _waitingDeferral; deliverSample = _ready;
                        _ready = null; _waiting = null; _waitingDeferral = null;
                    }
                }
                _pending = sample; _pendingTs = ts;
            }
        }
        if (deliver is not null) { deliver.Sample = deliverSample; deliverDeferral?.Complete(); }
    }

    /// <summary>Full frames ride the GPU surface; a crop (or the webcam overlay) rides pixels, throttled to the target rate.</summary>
    private MediaStreamSample? MakeSample(Direct3D11CaptureFrame frame, TimeSpan ts)
    {
        if (_crop is null && _webcam is null) return MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, ts);
        if (ts - _lastPixelTs < _frameInterval * 0.9) return null;
        var dst = PixelCanvas.Compose(_stream, frame, _crop, _webcam, _webcamCorner, out _, out _);
        if (dst is null) return null;
        _lastPixelTs = ts;
        return MediaStreamSample.CreateFromBuffer(dst.AsBuffer(), ts);
    }

    private void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
    {
        lock (_gate)
        {
            if (_ready is not null) { args.Request.Sample = _ready; _ready = null; return; }
            if (_stopping) { args.Request.Sample = null; return; }
            _waiting = args.Request;
            _waitingDeferral = args.Request.GetDeferral();
        }
    }

    /// <summary>Flushes the last frame, ends the stream and waits for the file to finish writing.</summary>
    public async Task StopAsync()
    {
        _watcher?.Dispose(); _watcher = null;
        MediaStreamSourceSampleRequest? waiting;
        MediaStreamSourceSampleRequestDeferral? deferral;
        MediaStreamSample? flush = null;
        lock (_gate)
        {
            _stopping = true;
            if (_pending is not null)
            {
                _pending.Duration = TimeSpan.FromSeconds(1.0 / _fps);
                _ready ??= _pending;
                _pending = null;
            }
            waiting = _waiting; deferral = _waitingDeferral; _waiting = null; _waitingDeferral = null;
            if (waiting is not null && _ready is not null) { flush = _ready; _ready = null; }
        }
        if (waiting is not null) { waiting.Sample = flush; deferral?.Complete(); }
        if (_transcode is not null)
        {
            var done = await Task.WhenAny(_transcode, Task.Delay(TimeSpan.FromSeconds(30)));
            if (done == _transcode && _transcode.IsFaulted)
                Log.Error("Recording transcode failed", _transcode.Exception!);
        }
        if (_audioDir is not null)
        {
            foreach (var c in _captures) c.Dispose();
            _captures.Clear();
            _file.Dispose(); // the temp video must be closed before the mux reads it
            try { await MuxWithAudioAsync(Path.Combine(_audioDir, "video.mp4")); }
            catch (Exception ex)
            {
                Log.Error("Audio mux failed; keeping the silent video", ex);
                try { File.Copy(Path.Combine(_audioDir, "video.mp4"), OutputPath, overwrite: true); } catch { }
            }
            try { Directory.Delete(_audioDir, recursive: true); } catch { }
        }
    }

    /// <summary>Mixes the captured tracks (if two) and renders video + audio into the output MP4.</summary>
    private async Task MuxWithAudioAsync(string videoPath)
    {
        string? audioPath = BuildAudioTrack();
        if (audioPath is null) { File.Copy(videoPath, OutputPath, overwrite: true); return; }
        var videoFile = await StorageFile.GetFileFromPathAsync(videoPath);
        var audioFile = await StorageFile.GetFileFromPathAsync(audioPath);
        var comp = new MediaComposition();
        comp.Clips.Add(await MediaClip.CreateFromFileAsync(videoFile));
        comp.BackgroundAudioTracks.Add(await BackgroundAudioTrack.CreateFromFileAsync(audioFile));
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(OutputPath)!);
        var outFile = await folder.CreateFileAsync(Path.GetFileName(OutputPath), CreationCollisionOption.ReplaceExisting);
        var result = await comp.RenderToFileAsync(outFile, MediaTrimmingPreference.Precise,
            _profile ?? MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto));
        if (result != TranscodeFailureReason.None)
            throw new InvalidOperationException("Composition render failed: " + result);
    }

    private string? BuildAudioTrack()
    {
        var tracks = new List<(string Path, int Rate)>();
        string sysPath = Path.Combine(_audioDir!, "system.wav"), micPath = Path.Combine(_audioDir!, "mic.wav");
        // Rates were captured on the WasapiCapture objects; after Dispose they are gone, so the WAV headers carry them.
        if (File.Exists(sysPath) && new FileInfo(sysPath).Length > 44) tracks.Add((sysPath, ReadWavRate(sysPath)));
        if (File.Exists(micPath) && new FileInfo(micPath).Length > 44) tracks.Add((micPath, ReadWavRate(micPath)));
        if (tracks.Count == 0) return null;
        if (tracks.Count == 1) return tracks[0].Path;
        var (aSamples, aRate) = ReadWavData(tracks[0]);
        var (bSamples, bRate) = ReadWavData(tracks[1]);
        if (bRate != aRate) bSamples = PcmMix.Resample(bSamples, 2, bRate, aRate);
        var mixed = PcmMix.Mix(aSamples, bSamples);
        string mixedPath = Path.Combine(_audioDir!, "mixed.wav");
        using (var fs = new FileStream(mixedPath, FileMode.Create, FileAccess.Write))
        using (var w = new WavWriter(fs, aRate, 2)) w.Write(mixed);
        return mixedPath;
    }

    private static int ReadWavRate(string path)
    {
        using var fs = File.OpenRead(path);
        fs.Position = 24;
        var b = new byte[4]; _ = fs.Read(b, 0, 4);
        return BitConverter.ToInt32(b, 0);
    }

    private static (short[] Samples, int Rate) ReadWavData((string Path, int Rate) track)
    {
        var bytes = File.ReadAllBytes(track.Path);
        int dataBytes = BitConverter.ToInt32(bytes, 40);
        var samples = new short[dataBytes / 2];
        for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(bytes, 44 + i * 2);
        return (samples, track.Rate);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _stream.FrameReady -= OnFrame;
        _stream.Dispose();
        foreach (var c in _captures) c.Dispose();
        _webcam?.Dispose();
        _file.Dispose();
    }
}
