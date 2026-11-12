using System.Text;

namespace SharpSnip.Core;

/// <summary>Minimal single-page PDF 1.4 holding one JPEG (DCT) image. Page size = image size in points at 96 dpi → 72 dpi.</summary>
public static class PdfWriter
{
    public static byte[] SingleJpegPage(byte[] jpeg, int widthPx, int heightPx)
    {
        if (jpeg is null || jpeg.Length < 4) throw new ArgumentException("JPEG data required.", nameof(jpeg));
        if (widthPx < 1 || heightPx < 1) throw new ArgumentOutOfRangeException(nameof(widthPx));
        double wPt = Math.Round(widthPx * 0.75, 2), hPt = Math.Round(heightPx * 0.75, 2);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string content = string.Create(inv, $"q {wPt} 0 0 {hPt} 0 0 cm /Im0 Do Q");

        using var ms = new MemoryStream();
        var offsets = new List<long>();
        void Raw(string s) { var b = Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); }
        void Obj(string body) { offsets.Add(ms.Position); Raw($"{offsets.Count} 0 obj\n{body}\nendobj\n"); }

        Raw("%PDF-1.4\n");
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(string.Create(inv, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {wPt} {hPt}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>"));
        offsets.Add(ms.Position);
        Raw($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {widthPx} /Height {heightPx} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        ms.Write(jpeg, 0, jpeg.Length);
        Raw("\nendstream\nendobj\n");
        Obj($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");

        long xref = ms.Position;
        Raw($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (long o in offsets) Raw($"{o:D10} 00000 n \n");
        Raw($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }
}
