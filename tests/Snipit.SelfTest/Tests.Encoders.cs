using Snipit.Core;

namespace Snipit.SelfTest;

// Encoders checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Encoders()
    {
        Console.WriteLine("GifWriter");
        // Test-side GIF LZW decoder (written from the GIF89a spec, independent of the encoder).
        static byte[] GifLzwDecode(byte[] packed, int minCodeSize)
        {
            var output = new List<byte>();
            var dict = new List<byte[]>();
            int clear = 1 << minCodeSize, eoi = clear + 1;
            void Reset() { dict.Clear(); for (int i = 0; i < clear; i++) dict.Add(new[] { (byte)i }); dict.Add(Array.Empty<byte>()); dict.Add(Array.Empty<byte>()); }
            Reset();
            int codeSize = minCodeSize + 1, bitPos = 0;
            byte[]? prev = null;
            while (true)
            {
                int code = 0;
                for (int b = 0; b < codeSize; b++)
                {
                    int byteIdx = (bitPos + b) / 8;
                    if (byteIdx >= packed.Length) return output.ToArray();
                    if ((packed[byteIdx] >> ((bitPos + b) % 8) & 1) != 0) code |= 1 << b;
                }
                bitPos += codeSize;
                if (code == clear) { Reset(); codeSize = minCodeSize + 1; prev = null; continue; }
                if (code == eoi) break;
                byte[] entry;
                if (code < dict.Count && dict[code].Length > 0) entry = dict[code];
                else if (code == dict.Count && prev is not null) entry = prev.Concat(new[] { prev[0] }).ToArray();
                else break;
                output.AddRange(entry);
                if (prev is not null)
                {
                    dict.Add(prev.Concat(new[] { entry[0] }).ToArray());
                    if (dict.Count == (1 << codeSize) && codeSize < 12) codeSize++;
                }
                prev = entry;
            }
            return output.ToArray();
        }

        (List<(ushort Delay, byte[] Pixels)> Frames, ushort Loop, int W, int H) ParseGif(byte[] gif)
        {
            Check(gif.AsSpan(0, 6).SequenceEqual("GIF89a"u8), "gif header is GIF89a");
            int w = gif[6] | gif[7] << 8, h = gif[8] | gif[9] << 8;
            Check((gif[10] & 0x80) != 0 && (gif[10] & 0x07) == 7, "global colour table, 256 entries");
            int pos = 13 + 768;
            var frames = new List<(ushort, byte[])>();
            ushort loop = 0xFFFF;
            while (pos < gif.Length)
            {
                byte tag = gif[pos];
                if (tag == 0x3B) { pos++; break; }
                if (tag == 0x21 && gif[pos + 1] == 0xFF)
                {
                    int appLen = gif[pos + 2];
                    string app = System.Text.Encoding.ASCII.GetString(gif, pos + 3, appLen);
                    Check(app == "NETSCAPE2.0", "looping extension present");
                    int p = pos + 3 + appLen;
                    while (gif[p] != 0) { if (gif[p] == 3 && gif[p + 1] == 1) loop = (ushort)(gif[p + 2] | gif[p + 3] << 8); p += 1 + gif[p]; }
                    pos = p + 1; continue;
                }
                if (tag == 0x21 && gif[pos + 1] == 0xF9)
                {
                    ushort delay = (ushort)(gif[pos + 4] | gif[pos + 5] << 8);
                    int idPos = pos + 8;
                    Check(gif[idPos] == 0x2C, "image descriptor follows GCE");
                    int minCode = gif[idPos + 10];
                    var packed = new List<byte>();
                    int p = idPos + 11;
                    while (gif[p] != 0) { packed.AddRange(gif.AsSpan(p + 1, gif[p])); p += 1 + gif[p]; } // sub-block length is a byte, so ≤255 by construction
                    frames.Add((delay, GifLzwDecode(packed.ToArray(), minCode)));
                    pos = p + 1; continue;
                }
                Check(false, $"unexpected gif block 0x{tag:X2} at {pos}");
                break;
            }
            Check(pos == gif.Length && gif[^1] == 0x3B, "gif ends with trailer");
            return (frames, loop, w, h);
        }

        byte[] palette = new byte[768];
        for (int i = 0; i < palette.Length; i++) palette[i] = (byte)(255 - i % 256);
        byte[] MakeGif(int w, int h, params (byte[] Indices, ushort Delay)[] frames)
        {
            var ms = new MemoryStream();
            using (var gw = new GifWriter(ms, w, h, palette, loopCount: 0))
                foreach (var (indices, delay) in frames) gw.AddFrame(indices, delay);
            return ms.ToArray();
        }

        var onePx = MakeGif(1, 1, (new byte[] { 42 }, 100));
        var parsed1 = ParseGif(onePx);
        Check(parsed1.W == 1 && parsed1.H == 1 && parsed1.Loop == 0, "1x1 gif geometry, loops forever");
        Check(parsed1.Frames.Count == 1 && parsed1.Frames[0].Delay == 100 && parsed1.Frames[0].Pixels.SequenceEqual(new byte[] { 42 }), "1x1 frame round-trips");

        var small = new byte[64 * 64];
        Array.Fill(small, (byte)7);
        var rnd = new Random(1234);
        var noisy = new byte[64 * 64];
        rnd.NextBytes(noisy);
        var parsed2 = ParseGif(MakeGif(64, 64, (small, GifPlan.DelayHundredths(3)), ((byte[])noisy.Clone(), 250)));
        Check(parsed2.Frames.Count == 2 && parsed2.Frames[0].Pixels.SequenceEqual(small), "solid frame round-trips");
        Check(parsed2.Frames[1].Pixels.SequenceEqual(noisy) && parsed2.Frames[1].Delay == 250, "noisy frame round-trips with its delay");
        var odd = new byte[37 * 11];
        rnd.NextBytes(odd);
        Check(ParseGif(MakeGif(37, 11, (odd, 100))).Frames[0].Pixels.SequenceEqual(odd), "odd-sized frame round-trips");

        var big = new byte[256 * 256];
        rnd.NextBytes(big);
        var parsed3 = ParseGif(MakeGif(256, 256, (big, 100)));
        Check(parsed3.Frames[0].Pixels.SequenceEqual(big), "large random frame round-trips (dictionary growth + clear codes)");
        Check(MakeGif(8, 8, (new byte[64], 100)).SequenceEqual(MakeGif(8, 8, (new byte[64], 100))), "encoding is deterministic");

        bool threw = false;
        try { var ms = new MemoryStream(); using var gw = new GifWriter(ms, 4, 4, palette); gw.AddFrame(new byte[15], 100); } catch (ArgumentException) { threw = true; }
        Check(threw, "wrong pixel count rejected");
        threw = false;
        try { var ms = new MemoryStream(); using var gw = new GifWriter(ms, 4, 4, new byte[100]); } catch (ArgumentException) { threw = true; }
        Check(threw, "short palette rejected");
        threw = false;
        try { var ms = new MemoryStream(); using var gw = new GifWriter(ms, 0, 4, palette); } catch (ArgumentException) { threw = true; }
        Check(threw, "zero width rejected");

        Console.WriteLine("LastCaptureRect");
        Check(new Preferences { LastCaptureRect = new[] { -1920, 0, 1920, 1080 } }.Sanitize().LastCaptureRect!.SequenceEqual(new[] { -1920, 0, 1920, 1080 }), "valid last rect kept (negative origin ok)");
        Check(new Preferences { LastCaptureRect = new[] { 0, 0, 0, 100 } }.Sanitize().LastCaptureRect is null, "zero-size last rect dropped");
        Check(new Preferences { LastCaptureRect = new[] { 1, 2, 3 } }.Sanitize().LastCaptureRect is null, "wrong-length last rect dropped");
        Check(new Preferences { LastCaptureRect = new[] { 0, 0, 99999, 10 } }.Sanitize().LastCaptureRect is null, "absurd last rect dropped");
        string lrFile = Path.Combine(Path.GetTempPath(), "snipit-lr-" + Guid.NewGuid().ToString("N") + ".json");
        PrefsStore.Save(lrFile, new Preferences { LastCaptureRect = new[] { 10, 20, 300, 200 } });
        Check(PrefsStore.Load(lrFile, out _).LastCaptureRect!.SequenceEqual(new[] { 10, 20, 300, 200 }), "last rect survives save/load");
        File.Delete(lrFile);

        Console.WriteLine("UniquePath");
        var existing = new HashSet<string> { @"C:\shots\a.png", @"C:\shots\a-2.png" };
        Check(OutputNaming.UniquePath(@"C:\shots\b.png", existing.Contains) == @"C:\shots\b.png", "free name unchanged");
        Check(OutputNaming.UniquePath(@"C:\shots\a.png", existing.Contains) == @"C:\shots\a-3.png", "collisions bump -2, -3 until free");
        Check(OutputNaming.UniquePath("a.png", _ => false) == "a.png", "bare name unchanged when free");

        Console.WriteLine("CapturePixels");
        {
            // 4x2 canvas (BGRA bytes). No gaps: only the alpha byte of each pixel changes.
            byte[] px = { 1, 2, 3, 0, 5, 6, 7, 0, 9, 10, 11, 0, 13, 14, 15, 0, 17, 18, 19, 0, 21, 22, 23, 0, 25, 26, 27, 0, 29, 30, 31, 0 };
            var copy = (byte[])px.Clone();
            CapturePixels.ApplyAlpha(copy, 4, new[] { new Placement(m1, new RectPx(0, 0, 4, 2), 0, 0) }, gaps: false, matteArgb: 0);
            bool ok = true;
            for (int i = 0; i < 8; i++) ok &= copy[i * 4] == px[i * 4] && copy[i * 4 + 1] == px[i * 4 + 1] && copy[i * 4 + 2] == px[i * 4 + 2] && copy[i * 4 + 3] == 0xFF;
            Check(ok, "no gaps: rgb untouched, alpha forced opaque");

            // Gaps: left half covered by a placement, right half becomes the matte (ARGB 0xFF112233 -> BGRA 33 22 11 FF).
            byte[] g = new byte[32];
            for (int i = 0; i < g.Length; i++) g[i] = 99;
            var leftHalf = new Placement(m1, new RectPx(0, 0, 2, 2), 0, 0);
            CapturePixels.ApplyAlpha(g, 4, new[] { leftHalf }, gaps: true, matteArgb: 0xFF112233);
            bool gOk = true;
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 4; col++)
                {
                    int o = (row * 4 + col) * 4;
                    if (col < 2) gOk &= g[o] == 99 && g[o + 3] == 0xFF;                       // covered: source rgb kept, alpha opaque
                    else gOk &= g[o] == 0x33 && g[o + 1] == 0x22 && g[o + 2] == 0x11 && g[o + 3] == 0xFF; // gap: matte
                }
            Check(gOk, "gaps: covered pixels opaque, uncovered pixels are the matte");
            Check(CapturePixels.MatteBytes(0x80112233).SequenceEqual(new byte[] { 0x33, 0x22, 0x11, 0x80 }), "matte argb -> bgra byte order");
        }

        Console.WriteLine("LogRetention");
        Check(LogRetention.IsExpired("sharpsnip-20260101.log", new DateOnly(2026, 2, 15), 30), "45-day-old log expired at 30-day retention");
        Check(!LogRetention.IsExpired("sharpsnip-20260201.log", new DateOnly(2026, 2, 15), 30), "14-day-old log kept");
        Check(!LogRetention.IsExpired("sharpsnip-20260215.log", new DateOnly(2026, 2, 15), 30), "today's log kept");
        Check(!LogRetention.IsExpired("notes.txt", new DateOnly(2026, 2, 15), 30) && !LogRetention.IsExpired("sharpsnip-junk.log", new DateOnly(2026, 2, 15), 30), "foreign/unparseable files never expire");

        Console.WriteLine("PdfWriter");
        {
            var rgb = new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0, 0, 0, 0, 255, 0, 255, 255, 255, 255, 128, 128, 128 }; // 4x2
            byte[] pdf = PdfWriter.Build(rgb, 4, 2, "Test (one) \\ capture");
            string text = System.Text.Encoding.ASCII.GetString(pdf);
            Check(text.StartsWith("%PDF-1.4"), "pdf header");
            Check(text.TrimEnd().EndsWith("%%EOF"), "pdf ends with EOF marker");
            Check(text.Contains("/MediaBox [0 0 3 1.5]"), "page size is pixels at 96 dpi (4x2 -> 3x1.5 pt)");
            Check(text.Contains("/Width 4 /Height 2") && text.Contains("/ColorSpace /DeviceRGB") && text.Contains("/Filter /FlateDecode"), "image dictionary describes the pixels");
            Check(text.Contains("(Test \\(one\\) \\\\ capture)"), "title escaped for a PDF literal string");
            Check(PdfWriter.Build(rgb, 4, 2, "x").SequenceEqual(PdfWriter.Build(rgb, 4, 2, "x")), "pdf output is deterministic");

            // xref table: every recorded offset must point exactly at its "N 0 obj".
            int xrefAt = text.IndexOf("\nxref\n", StringComparison.Ordinal) + 1;
            var lines = text[xrefAt..].Split('\n');
            int count = int.Parse(lines[1].Split(' ')[1]);
            bool xrefOk = count == 7;
            for (int n = 1; n < count && xrefOk; n++)
            {
                int off = int.Parse(lines[2 + n].AsSpan(0, 10));
                xrefOk = text.AsSpan(off).StartsWith($"{n} 0 obj");
            }
            Check(xrefOk, "xref offsets point at their objects");

            // The image stream must decompress (zlib/Flate) back to the exact source pixels.
            int sAt = text.IndexOf("stream\n", StringComparison.Ordinal) + "stream\n".Length;
            int eAt = text.IndexOf("\nendstream", sAt, StringComparison.Ordinal);
            var comp = new MemoryStream(pdf, sAt, eAt - sAt);
            using var zlib = new System.IO.Compression.ZLibStream(comp, System.IO.Compression.CompressionMode.Decompress);
            var back = new MemoryStream();
            zlib.CopyTo(back);
            Check(back.ToArray().SequenceEqual(rgb), "image stream flate-round-trips to the source pixels");
            var lenAt = text.IndexOf("/Length ", StringComparison.Ordinal);
            int declared = int.Parse(text.AsSpan(lenAt + 8, text.IndexOf(' ', lenAt + 8) - (lenAt + 8)));
            Check(declared == eAt - sAt, "declared stream length matches the stream");

            Check(PdfWriter.Build(new byte[3], 1, 1).AsSpan().ToArray() is { Length: > 0 }, "1x1 pdf builds");
            bool pdfThrew = false;
            try { PdfWriter.Build(new byte[5], 2, 2); } catch (ArgumentException) { pdfThrew = true; }
            Check(pdfThrew, "pixel-count mismatch rejected");
            pdfThrew = false;
            try { PdfWriter.Build(new byte[12], 0, 2); } catch (ArgumentException) { pdfThrew = true; }
            Check(pdfThrew, "zero width rejected");
            pdfThrew = false;
            try { PdfWriter.Build(new byte[12], 2, 2, "t€st"); } catch (ArgumentException) { pdfThrew = true; }
            Check(!pdfThrew && System.Text.Encoding.ASCII.GetString(PdfWriter.Build(new byte[12], 2, 2, "t€st")).Contains("(t?st)"), "non-ascii title characters replaced, not thrown");
            Check(new Preferences { ImageFormat = "pdf" }.Sanitize().Extension == "pdf" && ImageFormats.FlattensAlpha("pdf"), "pdf is a save format and flattens alpha");
        }

        Console.WriteLine("ScrollStitch");
        {
            Check(ScrollStitch.Plan(100, 100).SequenceEqual(new[] { 0 }), "plan: content fits viewport -> single strip");
            Check(ScrollStitch.Plan(100, 90).SequenceEqual(new[] { 0 }), "plan: short content -> single strip");
            Check(ScrollStitch.Plan(100, 250, 20).SequenceEqual(new[] { 0, 80, 150 }), "plan: steps of viewport-overlap, final strip anchored to the end");
            Check(ScrollStitch.Plan(100, 180, 20).SequenceEqual(new[] { 0, 80 }), "plan: final anchor replaces an overshooting step");
            var tiny = ScrollStitch.Plan(10, 25, 64);
            Check(tiny[0] == 0 && tiny[^1] == 15 && tiny.Zip(tiny.Skip(1), (a, b) => b - a).All(d => d >= 1), "plan: an oversized overlap still steps at least 1px and anchors the end");
            bool sThrew = false;
            try { ScrollStitch.Plan(0, 100); } catch (ArgumentException) { sThrew = true; }
            Check(sThrew, "plan: zero viewport rejected");

            // Compose: source image where every byte encodes its (row, col) so provenance is checkable.
            byte[] Src(int w, int h) { var b = new byte[w * h * 4]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int i = (y * w + x) * 4; b[i] = (byte)x; b[i + 1] = (byte)y; b[i + 2] = (byte)(x + y); b[i + 3] = 255; } return b; }
            byte[] Slice(byte[] img, int w, int y0, int h) => img.AsSpan(y0 * w * 4, h * w * 4).ToArray();
            var full = Src(3, 10);
            var composed = ScrollStitch.Compose(3, 10, new[]
            {
                new ScrollStrip(0, 6, Slice(full, 3, 0, 6)),
                new ScrollStrip(4, 6, Slice(full, 3, 4, 6)),
            });
            Check(composed.SequenceEqual(full), "compose: overlapping strips reproduce the source image exactly");
            var clamped = ScrollStitch.Compose(3, 8, new[]
            {
                new ScrollStrip(0, 6, Slice(full, 3, 0, 6)),
                new ScrollStrip(4, 6, Slice(full, 3, 4, 6)),
            });
            Check(clamped.SequenceEqual(Slice(full, 3, 0, 8)), "compose: a strip past the content end is clamped");
            sThrew = false;
            try { ScrollStitch.Compose(3, 10, new[] { new ScrollStrip(2, 4, Slice(full, 3, 2, 4)) }); } catch (ArgumentException) { sThrew = true; }
            Check(sThrew, "compose: first strip must start at 0");
            sThrew = false;
            try { ScrollStitch.Compose(3, 10, new[] { new ScrollStrip(0, 4, Slice(full, 3, 0, 4)), new ScrollStrip(7, 3, Slice(full, 3, 7, 3)) }); } catch (ArgumentException) { sThrew = true; }
            Check(sThrew, "compose: a gap between strips is rejected");
            sThrew = false;
            try { ScrollStitch.Compose(3, 10, new[] { new ScrollStrip(0, 4, new byte[10]) }); } catch (ArgumentException) { sThrew = true; }
            Check(sThrew, "compose: wrong strip size is rejected");

            var acc = Slice(full, 3, 0, 6);
            Check(ScrollStitch.FindOverlap(acc, 6, Slice(full, 3, 4, 6), 6, 3) == 2, "overlap: bottom of accumulated matches top of next strip");
            Check(ScrollStitch.FindOverlap(acc, 6, Slice(full, 3, 6, 4), 4, 3) == 0, "overlap: abutting strips have zero overlap");
            Check(ScrollStitch.FindOverlap(acc, 6, Slice(full, 3, 0, 6), 6, 3) == 6, "overlap: a repeated strip reports full overlap (zero new rows) — the caller's stop signal");
        }

        Console.WriteLine("OcrText");
        {
            OcrWord W(string t, int x, int y, int w = 30, int h = 10) => new(t, new RectPx(x, y, w, h));
            Check(OcrText.Assemble(Array.Empty<OcrWord>()) == "", "ocr: no words -> empty text");
            Check(OcrText.Assemble(new[] { W("hello", 0, 0) }) == "hello", "ocr: single word");
            Check(OcrText.Assemble(new[] { W("world", 40, 0), W("hello", 0, 0) }) == "hello world", "ocr: words ordered left to right within a line");
            Check(OcrText.Assemble(new[] { W("second", 0, 30), W("line", 40, 30), W("first", 0, 0), W("row", 40, 1) }) == "first row\nsecond line",
                "ocr: lines ordered top to bottom, small y jitter stays on the line");
            Check(OcrText.Assemble(new[] { W("a", 0, 0), W("b", 0, 14, 30, 10) }) == "a\nb", "ocr: a gap beyond half the line height starts a new line");
            Check(OcrText.Assemble(new[] { W("  ", 0, 0), W("kept", 40, 0), W("", 80, 0) }) == "kept", "ocr: blank words dropped");
            Check(OcrText.Assemble(new[] { W("one", 0, 0), W("two", 0, 100), W("three", 0, 200) }) == "one\ntwo\nthree", "ocr: three separated lines");
        }

    }
}
