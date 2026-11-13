using System.Text;

namespace SharpSnip.Core;

/// <summary>
/// Streaming 16-bit PCM WAV writer over a seekable stream: the header is
/// written up front with placeholder sizes and patched on Dispose, so a
/// capture can run for hours without holding samples in memory.
/// </summary>
public sealed class WavWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly int _channels;
    private long _dataBytes;
    private bool _disposed;

    public WavWriter(Stream stream, int sampleRate, int channels)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(channels));
        _channels = channels;
        WriteHeader(sampleRate, channels, 0);
    }

    public void Write(ReadOnlySpan<short> samples)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WavWriter));
        if (samples.Length % _channels != 0) throw new ArgumentException("Sample count must align to whole frames.", nameof(samples));
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            bytes[i * 2] = (byte)(samples[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }
        _stream.Write(bytes, 0, bytes.Length);
        _dataBytes += bytes.Length;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Patch the two size fields (RIFF size at 4, data size at 40) now the total is known.
        if (_stream.CanSeek)
        {
            _stream.Position = 4;
            WriteU32((uint)(36 + _dataBytes));
            _stream.Position = 40;
            WriteU32((uint)_dataBytes);
        }
        _stream.Flush();
    }

    private void WriteHeader(int sampleRate, int channels, uint dataBytes)
    {
        var h = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
        h.Write(Encoding.ASCII.GetBytes("RIFF"));
        h.Write((uint)(36 + dataBytes));
        h.Write(Encoding.ASCII.GetBytes("WAVE"));
        h.Write(Encoding.ASCII.GetBytes("fmt "));
        h.Write(16u);
        h.Write((ushort)1); // PCM
        h.Write((ushort)channels);
        h.Write((uint)sampleRate);
        h.Write((uint)(sampleRate * channels * 2)); // byte rate
        h.Write((ushort)(channels * 2)); // block align
        h.Write((ushort)16); // bits per sample
        h.Write(Encoding.ASCII.GetBytes("data"));
        h.Write(dataBytes);
        h.Flush();
    }

    private void WriteU32(uint v)
    {
        _stream.WriteByte((byte)(v & 0xFF));
        _stream.WriteByte((byte)((v >> 8) & 0xFF));
        _stream.WriteByte((byte)((v >> 16) & 0xFF));
        _stream.WriteByte((byte)((v >> 24) & 0xFF));
    }
}
