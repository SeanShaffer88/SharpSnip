using Snipit.Core;

namespace Snipit.SelfTest;

// Effects checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Effects()
    {
        Console.WriteLine("EdgeEffects");
        {
            int ew = 60, eh = 40;
            var src = new byte[ew * eh * 4];
            for (int i = 0; i < src.Length; i += 4) { src[i] = 10; src[i + 1] = 20; src[i + 2] = 30; src[i + 3] = 255; }
            int Alpha(byte[] b, int x, int y) => b[(y * ew + x) * 4 + 3];
            var torn = EdgeEffects.Apply(src, ew, eh, EdgeStyle.Torn, EdgeSides.All, 6, 7);
            Check(torn.Length == src.Length, "edges: size is unchanged");
            Check(src[3] == 255 && src[0] == 10, "edges: input is not modified");
            Check(Alpha(torn, ew / 2, eh / 2) == 255 && torn[(eh / 2 * ew + ew / 2) * 4] == 10, "edges: centre pixel is untouched");
            bool deepOk = true;
            for (int x = 6; x < ew - 6; x++) for (int y = 6; y < eh - 6; y++) if (Alpha(torn, x, y) != 255) deepOk = false;
            for (int y = 6; y < eh - 6; y++) for (int x = 0; x < ew; x++) if (x >= 6 && x < ew - 6 && Alpha(torn, x, y) != 255) deepOk = false;
            Check(deepOk, "edges: nothing deeper than the depth is cut");
            int cutPx = 0; for (int i = 3; i < torn.Length; i += 4) if (torn[i] == 0) cutPx++;
            Check(cutPx > 0, "edges: torn edge cuts some pixels");
            bool cleared = true; for (int i = 0; i < torn.Length; i += 4) if (torn[i + 3] == 0 && (torn[i] | torn[i + 1] | torn[i + 2]) != 0) cleared = false;
            Check(cleared, "edges: cut pixels are fully transparent black");
            Check(EdgeEffects.Apply(src, ew, eh, EdgeStyle.Torn, EdgeSides.All, 6, 7).SequenceEqual(torn), "edges: same seed gives the same edge");
            Check(!EdgeEffects.Apply(src, ew, eh, EdgeStyle.Torn, EdgeSides.All, 6, 8).SequenceEqual(torn), "edges: another seed gives another edge");
            var bottomOnly = EdgeEffects.Apply(src, ew, eh, EdgeStyle.Torn, EdgeSides.Bottom, 6, 7);
            bool onlyBottom = true; for (int y = 0; y < eh - 6; y++) for (int x = 0; x < ew; x++) if (Alpha(bottomOnly, x, y) != 255) onlyBottom = false;
            Check(onlyBottom, "edges: only the chosen side is cut");
            Check(EdgeEffects.Apply(src, ew, eh, EdgeStyle.Torn, EdgeSides.None, 6, 7).SequenceEqual(src), "edges: no sides = unchanged copy");
            Check(EdgeEffects.Apply(src, ew, eh, EdgeStyle.Wave, EdgeSides.All, 0, 7).SequenceEqual(src), "edges: depth 0 = unchanged copy");
            var hugeCut = EdgeEffects.Apply(src, ew, eh, EdgeStyle.Torn, EdgeSides.All, 9999, 1);
            Check(Alpha(hugeCut, ew / 2, eh / 2) == 255, "edges: absurd depth is clamped, centre survives");
            var wave = EdgeEffects.Profile(EdgeStyle.Wave, 200, 8, 0);
            Check(wave.Min() == 0 && wave.Max() == 8 && wave.All(v => v is >= 0 and <= 8), "edges: wave spans 0..depth");
            Check(Math.Abs(wave[1] - wave[0]) <= 1, "edges: wave is smooth");
            Check(EdgeEffects.Profile(EdgeStyle.Torn, 300, 10, 3).All(v => v is >= 0 and <= 10), "edges: torn profile stays inside 0..depth");
            Check(EdgeEffects.MaxDepthFor(1, 1) == 0 && EdgeEffects.MaxDepthFor(3, 80) == 0 &&
                  EdgeEffects.MaxDepthFor(4, 80) == 1 && EdgeEffects.MaxDepthFor(400, 400) == 100,
                  "edges: max depth is a quarter of the short side, rounded down");
            var tinyCrop = Enumerable.Repeat((byte)255, 2 * 2 * 4).ToArray();
            Check(EdgeEffects.Apply(tinyCrop, 2, 2, EdgeStyle.Torn, EdgeSides.All, 4, 0).SequenceEqual(tinyCrop),
                  "edges: torn leaves a two-by-two crop intact");
            var narrowCrop = Enumerable.Repeat((byte)255, 80 * 3 * 4).ToArray();
            Check(EdgeEffects.Apply(narrowCrop, 80, 3, EdgeStyle.Wave, EdgeSides.All, 4, 0).SequenceEqual(narrowCrop),
                  "edges: wave leaves a three-pixel-high crop intact");
        }

        Console.WriteLine("EffectSettings");
        {
            Check(EffectSettings.FrameThickness(0, 1200, 800) == 13 && EffectSettings.FrameThickness(0, 50, 50) == 2, "effects: automatic frame width follows image size, min 2");
            Check(EffectSettings.FrameThickness(7, 1200, 800) == 7 && EffectSettings.FrameThickness(99999, 100, 100) == ImageFrames.MaxThickness, "effects: manual frame width wins, clamped");
            Check(EffectSettings.ShadowBlur(0, 4000, 3000) == 24 && EffectSettings.ShadowBlur(0, 40, 40) == 4 && EffectSettings.ShadowBlur(9, 40, 40) == 9, "effects: automatic blur clamps 4..24, manual wins");
            Check(EffectSettings.ShadowBlur(500, 40, 40) == ImageFrames.MaxBlur, "effects: huge blur is capped");
            Check(EffectSettings.ShadowOffset(10) == 7 && EffectSettings.ShadowOffset(0) == 2, "effects: offset follows blur");
            Check(EffectSettings.ShadowOpacity(45) == 0.45 && EffectSettings.ShadowOpacity(-5) == 0 && EffectSettings.ShadowOpacity(400) == 1, "effects: opacity percent clamps");
            Check(EffectSettings.EdgeDepth(0, 1600, 1200) == 30 && EffectSettings.EdgeDepth(0, 40, 40) == 4, "effects: automatic depth follows image size");
            Check(EffectSettings.EdgeDepth(200, 100, 100) == 25, "effects: manual depth never beyond a quarter of the short side");
            Check(EffectSettings.EdgeDepth(0, 8, 8) == EdgeEffects.MaxDepthFor(8, 8), "effects: tiny image caps automatic depth");
            Check(EffectSettings.SanitizeColor("#ff8000") == "FF8000" && EffectSettings.SanitizeColor("f80") == "FF8800", "effects: colour accepts #, short form, any case");
            Check(EffectSettings.SanitizeColor("nope") == EffectSettings.DefaultFrameColor && EffectSettings.SanitizeColor(null) == EffectSettings.DefaultFrameColor, "effects: bad colour falls back");
            Check(EffectSettings.FrameRgb("0A0B0C") is { R: 0x0A, G: 0x0B, B: 0x0C }, "effects: FrameRgb parses");
            var ep = new Preferences { FrameThickness = -4, FrameColor = "zz", ShadowBlur = 900, ShadowOpacityPercent = 250, EdgeDepth = 9999 }.Sanitize();
            Check(ep.FrameThickness == 0 && ep.FrameColor == EffectSettings.DefaultFrameColor && ep.ShadowBlur == ImageFrames.MaxBlur && ep.ShadowOpacityPercent == 100 && ep.EdgeDepth == EdgeEffects.MaxDepth, "effects: saved settings are clamped on load");
            var dflt = new Preferences();
            Check(dflt.FrameThickness == 0 && dflt.ShadowBlur == 0 && dflt.EdgeDepth == 0 && dflt.ShadowOpacityPercent == 45, "effects: defaults are automatic");
        }

        Console.WriteLine("ImageCut");
        {
            // 4x3 image, each pixel's blue channel = its index so moves are visible
            int cw = 4, ch = 3;
            var cs = new byte[cw * ch * 4];
            for (int i = 0; i < cw * ch; i++) { cs[i * 4] = (byte)i; cs[i * 4 + 3] = 255; }
            var rows = ImageCut.Remove(cs, cw, ch, new ImageCut.Band(CutAxis.Rows, 1, 1));
            Check(rows.Width == 4 && rows.Height == 2 && rows.Pixels[4 * 4] == 8 && rows.Pixels[0] == 0, "cut: removing the middle row joins top and bottom");
            var cols = ImageCut.Remove(cs, cw, ch, new ImageCut.Band(CutAxis.Columns, 1, 2));
            Check(cols.Width == 2 && cols.Height == 3 && cols.Pixels[0] == 0 && cols.Pixels[4] == 3 && cols.Pixels[2 * 4] == 4 && cols.Pixels[3 * 4] == 7, "cut: removing columns 1-2 joins left and right on every row");
            Check(ImageCut.Remove(cs, cw, ch, new ImageCut.Band(CutAxis.Rows, 0, 1)).Height == 2 && ImageCut.Remove(cs, cw, ch, new ImageCut.Band(CutAxis.Rows, 2, 1)).Pixels[(1 * 4) * 4] == 4, "cut: top and bottom strips can go");
            var whole = ImageCut.Remove(cs, cw, ch, new ImageCut.Band(CutAxis.Rows, 0, 3));
            Check(whole.Height == 3 && whole.Pixels.SequenceEqual(cs), "cut: a band covering everything is refused");
            var oob = ImageCut.Remove(cs, cw, ch, new ImageCut.Band(CutAxis.Rows, 2, 99));
            Check(oob.Height == 2, "cut: out-of-range band is clamped to the image");
            Check(ImageCut.Remove(cs, cw, ch, new ImageCut.Band(CutAxis.Columns, 1, 0)).Pixels.SequenceEqual(cs), "cut: empty band changes nothing");
            Check(cs[0] == 0 && cs[4 * 4] == 4 && cs.Length == 48, "cut: input is not modified");
            Check(ImageCut.Plan(0, 40, 200, 30, 200, 100) is { Axis: CutAxis.Rows, Start: 40, Length: 30 }, "cut plan: a wide drag cuts rows");
            Check(ImageCut.Plan(60, 0, 20, 100, 200, 100) is { Axis: CutAxis.Columns, Start: 60, Length: 20 }, "cut plan: a tall drag cuts columns");
            Check(ImageCut.Plan(10, 10, 100, 50, 200, 100) is { Axis: CutAxis.Rows }, "cut plan: a tie cuts rows");
            Check(ImageCut.Plan(0, 0, 300, 100, 200, 100) is null, "cut plan: a drag over the whole image is refused");
            Check(ImageCut.Plan(0, -20, 200, 40, 200, 100) is { Start: 0, Length: 20 }, "cut plan: a drag past the top edge is clamped");
            Check(ImageCut.Plan(0, 0, 0, 5, 200, 100) is null && ImageCut.Plan(0, 0, 5, 5, 1, 1) is null, "cut plan: degenerate input is refused");
            Check(ImageCut.Plan(0, 150, 200, 20, 200, 100) is null, "cut plan: a drag fully outside the image cuts nothing");
        }

        Console.WriteLine("AreaMask");
        {
            var a1 = new RectPx(10, 10, 4, 4); var a2 = new RectPx(30, 20, 5, 5);
            Check(AreaMask.Bounds(new[] { a1, a2 }) == new RectPx(10, 10, 25, 15), "areas: bounds hold every area");
            Check(AreaMask.Bounds(Array.Empty<RectPx>()).IsEmpty && AreaMask.Bounds(new[] { RectPx.Empty, a1 }) == a1, "areas: empty list and empty areas are ignored");
            var frame = AreaMask.Bounds(new[] { a1, a2 });
            int fw = frame.Width, fh = frame.Height;
            var fp = new byte[fw * fh * 4];
            for (int i = 0; i < fp.Length; i += 4) { fp[i] = 9; fp[i + 1] = 9; fp[i + 2] = 9; fp[i + 3] = 255; }
            AreaMask.ApplyAlpha(fp, fw, fh, new[] { a1, a2 }, frame);
            int FA(int vx, int vy) => fp[((vy - frame.Y) * fw + (vx - frame.X)) * 4 + 3];
            Check(FA(10, 10) == 255 && FA(13, 13) == 255 && FA(30, 20) == 255 && FA(34, 24) == 255, "areas: pixels inside an area are kept");
            Check(FA(14, 10) == 0 && FA(20, 15) == 0 && FA(29, 20) == 0 && FA(35 - 1, 25 - 1 + 0) == 255, "areas: pixels between areas and just outside edges go transparent");
            int kept = 0; for (int i = 3; i < fp.Length; i += 4) if (fp[i] == 255) kept++;
            Check(kept == 16 + 25, "areas: exactly the area pixels are kept");
            bool clear = true; for (int i = 0; i < fp.Length; i += 4) if (fp[i + 3] == 0 && (fp[i] | fp[i + 1] | fp[i + 2]) != 0) clear = false;
            Check(clear, "areas: cut pixels are fully transparent black");
            var ov = new byte[8 * 8 * 4]; for (int i = 3; i < ov.Length; i += 4) ov[i] = 255;
            AreaMask.ApplyAlpha(ov, 8, 8, new[] { new RectPx(0, 0, 5, 5), new RectPx(3, 3, 5, 5) }, new RectPx(0, 0, 8, 8));
            int okp = 0; for (int i = 3; i < ov.Length; i += 4) if (ov[i] == 255) okp++;
            Check(okp == 25 + 25 - 4, "areas: overlapping areas merge without double counting");
            var off = new byte[4 * 4 * 4]; for (int i = 3; i < off.Length; i += 4) off[i] = 255;
            AreaMask.ApplyAlpha(off, 4, 4, new[] { new RectPx(100, 100, 5, 5), new RectPx(-2, -2, 4, 4) }, new RectPx(0, 0, 4, 4));
            int okq = 0; for (int i = 3; i < off.Length; i += 4) if (off[i] == 255) okq++;
            Check(okq == 4, "areas: areas off the frame are ignored and partly-off areas are clipped");
            bool shortBufThrew = false; try { AreaMask.ApplyAlpha(new byte[4], 2, 2, new[] { a1 }, new RectPx(0, 0, 2, 2)); } catch (ArgumentException) { shortBufThrew = true; }
            Check(shortBufThrew, "areas: a short buffer is refused");
        }


        {
            var sat = new byte[] { 0, 0, 255, 255, 50, 100, 150, 77 };   // red; muted orange-ish, alpha 77
            var s1 = (byte[])sat.Clone(); PixelAdjust.Saturation(s1, 1.0);
            Check(s1.SequenceEqual(sat), "saturation: factor 1 is identity");
            var s0 = (byte[])sat.Clone(); PixelAdjust.Saturation(s0, 0.0);
            Check(s0[0] == s0[1] && s0[1] == s0[2] && s0[0] == 76 && s0[7] == 77, "saturation: factor 0 = Rec.601 grey, alpha untouched");
            var s2 = (byte[])sat.Clone(); PixelAdjust.Saturation(s2, 3.0);
            Check(s2[2] == 255 && s2[0] == 0 && s2[1] == 0, "saturation: boost clamps to 0..255");
            var thrownSat = false; try { PixelAdjust.Saturation(new byte[4], -1); } catch (ArgumentOutOfRangeException) { thrownSat = true; }
            Check(thrownSat, "saturation: negative factor rejected");

            var flatSh = Enumerable.Repeat((byte)90, 3 * 3 * 4).ToArray();
            var fl = (byte[])flatSh.Clone(); PixelAdjust.Sharpen(fl, 3, 3, 2.0);
            Check(fl.SequenceEqual(flatSh), "sharpen: flatSh image unchanged");
            var edge = new byte[4 * 1 * 4];
            for (int px = 2; px < 4; px++) for (int c = 0; c < 3; c++) edge[px * 4 + c] = 200;
            for (int px = 0; px < 4; px++) edge[px * 4 + 3] = 255;
            var ed = (byte[])edge.Clone(); PixelAdjust.Sharpen(ed, 4, 1, 1.0);
            Check(ed[1 * 4] == 0 && ed[2 * 4] == 250, "sharpen: edge contrast increases (dark side stays dark, light side brighter)");
            Check(ed[3] == 255 && ed[7] == 255, "sharpen: alpha untouched");
            var id = (byte[])edge.Clone(); PixelAdjust.Sharpen(id, 4, 1, 0.0);
            Check(id.SequenceEqual(edge), "sharpen: amount 0 is identity");
            var thrownSh = false; try { PixelAdjust.Sharpen(new byte[8], 3, 1, 1.0); } catch (ArgumentException) { thrownSh = true; }
            Check(thrownSh, "sharpen: buffer/size mismatch rejected");
        }


        {
            var z = LoupeView.Zoom(100, 100, 80, 60, 500, 400, 2.0);
            Check(z == (120, 115, 40, 30), "loupe: 2x shows the centred half-size window");
            var zc = LoupeView.Zoom(-20, 380, 80, 60, 500, 400, 2.0);
            Check(zc.X == 0 && zc.Y + zc.Height == 400 && zc.Width == 40 && zc.Height == 30, "loupe: window shifted back inside the image");
            Check(LoupeView.Zoom(0, 0, 80, 60, 500, 400, 1.0) == (0, 0, 80, 60), "loupe: factor 1 shows the same area");
            var bigLp = LoupeView.Zoom(0, 0, 900, 900, 100, 100, 2.0);
            Check(bigLp.Width == 100 && bigLp.Height == 100 && bigLp.X == 0 && bigLp.Y == 0, "loupe: window never larger than the image");
            var thrownLp = false; try { LoupeView.Zoom(0, 0, 1, 1, 10, 10, 0.5); } catch (ArgumentOutOfRangeException) { thrownLp = true; }
            Check(thrownLp, "loupe: factor below 1 rejected");
        }


        {
            var px = new byte[] { 0, 0, 255, 255,  90, 90, 90, 40 };   // BGRA: pure red alpha 255; grey alpha 40
            var h0 = (byte[])px.Clone(); PixelAdjust.HueShift(h0, 0);
            Check(h0.SequenceEqual(px), "hue: 0 degrees is identity");
            var h360 = (byte[])px.Clone(); PixelAdjust.HueShift(h360, 360);
            Check(h360.SequenceEqual(px), "hue: 360 degrees wraps to identity");
            var h120 = (byte[])px.Clone(); PixelAdjust.HueShift(h120, 120);
            Check(h120[1] > h120[2] && h120[1] > h120[0] && h120[1] > 150, "hue: red rotated 120 degrees becomes green-dominant");
            Check(Math.Abs(h120[4] - 90) <= 1 && Math.Abs(h120[5] - 90) <= 1 && Math.Abs(h120[6] - 90) <= 1 && h120[7] == 40 && h120[3] == 255, "hue: grey stays grey, alpha untouched");

            var cb = new byte[] { 10, 100, 250, 77 };
            PixelAdjust.ColorBalance(cb, 20, -50, 5);
            Check(cb[2] == 255 && cb[1] == 50 && cb[0] == 15 && cb[3] == 77, "balance: per-channel add, clamped, alpha untouched");
            var cb0 = new byte[] { 1, 2, 3, 4 }; PixelAdjust.ColorBalance(cb0, 0, 0, 0);
            Check(cb0.SequenceEqual(new byte[] { 1, 2, 3, 4 }), "balance: zero is identity");
            var cbBig = new byte[] { 100, 100, 100, 255 }; PixelAdjust.ColorBalance(cbBig, 9999, -9999, 0);
            Check(cbBig[2] == 255 && cbBig[1] == 0 && cbBig[0] == 100, "balance: offsets clamped to +-255");
        }


        {
            var ch = new EscapeChord();
            Check(!ch.Press(1000) && !ch.Press(1400) && ch.Press(1800), "escape chord: three presses inside the window fire on the third");
            Check(!ch.Press(2000) && !ch.Press(2100), "escape chord: restarts after firing");
            var slow = new EscapeChord();
            Check(!slow.Press(0) && !slow.Press(1000) && !slow.Press(2000) && !slow.Press(3000), "escape chord: slow presses never fire");
            Check(!slow.Press(3500) && slow.Press(3800), "escape chord: three inside 1.5 s after slow ones fires");
            var reset = new EscapeChord();
            Check(!reset.Press(5000) && !reset.Press(5100) && !reset.Press(10), "escape chord: an earlier tick (counter reset) drops stale presses");
        }


        {
            Check(UndoBudget.KeepCount(new long[] { 100, 100, 100 }, 250) == 2, "undo budget: keeps the newest entries that fit");
            Check(UndoBudget.KeepCount(new long[] { 900, 10 }, 100) == 1, "undo budget: always keeps the latest entry, even over budget");
            Check(UndoBudget.KeepCount(Array.Empty<long>(), 100) == 0, "undo budget: nothing to keep");
            Check(UndoBudget.KeepCount(new long[] { 0, 0, 0, 0 }, 0) == 4, "undo budget: free entries (annotations) are all kept");
            Check(UndoBudget.BitmapBytes(1920, 1080) == 8294400, "undo budget: bitmap size in bytes");
        }


        {
            Check(Math.Abs(WcagContrast.Ratio("#000000", "#FFFFFF") - 21.0) < 0.01, "contrast: black on white is 21:1");
            Check(WcagContrast.Ratio("#777777", "#FFFFFF") is > 4.4 and < 4.6, "contrast: #777 on white is about 4.48:1");
            // The dark theme's text tokens (TesselTheme / EditorChrome) on every surface they sit on.
            string[] surfaces = { "#1B1B1D", "#1F1F22", "#232326", "#2A2A2F", "#28282D", "#17171A" };
            bool inkOk = true, mutedOk = true;
            foreach (var surface in surfaces)
            {
                inkOk &= WcagContrast.Ratio("#E6E3E1", surface) >= WcagContrast.AaNormalText;
                mutedOk &= WcagContrast.Ratio("#A3A0A4", surface) >= WcagContrast.AaNormalText;
            }
            Check(inkOk, "contrast: foreground token meets AA 4.5:1 on every dark surface");
            Check(mutedOk, "contrast: muted token meets AA 4.5:1 on every dark surface");
            Check(WcagContrast.Ratio("#89868A", "#232326") < WcagContrast.AaNormalText, "contrast: the old muted value #89868A failed AA on the bar");
            Check(WcagContrast.Ratio("#E8939F", "#232326") >= WcagContrast.AaLargeOrUi, "contrast: focus ring colour meets 3:1 on the bar");
            var badHex = false; try { WcagContrast.Luminance("red"); } catch (FormatException) { badHex = true; }
            Check(badHex, "contrast: malformed colour rejected");
        }


        {
            // Clone must carry every stored setting (Settings starts from it so unedited settings are never reset).
            var src = new Preferences();
            foreach (var prop in typeof(Preferences).GetProperties())
            {
                if (!prop.CanWrite || !prop.CanRead) continue;
                object? v = prop.PropertyType == typeof(bool) ? !(bool)prop.GetValue(src)!
                          : prop.PropertyType == typeof(int) ? (object)((int)prop.GetValue(src)! + 7)
                          : prop.PropertyType == typeof(double) ? (object)0.5
                          : prop.PropertyType == typeof(string) ? "x" + prop.Name
                          : prop.PropertyType == typeof(uint) ? (object)(((uint)prop.GetValue(src)!) + 3u)
                          : null;
                if (v is not null) prop.SetValue(src, v);
            }
            var cl = src.Clone();
            var lost = typeof(Preferences).GetProperties().Where(pp => pp.CanWrite && pp.CanRead && !Equals(pp.GetValue(src), pp.GetValue(cl)) && pp.PropertyType.IsValueType | pp.PropertyType == typeof(string)).Select(pp => pp.Name).ToList();
            Check(lost.Count == 0, "preferences clone carries every simple setting" + (lost.Count == 0 ? "" : ": lost " + string.Join(", ", lost)));
            Check(new Preferences().EditorToolHotkeys, "editor tool hotkeys default to on");
            var fx = new Preferences { FrameThickness = 9, ShadowBlur = 11, EdgeDepth = 13 }.Clone();
            Check(fx.FrameThickness == 9 && fx.ShadowBlur == 11 && fx.EdgeDepth == 13, "clone keeps the effect settings the Settings dialog used to reset");
        }

    }
}
