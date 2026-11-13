namespace Snipit.Core;

/// <summary>
/// Streaming reader for the canonical 16-bit PCM WAV files <see cref="WavWriter"/>
/// produces: walks the RIFF chunks to the data block, then decodes little-endian
/// samples on demand, so hour-long recordings never sit in memory whole.
/// </summary>
public sealed class WavReader : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private long _remainingBytes;

    public int SampleRate { get; }
    public int Channels { get; }
    public long TotalFrames { get; }

    public WavReader(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        Span<byte> header = stackalloc byte[12];
        ReadExact(header);
        if (header[0] != (byte)'R' || header[1] != (byte)'I' || header[2] != (byte)'F' || header[3] != (byte)'F'
            || header[8] != (byte)'W' || header[9] != (byte)'A' || header[10] != (byte)'V' || header[11] != (byte)'E')
            throw new InvalidDataException("Not a WAV file.");
        int rate = 0, channels = 0; long dataBytes = -1;
        Span<byte> chunk = stackalloc byte[8];
        Span<byte> fmt = stackalloc byte[16];
        while (dataBytes < 0)
        {
            if (_stream.Read(chunk) < 8) throw new InvalidDataException("Truncated WAV file.");
            long size = BitConverter.ToUInt32(chunk.Slice(4, 4));
            string id = System.Text.Encoding.ASCII.GetString(chunk.Slice(0, 4));
            if (id == "fmt ")
            {
                ReadExact(fmt);
                int tag = BitConverter.ToUInt16(fmt);
                channels = BitConverter.ToUInt16(fmt.Slice(2, 2));
                rate = BitConverter.ToInt32(fmt.Slice(4, 4));
                int bits = BitConverter.ToUInt16(fmt.Slice(14, 2));
                if (tag != 1 || bits != 16) throw new InvalidDataException("Only 16-bit PCM WAV is supported.");
                Skip(size - 16 + (size & 1));
            }
            else if (id == "data") { dataBytes = size; }
            else Skip(size + (size & 1));
        }
        SampleRate = rate; Channels = channels;
        _remainingBytes = dataBytes;
        TotalFrames = channels > 0 ? dataBytes / 2 / channels : 0;
    }

    /// <summary>Reads up to buffer.Length samples; returns how many were read (0 at the end of the data).</summary>
    public int Read(Span<short> buffer)
    {
        int want = (int)Math.Min((long)buffer.Length * 2, _remainingBytes);
        if (want <= 0) return 0;
        var bytes = new byte[want];
        int got = 0;
        while (got < want)
        {
            int n = _stream.Read(bytes, got, want - got);
            if (n == 0) break;
            got += n;
        }
        _remainingBytes -= got;
        int samples = got / 2;
        for (int i = 0; i < samples; i++) buffer[i] = BitConverter.ToInt16(bytes, i * 2);
        return samples;
    }

    private void ReadExact(Span<byte> buffer)
    {
        int got = 0;
        while (got < buffer.Length)
        {
            int n = _stream.Read(buffer.Slice(got));
            if (n == 0) throw new InvalidDataException("Truncated WAV file.");
            got += n;
        }
    }

    private void Skip(long count)
    {
        if (count <= 0) return;
        if (_stream.CanSeek) { _stream.Seek(count, SeekOrigin.Current); return; }
        var scratch = new byte[Math.Min(count, 8192)];
        long left = count;
        while (left > 0)
        {
            int n = _stream.Read(scratch, 0, (int)Math.Min(left, scratch.Length));
            if (n == 0) throw new InvalidDataException("Truncated WAV file.");
            left -= n;
        }
    }

    public void Dispose()
    {
        if (!_leaveOpen) _stream.Dispose();
    }
}
