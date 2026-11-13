using System.Runtime.InteropServices;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// WASAPI shared-mode capture of one default endpoint — the render endpoint
/// with the loopback flag for system sound, or the capture endpoint for the
/// microphone — written straight to a WAV file (stereo 16-bit at the device's
/// mix rate) on a background thread. Classic COM interop only. Start returns
/// null when the endpoint does not exist or cannot be opened, and the caller
/// simply records without that source.
/// </summary>
internal sealed class WasapiCapture : IDisposable
{
    private readonly IAudioClient _client;
    private readonly IAudioCaptureClient _capture;
    private readonly WavWriter _writer;
    private readonly FileStream _file;
    private readonly int _channels, _bits, _formatTag;
    private readonly Thread _thread;
    private volatile bool _stop;
    private bool _disposed;

    public int SampleRate { get; }
    public string Path { get; }

    private WasapiCapture(IAudioClient client, IAudioCaptureClient capture, WavWriter writer, FileStream file,
        int sampleRate, int channels, int bits, int formatTag, string path)
    {
        _client = client; _capture = capture; _writer = writer; _file = file;
        SampleRate = sampleRate; _channels = channels; _bits = bits; _formatTag = formatTag; Path = path;
        _thread = new Thread(Run) { IsBackground = true, Name = "SharpSnip audio capture" };
    }

    public static WasapiCapture? Start(bool loopback, string wavPath)
    {
        FileStream? file = null;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(loopback ? 0 : 1, 0, out var device) < 0 || device is null)
                return null;
            var clientIid = typeof(IAudioClient).GUID;
            if (device.Activate(ref clientIid, CLSCTX_ALL, IntPtr.Zero, out object clientObj) < 0 || clientObj is not IAudioClient client)
                return null;
            if (client.GetMixFormat(out IntPtr fmtPtr) < 0 || fmtPtr == IntPtr.Zero) return null;
            var fmt = Marshal.PtrToStructure<WAVEFORMATEX>(fmtPtr);
            int tag = fmt.wFormatTag;
            if (tag == WAVE_FORMAT_EXTENSIBLE && fmt.cbSize >= 24)
                tag = Marshal.ReadInt32(fmtPtr, 24); // the SubFormat GUID's first field carries the real tag
            uint flags = loopback ? AUDCLNT_STREAMFLAGS_LOOPBACK : 0;
            int initHr;
            try { initHr = client.Initialize(AUDCLNT_SHAREMODE_SHARED, flags, 10_000_000, 0, fmtPtr, IntPtr.Zero); }
            finally { Marshal.FreeCoTaskMem(fmtPtr); }
            if (tag is not (WAVE_FORMAT_PCM or WAVE_FORMAT_IEEE_FLOAT) || initHr < 0) return null;
            var captureIid = typeof(IAudioCaptureClient).GUID;
            if (client.GetService(ref captureIid, out object captureObj) < 0 || captureObj is not IAudioCaptureClient capture)
                return null;
            if (client.Start() < 0) return null;

            file = new FileStream(wavPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var writer = new WavWriter(file, (int)fmt.nSamplesPerSec, 2);
            var cap = new WasapiCapture(client, capture, writer, file, (int)fmt.nSamplesPerSec, fmt.nChannels, fmt.wBitsPerSample, tag, wavPath);
            cap._thread.Start();
            return cap;
        }
        catch { file?.Dispose(); return null; }
    }

    private void Run()
    {
        var scratch = new short[48000 * 2]; // up to a second of stereo per packet batch; regrown if a packet is bigger
        try
        {
            while (!_stop)
            {
                if (_capture.GetNextPacketSize(out uint packet) < 0) break;
                if (packet == 0) { Thread.Sleep(5); continue; }
                while (packet > 0 && !_stop)
                {
                    if (_capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out _) < 0) return;
                    try
                    {
                        int needed = (int)frames * 2;
                        if (scratch.Length < needed) scratch = new short[needed];
                        bool silent = (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0;
                        Convert(data, (int)frames, silent, scratch);
                        _writer.Write(scratch.AsSpan(0, needed));
                    }
                    finally { _capture.ReleaseBuffer(frames); }
                    if (_capture.GetNextPacketSize(out packet) < 0) return;
                }
            }
        }
        catch { /* the file keeps whatever was written; the recorder logs the gap */ }
    }

    /// <summary>Converts one packet of the device mix format into stereo 16-bit PCM.</summary>
    private void Convert(IntPtr data, int frames, bool silent, short[] dst)
    {
        if (silent) { Array.Clear(dst, 0, frames * 2); return; }
        for (int f = 0; f < frames; f++)
        {
            for (int c = 0; c < 2; c++)
            {
                int srcChannel = _channels == 1 ? 0 : c;
                long offset = ((long)f * _channels + srcChannel);
                dst[f * 2 + c] = _formatTag switch
                {
                    WAVE_FORMAT_IEEE_FLOAT => PcmMix.FloatToPcm(BitConverter.Int32BitsToSingle(Marshal.ReadInt32(data + (nint)(offset * 4)))),
                    _ when _bits == 32 => (short)(Marshal.ReadInt32(data + (nint)(offset * 4)) >> 16),
                    _ when _bits == 16 => Marshal.ReadInt16(data + (nint)(offset * 2)),
                    _ => 0,
                };
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop = true;
        _thread.Join(TimeSpan.FromSeconds(2));
        try { _client.Stop(); } catch { }
        _writer.Dispose();
        _file.Dispose();
    }

    // ---- WASAPI COM ---------------------------------------------------------------

    private const int CLSCTX_ALL = 0x17;
    private const int AUDCLNT_SHAREMODE_SHARED = 0;
    private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    private const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;
    private const int WAVE_FORMAT_PCM = 1, WAVE_FORMAT_IEEE_FLOAT = 3, WAVE_FORMAT_EXTENSIBLE = 0xFFFE;

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag, nChannels;
        public uint nSamplesPerSec, nAvgBytesPerSec;
        public ushort nBlockAlign, wBitsPerSample, cbSize;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out object iface);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, uint periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid iid, out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
