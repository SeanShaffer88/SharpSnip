using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Snipit.Core;

/// <summary>
/// Hand-written single-image PDF (roadmap Phase 3: no package). One page, sized
/// to the image at 96 dpi, carrying the pixels losslessly as a FlateDecode
/// (zlib) RGB image stream. Output is deterministic: no dates, no file IDs.
/// Alpha is not carried — callers pass opaque RGB, one byte each for R, G, B.
/// </summary>
public static class PdfWriter
{
    public static byte[] Build(ReadOnlySpan<byte> rgb, int width, int height, string? title = null)
    {
        if (width is < 1 or > 32767) throw new ArgumentException("PDF image width must be 1..32767.", nameof(width));
        if (height is < 1 or > 32767) throw new ArgumentException("PDF image height must be 1..32767.", nameof(height));
        if (rgb.Length != (long)width * height * 3)
            throw new ArgumentException($"Expected {width}x{height}x3 = {(long)width * height * 3} RGB bytes, got {rgb.Length}.", nameof(rgb));

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(rgb);
            compressed = ms.ToArray();
        }

        string wPt = Points(width), hPt = Points(height);
        var objects = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {wPt} {hPt}] " +
                  "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>"),
            StreamObject($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} " +
                         $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressed.Length} >>", compressed),
            null!, // contents, built below (needs no forward references)
            Ascii($"<< /Title ({Escape(title ?? "Snipit capture")}) /Creator (Snipit) /Producer (Snipit) >>"),
        };
        objects[4] = StreamObject(null, Ascii($"q {wPt} 0 0 {hPt} 0 0 cm /Im0 Do Q"));

        using var outMs = new MemoryStream();
        void Write(ReadOnlySpan<byte> b) => outMs.Write(b);
        Write(Ascii("%PDF-1.4\n"));
        Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' }); // binary marker comment, per spec recommendation

        var offsets = new long[7];
        for (int n = 1; n <= 6; n++)
        {
            offsets[n] = outMs.Position;
            Write(Ascii($"{n} 0 obj\n"));
            Write(objects[n - 1]);
            Write(Ascii("\nendobj\n"));
        }

        long xrefAt = outMs.Position;
        var xref = new StringBuilder("xref\n0 7\n0000000000 65535 f \n");
        for (int n = 1; n <= 6; n++) xref.Append(offsets[n].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        Write(Ascii(xref.ToString()));
        Write(Ascii($"trailer\n<< /Size 7 /Root 1 0 R /Info 6 0 R >>\nstartxref\n{xrefAt}\n%%EOF"));
        return outMs.ToArray();
    }

    /// <summary>Pixels to PDF points at 96 dpi (× 72/96), at most two decimals, no trailing zeros.</summary>
    private static string Points(int px) =>
        (px * 0.75).ToString("0.00", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');

    private static byte[] StreamObject(string? dict, byte[] data)
    {
        using var ms = new MemoryStream();
        if (dict is not null) ms.Write(Ascii(dict + "\n"));
        else ms.Write(Ascii($"<< /Length {data.Length} >>\n"));
        ms.Write(Ascii("stream\n"));
        ms.Write(data);
        ms.Write(Ascii("\nendstream"));
        return ms.ToArray();
    }

    /// <summary>PDF literal-string escaping; non-printable/non-ASCII characters become '?'.</summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is < ' ' or > '~') sb.Append('?');
            else if (c is '\\' or '(' or ')') sb.Append('\\').Append(c);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);
}
