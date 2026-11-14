using Snipit.Core;

namespace Snipit.SelfTest;

// Layout checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Layout()
    {
        Console.WriteLine("Print Screen override");
        {
            var sp = new Preferences { PrintScreenSnippingPrior = 7 }.Sanitize();
            Check(sp.PrintScreenSnippingPrior == 1, "prefs sanitize: recorded prior clamps to [-1,1]");
            sp = new Preferences { PrintScreenSnippingPrior = -5 }.Sanitize();
            Check(sp.PrintScreenSnippingPrior == -1, "prefs sanitize: -1 (the value was absent) survives the clamp");
            Check(new Preferences().ShowTrayIcon, "prefs defaults: tray icon shown");
        }

        Console.WriteLine("ModeBarLayout");
        {
            Check(ModeBarLayout.BarWidth(6) == 6 + 16 + 6 * 64 + 9 + 36 + 6, "mode bar: width = padding + buttons + separator + close");
            Check(ModeBarLayout.BarWidth(0) == 0, "mode bar: no buttons -> no bar");
            var hit = ModeBarLayout.HitTest(ModeBarLayout.ButtonLeft(0) + 32, 36, 6);
            Check(hit is { Index: 0, IsClose: false }, "mode bar: first button centre hits button 0");
            hit = ModeBarLayout.HitTest(ModeBarLayout.ButtonLeft(5) + 63.9, 36, 6);
            Check(hit is { Index: 5, IsClose: false }, "mode bar: last button's right edge still hits it");
            Check(ModeBarLayout.HitTest(ModeBarLayout.ButtonLeft(6) + 4, 36, 6) is null, "mode bar: separator hits nothing");
            hit = ModeBarLayout.HitTest(ModeBarLayout.CloseLeft(6) + 18, 36, 6);
            Check(hit is { IsClose: true }, "mode bar: close zone hits close");
            Check(ModeBarLayout.HitTest(ModeBarLayout.CloseLeft(6) + 36, 36, 6) is null, "mode bar: past close hits nothing");
            Check(ModeBarLayout.HitTest(40, ModeBarLayout.BarHeight, 6) is null && ModeBarLayout.HitTest(40, -1, 6) is null, "mode bar: above/below hits nothing");
            Check(ModeBarLayout.HitTest(3, 36, 6) is null, "mode bar: left padding hits nothing");
            hit = ModeBarLayout.HitTest(ModeBarLayout.PadX + 8, 36, 6);
            Check(hit is { IsGrip: true, IsClose: false }, "mode bar: left end is the drag grip");
            Check(ModeBarLayout.HitTest(ModeBarLayout.ButtonLeft(0), 36, 6) is { Index: 0, IsGrip: false }, "mode bar: grip ends where button 0 starts");
            double mw = 1920, bw = ModeBarLayout.BarWidth(7);
            Check(ModeBarLayout.Left(0.5, mw, bw) == (mw - bw) / 2, "mode bar place: default centre is centred");
            Check(ModeBarLayout.Left(0, mw, bw) == 0 && ModeBarLayout.Left(1, mw, bw) == mw - bw, "mode bar place: edges clamp onto the monitor");
            Check(ModeBarLayout.Left(double.NaN, mw, bw) == (mw - bw) / 2, "mode bar place: NaN falls back to centred");
            Check(ModeBarLayout.Left(0.5, 300, bw) == 0, "mode bar place: bar wider than monitor pins left");
            double c0 = 0.3;
            Check(Math.Abs(ModeBarLayout.CenterFor(ModeBarLayout.Left(c0, mw, bw), mw, bw) - c0) < 1e-9, "mode bar place: CenterFor inverts Left");
            Check(ModeBarLayout.CenterFor(-500, mw, bw) == 0 && ModeBarLayout.CenterFor(5000, mw, bw) == 1, "mode bar place: dragged past an edge stores 0 or 1");
            var pp = new Preferences { ModeBarCenter = 7 }; pp.Sanitize();
            Check(pp.ModeBarCenter == 1, "mode bar place: stored value is clamped on load");
        }

        Console.WriteLine("PixelBlit");
        {
            // 4x3 source, every pixel a unique byte value; copy the middle 2x2 into a 3x3 canvas at (1,1).
            var src = new byte[4 * 3 * 4];
            for (int i = 0; i < src.Length; i++) src[i] = (byte)i;
            var dst = new byte[3 * 3 * 4];
            PixelBlit.Copy(src, 16, 1, 1, dst, 12, 1, 1, 2, 2);
            Check(dst[(1 * 3 + 1) * 4] == src[(1 * 4 + 1) * 4] && dst[(2 * 3 + 2) * 4 + 3] == src[(2 * 4 + 2) * 4 + 3], "blit: copied block lands pixel-exact");
            Check(dst[0] == 0 && dst[(0 * 3 + 2) * 4] == 0, "blit: untouched canvas stays zero");
            // Strided source (padded rows) reads through the stride, not the width.
            var padded = new byte[6 * 2 * 4];
            for (int i = 0; i < padded.Length; i++) padded[i] = (byte)(i + 1);
            var blitSmall = new byte[2 * 2 * 4];
            PixelBlit.Copy(padded, 24, 2, 0, blitSmall, 8, 0, 0, 2, 2);
            Check(blitSmall[0] == padded[2 * 4] && blitSmall[8] == padded[24 + 2 * 4], "blit: source stride honoured");
            bool blitThrew = false;
            try { PixelBlit.Copy(src, 16, 3, 2, dst, 12, 0, 0, 2, 2); } catch (ArgumentException) { blitThrew = true; }
            Check(blitThrew, "blit: out-of-range copy is rejected");
        }

        Console.WriteLine("RecordingPlan");
        {
            Check(RecordingPlan.FileName(new DateTime(2026, 11, 13, 9, 5, 3)) == "recording-20261113-090503.mp4", "recording: timestamped mp4 name");
            Check(RecordingPlan.ClampFps(0) == 1 && RecordingPlan.ClampFps(30) == 30 && RecordingPlan.ClampFps(240) == 60, "recording: fps clamped to 1..60");
            Check(RecordingPlan.EvenSize(1920, 1080) == (1920, 1080) && RecordingPlan.EvenSize(1919, 1079) == (1918, 1078) && RecordingPlan.EvenSize(1, 3) == (2, 2), "recording: sizes rounded down to even, min 2");
            Check(RecordingPlan.Bitrate(320, 240, 30) >= 1_000_000u, "recording: tiny frames still get the floor bitrate");
            Check(RecordingPlan.Bitrate(3840, 2160, 60) is > 50_000_000u and < 80_000_000u, "recording: 4K60 lands in the high bitrate band");
            Check(RecordingPlan.Bitrate(7680, 4320, 60) == 80_000_000u, "recording: 8K60 hits the bitrate ceiling");
            Check(RecordingPlan.Bitrate(1920, 1080, 30) > RecordingPlan.Bitrate(1280, 720, 30), "recording: bitrate grows with frame size");
            Check(RecordingQuality.Factor("Low") < RecordingQuality.Factor("Medium")
                && RecordingQuality.Factor("Medium") < RecordingQuality.Factor("High")
                && RecordingQuality.Factor("High") < RecordingQuality.Factor("Ultra"), "recording: quality factors increase by preset");
            Check(RecordingQuality.Sanitize("Ultra") == "Ultra" && RecordingQuality.Sanitize("bogus") == "Medium" && RecordingQuality.Sanitize(null) == "Medium", "recording: quality sanitize falls back to Medium");
            Check(RecordingPlan.Bitrate(1920, 1080, 30, RecordingQuality.Factor("Ultra")) > RecordingPlan.Bitrate(1920, 1080, 30, RecordingQuality.Factor("Low")), "recording: bitrate follows the quality factor");
            Check(RecordingPlan.GifFileName(new DateTime(2026, 11, 13, 9, 5, 3)) == "recording-20261113-090503.gif", "recording: gif file name");
            Check(!RecordingPlan.LimitHit(TimeSpan.FromMinutes(5), 0, 10_000_000, 0), "recording: zero limits never hit");
            Check(RecordingPlan.LimitHit(TimeSpan.FromMinutes(5), 240, 0, 0) && !RecordingPlan.LimitHit(TimeSpan.FromMinutes(3), 240, 0, 0), "recording: time limit hits at the boundary");
            Check(RecordingPlan.LimitHit(TimeSpan.Zero, 0, 500, 500) && !RecordingPlan.LimitHit(TimeSpan.Zero, 0, 499, 500), "recording: size limit hits at the boundary");
        }

        Console.WriteLine("WavWriter + PcmMix");
        {
            using var ms = new MemoryStream();
            using (var w = new WavWriter(ms, 48000, 2))
                w.Write(new short[] { 1000, -1000, 2000, -2000 });
            var wav = ms.ToArray();
            Check(wav.Length == 44 + 8 && wav[0] == (byte)'R' && wav[8] == (byte)'W', "wav: header + data length");
            Check(BitConverter.ToUInt32(wav, 40) == 8u && BitConverter.ToUInt32(wav, 4) == 36u + 8u, "wav: size fields patched on dispose");
            Check(BitConverter.ToUInt16(wav, 22) == 2 && BitConverter.ToUInt32(wav, 24) == 48000u, "wav: channels and rate in header");
            Check(BitConverter.ToInt16(wav, 44) == 1000 && BitConverter.ToInt16(wav, 46) == -1000, "wav: samples little-endian");

            var mixed = PcmMix.Mix(new short[] { 30000, 100 }, new short[] { 10000, -200, 500 });
            Check(mixed.Length == 3 && mixed[0] == short.MaxValue && mixed[1] == -100 && mixed[2] == 500, "pcm: mix sums, clamps and pads with silence");
            var rs = PcmMix.Resample(new short[] { 0, 1000, 2000, 3000 }, 1, 4000, 2000);
            Check(rs.Length == 2 && rs[0] == 0 && rs[1] == 2000, "pcm: 2:1 downsample picks the interpolated frames");
            var up = PcmMix.Resample(new short[] { 0, 1000 }, 1, 2000, 4000);
            Check(up.Length == 4 && up[0] == 0 && up[1] == 500 && up[2] == 1000, "pcm: 1:2 upsample interpolates midpoints");
            Check(PcmMix.FloatToPcm(0.5f) == 16384 && PcmMix.FloatToPcm(2f) == short.MaxValue && PcmMix.FloatToPcm(-2f) == short.MinValue, "pcm: float conversion clamps");
        }

        Console.WriteLine("PixelBlit.ScaleCopy + PipLayout");
        {
            var src2 = new byte[2 * 2 * 4];
            for (int i = 0; i < src2.Length; i++) src2[i] = (byte)(i + 1);
            var bigCanvas = new byte[4 * 4 * 4];
            PixelBlit.ScaleCopy(src2, 8, 2, 2, bigCanvas, 16, 0, 0, 4, 4);
            Check(bigCanvas[0] == src2[0] && bigCanvas[(0 * 4 + 3) * 4] == src2[(0 * 2 + 1) * 4] && bigCanvas[(3 * 4 + 0) * 4] == src2[(1 * 2 + 0) * 4], "scale: 2x upscale duplicates quadrants");
            var src4 = new byte[4 * 4 * 4];
            for (int i = 0; i < src4.Length; i++) src4[i] = (byte)i;
            var small2 = new byte[2 * 2 * 4];
            PixelBlit.ScaleCopy(src4, 16, 4, 4, small2, 8, 0, 0, 2, 2);
            Check(small2[0] == src4[0] && small2[4] == src4[8] && small2[8] == src4[32], "scale: 2x downscale samples every other pixel");

            var br = PipLayout.DestRect(1920, 1080, 640, 360, PipCorner.BottomRight);
            Check(br.Width == 422 && br.Height == 236 && br.X == 1920 - 16 - 422 && br.Y == 1080 - 16 - 236, "pip: bottom-right placement keeps aspect, even size");
            var tl = PipLayout.DestRect(1920, 1080, 640, 480, PipCorner.TopLeft);
            Check(tl.X == 16 && tl.Y == 16, "pip: top-left sits at the margin");
            Check(PipLayout.ParseCorner("topleft") == PipCorner.TopLeft && PipLayout.ParseCorner("junk") == PipCorner.BottomRight, "pip: corner parsing with fallback");
            Check(PipLayout.DestRect(100, 100, 640, 360, PipCorner.BottomRight).Width <= 50, "pip: tiny frames clamp the overlay to half");
        }

        Console.WriteLine("Editor plans (resize / watermark / text replace / border)");
        {
            Check(ResizePlan.FromWidth(1920, 1080, 960) == (960, 540), "resize: halving width halves height");
            Check(ResizePlan.FromHeight(1920, 1080, 540) == (960, 540), "resize: from height keeps aspect");
            Check(ResizePlan.FromWidth(100, 100, 5).W == ResizePlan.MinDim, "resize: dimensions clamp at the minimum");
            Check(WatermarkPlan.CornerOrigin(800, 600, 100, 20) == (800 - 112, 600 - 32), "watermark: corner origin leaves a margin");
            Check(WatermarkPlan.TileOrigins(300, 200, 150, 100).Count == 4, "watermark: tile grid covers the image");
            var words = new List<OcrWord>
            {
                new("Hello", new RectPx(0, 0, 40, 10)), new("brave", new RectPx(45, 0, 40, 10)),
                new("new", new RectPx(90, 0, 20, 10)), new("WORLD", new RectPx(115, 0, 40, 10)),
                new("hello", new RectPx(0, 20, 40, 10)),
            };
            var tm1 = TextReplace.Find(words, "hello");
            Check(tm1.Count == 2 && tm1[0].StartWord == 0 && tm1[1].StartWord == 4, "replace: single word matches case-insensitively, repeatedly");
            var tm2 = TextReplace.Find(words, "brave new");
            Check(tm2.Count == 1 && tm2[0].WordCount == 2 && tm2[0].Union.X == 45 && tm2[0].Union.Right == 110, "replace: multi-word match unions the rectangles");
            Check(TextReplace.Find(words, "absent").Count == 0 && TextReplace.Find(words, "  ").Count == 0, "replace: no match and empty query are safe");

            var img = new byte[4 * 4 * 4];
            PixelAdjust.Border(img, 4, 4, 1, 255, 0, 0);
            Check(img[2] == 255 && img[0] == 0 && img[(1 * 4 + 1) * 4 + 2] == 0, "border: frame painted, interior untouched");
            Check(AutoEffects.Sanitize("Border") == "Border" && AutoEffects.Sanitize("x") == "None", "auto effect: sanitize");
        }

        Console.WriteLine("PolygonMask + ColorReadout");
        {
            var square = new List<(int, int)> { (0, 0), (10, 0), (10, 10), (0, 10) };
            Check(PolygonMask.Contains(square, 5, 5) && !PolygonMask.Contains(square, 15, 5) && !PolygonMask.Contains(square, 5, -1), "polygon: square inside/outside");
            var tri = new List<(int, int)> { (0, 0), (20, 0), (0, 20) };
            Check(PolygonMask.Contains(tri, 2, 2) && !PolygonMask.Contains(tri, 15, 15), "polygon: triangle hypotenuse respected");
            Check(PolygonMask.Bounds(tri) == new RectPx(0, 0, 21, 21), "polygon: bounds include the outline pixels");
            var frame = new byte[4 * 4 * 4];
            for (int i = 3; i < frame.Length; i += 4) frame[i] = 255;
            var mask = new List<(int, int)> { (1, 1), (2, 1), (2, 2), (1, 2) };
            PolygonMask.ApplyAlpha(frame, 4, 4, mask, new RectPx(0, 0, 4, 4));
            Check(frame[(0 * 4 + 0) * 4 + 3] == 0 && frame[(1 * 4 + 1) * 4 + 3] == 255, "polygon: alpha cleared outside, kept inside");
            Check(ColorReadout.Format(255, 0, 16, 120, 40) == "#FF0010 · (255, 0, 16) · 120, 40", "readout: hex, rgb and position");
        }

        Console.WriteLine("WorkflowPlan");
        {
            Check(WorkflowPlan.TryParseStep("copy: D:\\Backup", out var cp) && cp.Kind == "copy" && cp.Arg == "D:\\Backup", "workflow: copy step keeps a path with a colon");
            Check(WorkflowPlan.TryParseStep("RUN: C:\\tools\\x.exe|\"%path%\"", out var rn) && rn.Kind == "run", "workflow: kind is case-insensitive");
            Check(WorkflowPlan.SplitRun(rn.Arg).Program == "C:\\tools\\x.exe" && WorkflowPlan.SplitRun(rn.Arg).Args == "\"%path%\"", "workflow: run splits on the pipe");
            Check(WorkflowPlan.TryParseStep("editor:", out var ed) && ed.Arg == "", "workflow: editor step takes no argument");
            Check(!WorkflowPlan.TryParseStep("editor: x", out _), "workflow: editor rejects an argument");
            Check(WorkflowPlan.TryParseStep("effect: Grayscale", out _), "workflow: effect accepts a real effect");
            Check(!WorkflowPlan.TryParseStep("effect: None", out _) && !WorkflowPlan.TryParseStep("effect: Sparkle", out _), "workflow: effect rejects None and unknown names");
            Check(WorkflowPlan.TryParseStep("format: JPG", out var fm) && fm.Arg == "jpg", "workflow: format normalizes through ImageFormats");
            Check(!WorkflowPlan.TryParseStep("format: exe", out _) && !WorkflowPlan.TryParseStep("upload: http://x", out _), "workflow: unknown kinds and formats rejected");
            var clean = WorkflowPlan.SanitizeSteps(new[] { "copy: D:\\A", "bogus", "format: png", "" });
            Check(clean.Count == 2 && clean[0] == "copy: D:\\A" && clean[1] == "format: png", "workflow: sanitize drops invalid lines and canonicalizes");
            Check(WorkflowPlan.SanitizeSteps(Enumerable.Repeat("editor:", 20)).Count == WorkflowPlan.MaxSteps, "workflow: steps capped");
            var wfPath = Path.Combine("pics-root", "shot.PNG");
            Check(WorkflowPlan.Expand("in=%path% d=%dir% n=%name% e=%ext%", wfPath) == $"in={wfPath} d=pics-root n=shot e=PNG", "workflow: placeholders expand");
            Check(WorkflowPlan.SanitizeApplyTo("recording") == "Recording" && WorkflowPlan.SanitizeApplyTo("junk") == "Both", "workflow: apply-to sanitize");
            Check(WorkflowPlan.AppliesTo("Both", "Capture") && WorkflowPlan.AppliesTo("Recording", "Recording") && !WorkflowPlan.AppliesTo("Recording", "Capture"), "workflow: applies-to matching");
            var wfDefs = new List<WorkflowDef> { new() { Name = "Archive" }, new() { Name = "Send" } };
            Check(WorkflowPlan.Find(wfDefs, "archive")?.Name == "Archive" && WorkflowPlan.Find(wfDefs, "nope") is null, "workflow: find by name, case-insensitive");
        }


        Console.WriteLine("ConnectorRoute");
        {
            var h = ConnectorRoute.Elbow(0, 0, 100, 40);
            Check(h.Length == 3 && h[1] == (100, 0) && h[2] == (100, 40), "wide drag bends horizontal first");
            var v = ConnectorRoute.Elbow(0, 0, 40, 100);
            Check(v.Length == 3 && v[1] == (0, 100), "tall drag bends vertical first");
            Check(ConnectorRoute.Elbow(10, 10, 200, 12).Length == 2, "near-flat drag stays a straight line");
            var back = ConnectorRoute.Elbow(100, 100, 20, 60);
            Check(back[0] == (100, 100) && back[1] == (20, 100) && back[2] == (20, 60), "leftward drag keeps start and end");
            Check(ConnectorRoute.Elbow(5, 5, 5, 5).Length == 2, "zero-length drag is two points");
        }

        Console.WriteLine("PolygonBuilder");
        {
            var pb = new PolygonBuilder();
            Check(pb.Click(10, 10) == PolygonBuilder.Result.Added && pb.Click(60, 10) == PolygonBuilder.Result.Added, "clicks add vertices");
            Check(pb.Click(61, 11) == PolygonBuilder.Result.Ignored && pb.Count == 2, "a repeat click on the last vertex is ignored below three vertices");
            Check(!pb.CanClose, "two vertices cannot close");
            pb.Click(60, 60);
            Check(pb.CanClose, "three spread-out vertices can close");
            Check(pb.Click(14, 13) == PolygonBuilder.Result.Closed, "click near the first vertex closes");
            Check(pb.Click(60, 60) == PolygonBuilder.Result.Closed, "double-click on the last vertex closes");
            Check(pb.Undo() && pb.Count == 2 && pb.Points[^1] == (60, 10), "undo removes the last vertex");
            pb.Clear();
            Check(pb.Count == 0 && !pb.Undo(), "clear empties and undo on empty is false");
            var tiny = new PolygonBuilder();
            tiny.Click(0, 0); tiny.Click(3, 0); tiny.Click(3, 3);
            Check(!tiny.CanClose && tiny.Click(1, 1) == PolygonBuilder.Result.Ignored, "a tiny outline refuses to close");
        }
    }
}
