using Snipit.Core;

namespace Snipit.SelfTest;

// Imaging checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Imaging()
    {
        Console.WriteLine("Redactor");
        {
            OcrWord W(string t, int x, int y, int w = 30, int h = 10) => new(t, new RectPx(x, y, w, h));
            var hits = Redactor.Find(new[] { W("Contact", 0, 0, 45), W("john@example.com", 50, 0, 110), W("today", 170, 0, 35) });
            Check(hits.Count == 1 && hits[0].Kind == "email" && hits[0].Rect == new RectPx(49, -1, 112, 12), "redact: email in a line -> its word box, inflated 1px");
            hits = Redactor.Find(new[] { W("john.doe", 0, 0, 55), W("@example.com", 60, 0, 80) });
            Check(hits.Count == 1 && hits[0].Rect == new RectPx(-1, -1, 142, 12), "redact: email split across words -> union box");
            hits = Redactor.Find(new[] { W("see", 0, 0, 20), W("https://example.com/x.", 25, 0, 130) });
            Check(hits.Count == 1 && hits[0].Kind == "url", "redact: url detected (trailing punctuation tolerated)");
            hits = Redactor.Find(new[] { W("4242", 0, 0, 30), W("4242", 35, 0, 30), W("4242", 70, 0, 30), W("4242", 105, 0, 30) });
            Check(hits.Count == 1 && hits[0].Kind == "card" && hits[0].Rect == new RectPx(-1, -1, 137, 12), "redact: luhn-valid card across four words -> one union box");
            Check(Redactor.Find(new[] { W("4242", 0, 0, 30), W("4242", 35, 0, 30), W("4242", 70, 0, 30), W("4241", 105, 0, 30) }).Count == 0, "redact: luhn-invalid number is not a card");
            Check(Redactor.Find(new[] { W("10.0.0.25", 0, 0, 60) }).Count == 1, "redact: ipv4 detected");
            Check(Redactor.Find(new[] { W("999.1.1.1", 0, 0, 60) }).Count == 0, "redact: out-of-range ipv4 rejected");
            hits = Redactor.Find(new[] { W("(555)", 0, 0, 35), W("123-4567", 40, 0, 60) });
            Check(hits.Count == 1 && hits[0].Kind == "phone" && hits[0].Rect == new RectPx(-1, -1, 102, 12), "redact: phone across words -> union box");
            Check(Redactor.Find(new[] { W("sk-9f8e7d6c5b4a39281706fedcba", 0, 0, 200) }).Count == 1, "redact: long mixed token (api-key shape) detected");
            Check(Redactor.Find(new[] { W("internationalisation", 0, 0, 130) }).Count == 0, "redact: a long plain word is not a secret");
            Check(Redactor.Find(new[] { W("The", 0, 0, 20), W("quick", 25, 0, 35), W("brown", 65, 0, 35), W("fox", 105, 0, 20) }).Count == 0, "redact: ordinary sentence -> no hits");
            hits = Redactor.Find(new[]
            {
                W("mail", 0, 0, 30), W("john@example.com", 35, 0, 110),
                W("or", 0, 30, 15), W("https://example.com/?u=john@example.com", 20, 30, 260),
            });
            Check(hits.Count == 2 && hits[0].Kind == "email" && hits[1].Kind == "url", "redact: email and url on separate lines, no double-claim inside the url");
            Check(Redactor.Find(Array.Empty<OcrWord>()).Count == 0, "redact: no words -> no hits");
        }

        Console.WriteLine("PixelAdjust");
        {
            bool Throws(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }
            byte[] Ids(params int[] ids) { var b = new byte[ids.Length * 4]; for (int i = 0; i < ids.Length; i++) { b[i * 4] = b[i * 4 + 1] = b[i * 4 + 2] = (byte)ids[i]; b[i * 4 + 3] = 255; } return b; }
            bool Same(byte[] a, byte[] b) => a.SequenceEqual(b);

            var inv = new byte[] { 10, 20, 30, 40 };
            PixelAdjust.Invert(inv);
            Check(Same(inv, new byte[] { 245, 235, 225, 40 }), "invert: channels flip, alpha untouched");
            PixelAdjust.Invert(inv);
            Check(Same(inv, new byte[] { 10, 20, 30, 40 }), "invert: twice is identity");

            var grey = new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 200 };
            PixelAdjust.Grayscale(grey);
            Check(grey[0] == 76 && grey[4] == 150 && grey[8] == 29 && grey[12] == 255, "grayscale: Rec.601 luma for red/green/blue/white");
            Check(grey[0] == grey[1] && grey[1] == grey[2] && grey[15] == 200, "grayscale: channels equalised, alpha untouched");

            var bc = Ids(100, 200, 28);
            PixelAdjust.BrightnessContrast(bc, 0, 1.0);
            Check(Same(bc, Ids(100, 200, 28)), "brightness/contrast: (0, 1.0) is identity");
            PixelAdjust.BrightnessContrast(bc, 0, 2.0);
            Check(Same(bc, Ids(72, 255, 0)), "brightness/contrast: contrast 2 about midpoint, clamped both ends");
            var flatBc = Ids(10, 250);
            PixelAdjust.BrightnessContrast(flatBc, 0, 0.0);
            Check(Same(flatBc, Ids(128, 128)), "brightness/contrast: contrast 0 -> flat mid-grey");
            var bright = Ids(100);
            PixelAdjust.BrightnessContrast(bright, 999, 1.0);
            Check(Same(bright, Ids(255)), "brightness/contrast: brightness input clamps to +255");
            Check(Throws(() => PixelAdjust.BrightnessContrast(Ids(1), 0, -0.5)), "brightness/contrast: negative contrast rejected");

            var fh = Ids(0, 1, 2, 3);
            PixelAdjust.FlipHorizontal(fh, 2, 2);
            Check(Same(fh, Ids(1, 0, 3, 2)), "flip horizontal: columns swap");
            var fv = Ids(0, 1, 2, 3);
            PixelAdjust.FlipVertical(fv, 2, 2);
            Check(Same(fv, Ids(2, 3, 0, 1)), "flip vertical: rows swap");
            PixelAdjust.FlipHorizontal(fh, 2, 2); PixelAdjust.FlipVertical(fv, 2, 2);
            Check(Same(fh, Ids(0, 1, 2, 3)) && Same(fv, Ids(0, 1, 2, 3)), "flips: twice is identity");

            var (rot, rw, rh) = PixelAdjust.Rotate90Clockwise(Ids(0, 1, 2, 3, 4, 5), 2, 3);
            Check(rw == 3 && rh == 2 && Same(rot, Ids(4, 2, 0, 5, 3, 1)), "rotate 90 CW: dims swap, pixels land exactly");
            var spin = Ids(0, 1, 2, 3, 4, 5); int sw = 2, sh = 3;
            for (int i = 0; i < 4; i++) (spin, sw, sh) = PixelAdjust.Rotate90Clockwise(spin, sw, sh);
            Check(sw == 2 && sh == 3 && Same(spin, Ids(0, 1, 2, 3, 4, 5)), "rotate: four quarter-turns is identity");

            Check(Throws(() => PixelAdjust.FlipHorizontal(Ids(0, 1, 2), 2, 2)), "frame ops: buffer/size mismatch rejected");
            Check(Throws(() => PixelAdjust.Rotate90Clockwise(Ids(0), 0, 1)), "frame ops: zero width rejected");
            Check(Throws(() => PixelAdjust.Invert(new byte[3])), "pixel ops: partial pixel rejected");
        }

    }
}
