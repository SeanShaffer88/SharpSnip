using System.Diagnostics;
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
using Snipit.Core;

namespace Snipit;

/// <summary>Which audio sources ride along with a recording (each is best-effort: a missing device just drops out).</summary>
internal sealed record AudioOptions(bool System, bool Mic);

/// <summary>Everything the Recorder window asks the host to start — one object so the surface and the host stay in step.</summary>
internal sealed record RecordRequest(MonitorInfo Monitor, bool UseRegion, int Fps, bool SystemAudio, bool Mic, string Quality, bool Webcam, string WebcamCorner, int MaxMinutes, int MaxMb, bool Gif, string SizeName, RectPx? RegionRect = null);

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
    public string? Warning { get; private set; }
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
    private readonly PauseClock _clock = new();
    // Audio pause support: each pause closes the current segment files and a
    // resume opens the next pair; at Stop the segments join back into one track
    // per source, so paused time is absent from the audio exactly as from the video.
    private bool _wantSystem, _wantMic;
    private int _segment;
    private readonly List<string> _sysSegments = new(), _micSegments = new();
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
        bool sysOpened = false, micOpened = false;
        if (audio is { } want && (want.System || want.Mic))
        {
            audioDir = Path.Combine(Path.GetTempPath(), "Snipit", "rec-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(audioDir);
            if (want.System && WasapiCapture.Start(loopback: true, Path.Combine(audioDir, "system-1.wav")) is { } sys) { captures.Add(sys); sysOpened = true; }
            if (want.Mic && WasapiCapture.Start(loopback: false, Path.Combine(audioDir, "mic-1.wav")) is { } mic) { captures.Add(mic); micOpened = true; }
            if (captures.Count == 0)
            {
                Log.Warn("Recording audio was requested but no audio endpoint opened; recording video only.");
                try { Directory.Delete(audioDir, true); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); }
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
        recorder._segment = 1;
        recorder._wantSystem = sysOpened;
        recorder._wantMic = micOpened;
        if (recorder._wantSystem) recorder._sysSegments.Add(Path.Combine(audioDir!, "system-1.wav"));
        if (recorder._wantMic) recorder._micSegments.Add(Path.Combine(audioDir!, "mic-1.wav"));
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
            if (audioDir is not null) { try { Directory.Delete(audioDir, true); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); } }
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
            lock (_gate)
            {
                if (_stopping || _clock.IsPaused) return;
                _base ??= frame.SystemRelativeTime;
                ts = _clock.MediaTime(frame.SystemRelativeTime - _base.Value);
            }
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
            string tempVideo = Path.Combine(_audioDir, "video.mp4");
            bool keepTemp = false;
            try { await MuxWithAudioAsync(tempVideo); }
            catch (Exception ex)
            {
                Log.Error("Audio mux failed; keeping the silent video", ex);
                try
                {
                    File.Copy(tempVideo, OutputPath, overwrite: true);
                    Warning = "Audio could not be added; the video was saved without sound.";
                }
                catch (Exception copyEx)
                {
                    keepTemp = true;   // the only copy of the recording: never delete it
                    Log.Error($"Could not save the silent video to {OutputPath}; the recording is kept at {tempVideo}", copyEx);
                    Warning = "The recording could not be saved. The raw video is kept at " + tempVideo;
                }
            }
            if (!keepTemp)
            {
                try { Directory.Delete(_audioDir, recursive: true); }
                catch (Exception ex) { Log.Warn("Could not remove the temp audio folder: " + ex.Message); }
            }
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
        // Rates were captured on the WasapiCapture objects; after Dispose they are gone, so the WAV headers carry them.
        string? sysPath = JoinSegments(_sysSegments, "system.wav");
        string? micPath = JoinSegments(_micSegments, "mic.wav");
        if (sysPath is not null) tracks.Add((sysPath, ReadWavRate(sysPath)));
        if (micPath is not null) tracks.Add((micPath, ReadWavRate(micPath)));
        if (tracks.Count == 0) return null;
        if (tracks.Count == 1) return tracks[0].Path;
        string mixedPath = Path.Combine(_audioDir!, "mixed.wav");
        MixTracksStreaming(tracks[0], tracks[1], mixedPath);
        return mixedPath;
    }

    /// <summary>
    /// Mixes two captured tracks (system + mic) into one WAV without ever holding
    /// a whole track in memory: A is read in chunks, B is resampled on the fly
    /// (Core PcmResampler — sample-identical to the whole-array resample) and
    /// metered out against A's position, so the pairing matches PcmMix.Mix over
    /// the full tracks while memory stays bounded by the chunk size.
    /// </summary>
    private static void MixTracksStreaming((string Path, int Rate) a, (string Path, int Rate) b, string mixedPath)
    {
        const int ChunkFrames = 8192;
        using var aReader = new WavReader(File.OpenRead(a.Path));
        using var bReader = new WavReader(File.OpenRead(b.Path));
        using var outFs = new FileStream(mixedPath, FileMode.Create, FileAccess.Write);
        using var writer = new WavWriter(outFs, aReader.SampleRate, aReader.Channels);
        var resampler = new PcmResampler(bReader.Channels, bReader.SampleRate, aReader.SampleRate);
        var aBuf = new short[ChunkFrames * aReader.Channels];
        var bBuf = new short[ChunkFrames * bReader.Channels];
        var bPending = new List<short>();
        int bOff = 0;
        bool bDone = false, bFinished = false;

        void TopUpB(int wantSamples)
        {
            while (bPending.Count - bOff < wantSamples && !bDone)
            {
                int n = bReader.Read(bBuf);
                if (n == 0) { bDone = true; break; }
                bPending.AddRange(resampler.Process(bBuf.AsSpan(0, n)));
            }
            if (bDone && !bFinished)
            {
                bFinished = true;
                bPending.AddRange(resampler.Finish());
            }
            if (bOff > ChunkFrames * 2) { bPending.RemoveRange(0, bOff); bOff = 0; }
        }

        while (true)
        {
            int aRead = aReader.Read(aBuf);
            if (aRead == 0)
            {
                // A is over: whatever resampled B remains plays out on its own.
                TopUpB(int.MaxValue);
                int left = bPending.Count - bOff;
                if (left > 0) writer.Write(bPending.GetRange(bOff, left).ToArray());
                break;
            }
            TopUpB(aRead);
            int take = Math.Min(aRead, bPending.Count - bOff);
            var bSlice = take > 0 ? bPending.GetRange(bOff, take).ToArray() : Array.Empty<short>();
            bOff += take;
            writer.Write(PcmMix.Mix(aBuf.AsSpan(0, aRead), bSlice));
        }
    }

    /// <summary>One segment plays as-is; several join (streaming) into a single track file. Missing or header-only segments do not count.</summary>
    private string? JoinSegments(List<string> segments, string joinedName)
    {
        var real = segments.Where(p => File.Exists(p) && new FileInfo(p).Length > 44).ToList();
        if (real.Count == 0) return null;
        if (real.Count == 1) return real[0];
        string joined = Path.Combine(_audioDir!, joinedName);
        var streams = real.Select(p => (Stream)File.OpenRead(p)).ToList();
        try
        {
            using var outFs = new FileStream(joined, FileMode.Create, FileAccess.Write);
            WavConcat.Concatenate(streams, outFs);
        }
        finally { foreach (var s in streams) s.Dispose(); }
        return joined;
    }

    public bool IsPaused => _clock.IsPaused;

    /// <summary>
    /// Pauses or resumes the recording. While paused, frames are dropped and
    /// every later timestamp shifts by the paused span; the audio captures close
    /// their segment files and reopen fresh ones on resume, so the pause is cut
    /// from the audio exactly as from the video.
    /// </summary>
    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_stopping || paused == _clock.IsPaused) return;
            var wall = SrtNow() - (_base ?? SrtNow());
            if (paused)
            {
                _clock.Pause(wall);
                foreach (var c in _captures) c.Dispose();
                _captures.Clear();
            }
            else
            {
                _clock.Resume(wall);
                _segment++;
                if (_audioDir is not null)
                {
                    if (_wantSystem && WasapiCapture.Start(loopback: true, Path.Combine(_audioDir, $"system-{_segment}.wav")) is { } sys)
                    { _captures.Add(sys); _sysSegments.Add(sys.Path); }
                    if (_wantMic && WasapiCapture.Start(loopback: false, Path.Combine(_audioDir, $"mic-{_segment}.wav")) is { } mic)
                    { _captures.Add(mic); _micSegments.Add(mic.Path); }
                }
            }
        }
    }

    /// <summary>Now, in the SystemRelativeTime (QPC) domain the frame timestamps use. Shared with the GIF recorder.</summary>
    internal static TimeSpan SrtNow() =>
        TimeSpan.FromTicks((long)(Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));

    private static int ReadWavRate(string path)
    {
        using var fs = File.OpenRead(path);
        fs.Position = 24;
        var b = new byte[4]; _ = fs.Read(b, 0, 4);
        return BitConverter.ToInt32(b, 0);
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
