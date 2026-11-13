using System.Buffers.Binary;
using System.IO;

namespace SharpSnip.Core;

/// <summary>IEEE CRC-32 (the PNG chunk checksum), table-driven.</summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    /// <summary>Chunk checksum: the CRC over the chunk type bytes followed by the chunk data.</summary>
    public static uint ComputeChunk(string type, ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (char ch in type) c = Table[(c ^ (byte)ch) & 0xFF] ^ (c >> 8);
        foreach (byte b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}

public static class ApngPlan
{
    public const string FileName = "animation.apng";
}

/// <summary>Which animated still format a run should produce: "gif", "apng", or null for stills.</summary>
public static class AnimationPlan
{
    public static string? Kind(string? format) => ImageFormats.Normalize(format) switch
    {
        "gif" => "gif",
        "apng" => "apng",
        _ => null,
    };
}

/// <summary>
/// Streaming APNG writer. Frames arrive as complete PNG files (as a PNG
/// encoder produces them); the writer carries each frame's IHDR/PLTE/IDAT
/// chunks into an APNG stream, adding the acTL/fcTL/fdAT animation chunks
/// with correct sequence numbers and CRCs. The frame count lives in acTL
/// before the first frame, so it is written as a placeholder and patched
/// on Dispose (the stream must be seekable) — the same trick WavWriter
/// uses for its header sizes. The writer does not own the stream.
/// </summary>
public sealed class ApngWriter : IDisposable
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly Stream _stream;
    private readonly long _actlDataOffset;
    private readonly byte[] _ihdr;
    private uint _sequence;
    private int _frames;
    private bool _disposed;

    public ApngWriter(Stream stream, byte[] firstFramePng, ushort delayHundredths)
    {
        _stream = stream;
        var chunks = Parse(firstFramePng);
        _ihdr = chunks.First(c => c.Type == "IHDR").Data;
        _stream.Write(Signature);
        WriteChunk("IHDR", _ihdr);
        foreach (var c in chunks)
            if (c.Type == "PLTE") { WriteChunk("PLTE", c.Data); break; }
        // acTL placeholder: the real frame count is patched in on Dispose.
        _actlDataOffset = _stream.Position + 8; // skip length + type
        var actl = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(actl.AsSpan(4), 0u); // num_plays = 0 (loop forever)
        WriteChunk("acTL", actl);
        WriteFrame(chunks, delayHundredths, first: true);
    }

    public void AddFrame(byte[] framePng, ushort delayHundredths)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ApngWriter));
        var chunks = Parse(framePng);
        var ihdr = chunks.First(c => c.Type == "IHDR").Data;
        if (!ihdr.AsSpan().SequenceEqual(_ihdr))
            throw new InvalidDataException("APNG frame dimensions or format changed mid-stream.");
        WriteFrame(chunks, delayHundredths, first: false);
    }

    private void WriteFrame(List<(string Type, byte[] Data)> chunks, ushort delayHundredths, bool first)
    {
        int w = (int)BinaryPrimitives.ReadUInt32BigEndian(_ihdr.AsSpan(0));
        int h = (int)BinaryPrimitives.ReadUInt32BigEndian(_ihdr.AsSpan(4));
        var fctl = new byte[26];
        BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(0), _sequence++);
        BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(4), (uint)w);
        BinaryPrimitives.WriteUInt32BigEndian(fctl.AsSpan(8), (uint)h);
        // x/y offsets stay 0: full-canvas frames.
        BinaryPrimitives.WriteUInt16BigEndian(fctl.AsSpan(20), Math.Clamp(delayHundredths, GifPlan.MinDelayHundredths, GifPlan.MaxDelayHundredths));
        BinaryPrimitives.WriteUInt16BigEndian(fctl.AsSpan(22), 100);
        // dispose_op 0 (none), blend_op 0 (source) stay 0.
        WriteChunk("fcTL", fctl);
        foreach (var (type, data) in chunks)
        {
            if (type != "IDAT") continue;
            if (first)
            {
                WriteChunk("IDAT", data); // the first frame's IDAT is the default image too
            }
            else
            {
                var fdat = new byte[4 + data.Length];
                BinaryPrimitives.WriteUInt32BigEndian(fdat.AsSpan(0), _sequence++);
                data.CopyTo(fdat.AsSpan(4));
                WriteChunk("fdAT", fdat);
            }
        }
        _frames++;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        WriteChunk("IEND", Array.Empty<byte>());
        // Patch the acTL frame count (and its CRC, which covers the count).
        var actl = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(actl.AsSpan(0), (uint)_frames);
        _stream.Position = _actlDataOffset;
        _stream.Write(actl);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32.ComputeChunk("acTL", actl));
        _stream.Write(crc);
        _stream.Flush();
    }

    private void WriteChunk(string type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)data.Length);
        _stream.Write(word);
        foreach (char ch in type) _stream.WriteByte((byte)ch);
        _stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32.ComputeChunk(type, data));
        _stream.Write(word);
    }

    /// <summary>Splits a PNG file into its chunks (signature verified; CRCs of the source are trusted, IDAT data is carried opaquely).</summary>
    private static List<(string Type, byte[] Data)> Parse(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("Not a PNG file.");
        var chunks = new List<(string Type, byte[] Data)>();
        int pos = 8;
        while (pos + 12 <= png.Length)
        {
            int len = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(pos));
            if (len < 0 || pos + 12 + len > png.Length) throw new InvalidDataException("Truncated PNG chunk.");
            string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            chunks.Add((type, png.AsSpan(pos + 8, len).ToArray()));
            pos += 12 + len;
            if (type == "IEND") break;
        }
        if (!chunks.Any(c => c.Type == "IHDR") || !chunks.Any(c => c.Type == "IDAT"))
            throw new InvalidDataException("PNG is missing IHDR or IDAT.");
        return chunks;
    }
}
