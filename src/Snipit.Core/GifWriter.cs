using System.IO;

namespace Snipit.Core;

/// <summary>Rules for animated GIF output (interval captures in GIF format).</summary>
public static class GifPlan
{
    public const string FileName = "animation.gif";
    public const ushort MinDelayHundredths = 2;      // below this, viewers force ~100 ms anyway
    public const ushort MaxDelayHundredths = 65_535; // the GIF delay field is a ushort

    /// <summary>Frame delay for one interval step, in GIF hundredths of a second, clamped to the field.</summary>
    public static ushort DelayHundredths(int intervalSeconds) =>
        (ushort)Math.Clamp((long)intervalSeconds * 100, MinDelayHundredths, MaxDelayHundredths);

    public static bool IsAnimatedFormat(string? format) => ImageFormats.Normalize(format) == "gif";
}

/// <summary>
/// Minimal streaming GIF89a writer: a 256-colour global palette, a NETSCAPE2.0
/// looping block, and one full-canvas frame per <see cref="AddFrame"/> call.
/// Hand-written (no package); the LZW encoder follows the GIF89a spec, and the
/// caller supplies palette indices, so memory stays at one frame.
/// </summary>
public sealed class GifWriter : IDisposable
{
    private const int MinCodeSize = 8;
    private const int ClearCode = 1 << MinCodeSize; // 256
    private const int EoiCode = ClearCode + 1;      // 257
    private const int FirstFree = EoiCode + 1;      // 258
    private const int MaxCodes = 4096;

    private readonly Stream _stream;
    private readonly int _pixels;
    private bool _finished;

    public GifWriter(Stream stream, int width, int height, ReadOnlyMemory<byte> paletteRgb, ushort loopCount = 0)
    {
        if (width is < 1 or > 65_535) throw new ArgumentException("GIF width must be 1..65535.", nameof(width));
        if (height is < 1 or > 65_535) throw new ArgumentException("GIF height must be 1..65535.", nameof(height));
        if (paletteRgb.Length != 256 * 3) throw new ArgumentException("Palette must be exactly 256 RGB entries (768 bytes).", nameof(paletteRgb));
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Width = width;
        Height = height;
        _pixels = width * height;

        WriteHeader(paletteRgb.Span, loopCount);
    }

    public int Width { get; }
    public int Height { get; }

    private void WriteHeader(ReadOnlySpan<byte> palette, ushort loopCount)
    {
        _stream.Write("GIF89a"u8);
        WriteU16(Width);
        WriteU16(Height);
        _stream.WriteByte(0xF7); // global colour table, 8-bit colour resolution, 256 entries
        _stream.WriteByte(0);    // background colour index
        _stream.WriteByte(0);    // aspect ratio (square)
        _stream.Write(palette);
        // NETSCAPE2.0 looping extension (0 = loop forever).
        _stream.WriteByte(0x21);
        _stream.WriteByte(0xFF);
        _stream.WriteByte(0x0B);
        _stream.Write("NETSCAPE2.0"u8);
        _stream.WriteByte(3);
        _stream.WriteByte(1);
        WriteU16(loopCount);
        _stream.WriteByte(0);
    }

    /// <summary>Append one frame of palette indices (row-major, exactly width×height bytes).</summary>
    public void AddFrame(ReadOnlySpan<byte> indices, ushort delayHundredths)
    {
        if (_finished) throw new InvalidOperationException("The GIF is already finished.");
        if (indices.Length != _pixels) throw new ArgumentException($"Frame must be exactly {_pixels} palette indices.", nameof(indices));

        // Graphics Control Extension: no transparency, no disposal, this frame's delay.
        _stream.WriteByte(0x21);
        _stream.WriteByte(0xF9);
        _stream.WriteByte(4);
        _stream.WriteByte(0);
        WriteU16(delayHundredths);
        _stream.WriteByte(0);
        _stream.WriteByte(0);

        // Image Descriptor: full canvas at the origin, no local palette.
        _stream.WriteByte(0x2C);
        WriteU16(0);
        WriteU16(0);
        WriteU16(Width);
        WriteU16(Height);
        _stream.WriteByte(0);

        _stream.WriteByte(MinCodeSize);
        byte[] compressed = LzwCompress(indices);
        for (int pos = 0; pos < compressed.Length; pos += 255)
        {
            int n = Math.Min(255, compressed.Length - pos);
            _stream.WriteByte((byte)n);
            _stream.Write(compressed.AsSpan(pos, n));
        }
        _stream.WriteByte(0); // block terminator
    }

    /// <summary>GIF LZW: variable-length codes, LSB-first, dictionary cleared when full.</summary>
    private static byte[] LzwCompress(ReadOnlySpan<byte> indices)
    {
        var bytes = new List<byte>();
        int bitBuffer = 0, bitCount = 0;
        int codeSize = MinCodeSize + 1;
        var dict = new Dictionary<int, int>(); // (prefix code << 8 | next index) -> assigned code
        int nextCode = FirstFree;

        void Emit(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                bytes.Add((byte)bitBuffer);
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        Emit(ClearCode);
        int prefix = indices[0];
        for (int i = 1; i < indices.Length; i++)
        {
            int k = indices[i];
            int key = prefix << 8 | k;
            if (dict.TryGetValue(key, out int code))
            {
                prefix = code;
                continue;
            }
            Emit(prefix);
            if (nextCode == MaxCodes)
            {
                Emit(ClearCode);
                dict.Clear();
                nextCode = FirstFree;
                codeSize = MinCodeSize + 1;
            }
            else
            {
                dict[key] = nextCode++;
                if (nextCode - 1 == (1 << codeSize) && codeSize < 12) codeSize++;
            }
            prefix = k;
        }
        Emit(prefix);
        Emit(EoiCode);
        if (bitCount > 0) bytes.Add((byte)bitBuffer);
        return bytes.ToArray();
    }

    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        _stream.WriteByte(0x3B); // trailer
        _stream.Flush();
    }

    public void Dispose() => Finish();

    private void WriteU16(int v)
    {
        _stream.WriteByte((byte)v);
        _stream.WriteByte((byte)(v >> 8));
    }
}
