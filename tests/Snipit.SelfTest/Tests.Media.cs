using Snipit.Core;

namespace Snipit.SelfTest;

// Media checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Media()
    {
        Console.WriteLine("Crc32 + ApngWriter");
        {
            Check(Crc32.Compute("123456789"u8) == 0xCBF43926u, "crc32: standard check vector");
            byte[] TinyPng(int w, int h, byte fill)
            {
                var m = new MemoryStream();
                m.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
                void Chunk(string t, byte[] d)
                {
                    Span<byte> word = stackalloc byte[4];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(word, (uint)d.Length);
                    m.Write(word); m.Write(System.Text.Encoding.ASCII.GetBytes(t)); m.Write(d);
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(word, Crc32.ComputeChunk(t, d));
                    m.Write(word);
                }
                var ih = new byte[13];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(ih.AsSpan(0), (uint)w);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(ih.AsSpan(4), (uint)h);
                ih[8] = 8; ih[9] = 6;
                Chunk("IHDR", ih); Chunk("IDAT", new[] { fill, fill, fill }); Chunk("IEND", Array.Empty<byte>());
                return m.ToArray();
            }
            var apngMs = new MemoryStream();
            using (var wtr = new ApngWriter(apngMs, TinyPng(2, 2, 0x11), 100)) wtr.AddFrame(TinyPng(2, 2, 0x22), 50);
            var apngBytes = apngMs.ToArray();
            var chunkTypes = new List<string>();
            var chunkData = new List<byte[]>();
            bool crcsOk = true;
            for (int pos = 8; pos + 12 <= apngBytes.Length;)
            {
                int len = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(apngBytes.AsSpan(pos));
                string t = System.Text.Encoding.ASCII.GetString(apngBytes, pos + 4, 4);
                var d = apngBytes.AsSpan(pos + 8, len).ToArray();
                uint stored = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(apngBytes.AsSpan(pos + 8 + len));
                if (stored != Crc32.ComputeChunk(t, d)) crcsOk = false;
                chunkTypes.Add(t); chunkData.Add(d);
                pos += 12 + len;
                if (t == "IEND") break;
            }
            Check(string.Join(",", chunkTypes) == "IHDR,acTL,fcTL,IDAT,fcTL,fdAT,IEND", "apng: chunk order for two frames");
            Check(crcsOk, "apng: every written chunk CRC verifies");
            Check(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chunkData[1].AsSpan(0)) == 2, "apng: acTL frame count patched to 2");
            Check(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chunkData[2].AsSpan(0)) == 0
                && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chunkData[4].AsSpan(0)) == 1
                && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chunkData[5].AsSpan(0)) == 2, "apng: sequence numbers run 0,1,2");
            bool mismatchThrew = false;
            try { using var w2 = new ApngWriter(new MemoryStream(), TinyPng(2, 2, 0x33), 10); w2.AddFrame(TinyPng(3, 3, 0x44), 10); }
            catch (InvalidDataException) { mismatchThrew = true; }
            Check(mismatchThrew, "apng: a frame with different dimensions is rejected");
            Check(AnimationPlan.Kind("apng") == "apng" && AnimationPlan.Kind("GIF") == "gif" && AnimationPlan.Kind("png") is null, "apng: animation kind routing");
            Check(ImageFormats.Normalize("apng") == "apng" && ImageFormats.Normalize(".APNG") == "apng", "apng: registered as a save format");
        }

        Console.WriteLine("RecordingSize");
        {
            Check(RecordingSize.Target(3840, 2160, "1080p") == (1920, 1080), "recsize: 4K caps to 1080p");
            Check(RecordingSize.Target(1920, 1080, "720p") == (1280, 720), "recsize: 1080p caps to 720p");
            Check(RecordingSize.Target(1280, 720, "1080p") == (1280, 720), "recsize: never upscales");
            Check(RecordingSize.Target(1920, 1080, "Original") == (1920, 1080), "recsize: Original passes through");
            Check(RecordingSize.Target(1080, 1920, "720p") == (404, 720), "recsize: portrait scales by height, stays even");
            Check(RecordingSize.Target(1919, 1079, "Original") == (1918, 1078), "recsize: odd dimensions go even");
            Check(RecordingSize.Sanitize("720p") == "720p" && RecordingSize.Sanitize("4K") == "Original" && RecordingSize.Sanitize(null) == "Original", "recsize: sanitize falls back to Original");
        }

        Console.WriteLine("FfmpegPlan");
        {
            Check(FfmpegPlan.Presets.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == FfmpegPlan.Presets.Count, "ffmpeg: preset names are unique");
            Check(FfmpegPlan.Presets.All(p => p.Args.Contains("{in}") && p.Args.Contains("{out}")), "ffmpeg: every preset takes {in} and {out}");
            Check(FfmpegPlan.Presets.All(p => !p.Args.Any(a => a.Contains("libx264") || a.Contains("libx265"))), "ffmpeg: no GPL-only encoders in the LGPL matrix");
            Check(FfmpegPlan.TryBuildArgs("webm", "in file.mp4", "out.webm", out var webmArgs)
                && webmArgs[1] == "in file.mp4" && webmArgs[^1] == "out.webm" && webmArgs.Contains("libvpx-vp9"), "ffmpeg: args substitute in/out as whole tokens");
            Check(!FfmpegPlan.TryBuildArgs("nope", "a", "b", out _), "ffmpeg: unknown preset rejected");
            Check(FfmpegPlan.Find("GIFHQ")?.OutputExtension == "gif", "ffmpeg: find is case-insensitive");
            var inPath = Path.Combine("rec dir", "clip.mp4");
            Check(FfmpegPlan.DefaultOutputPath(FfmpegPlan.Find("mp3")!, inPath) == Path.Combine("rec dir", "clip.mp3"), "ffmpeg: default output path swaps the extension");
            var custom = FfmpegPlan.ParseCustomArgs("-i %in% -vf \"scale=320:-1\" -y %out%", "a b.mp4", "c.gif");
            Check(custom.SequenceEqual(new[] { "-i", "a b.mp4", "-vf", "scale=320:-1", "-y", "c.gif" }), "ffmpeg: custom template tokenizes quotes and expands placeholders");
            Check(FfmpegPlan.ParseCustomArgs("", "a", "b").Length == 0 && FfmpegPlan.ParseCustomArgs(null, "a", "b").Length == 0, "ffmpeg: empty custom template yields no args");
            Check(WorkflowPlan.TryParseStep("format: webm", out var webmStep) && webmStep.Arg == "webm" && WorkflowPlan.IsFfmpegFormat(webmStep), "workflow: format accepts an FFmpeg preset for recordings");
            Check(WorkflowPlan.TryParseStep("format: GIFHQ", out var gifhqStep) && gifhqStep.Arg == "gifhq" && WorkflowPlan.IsFfmpegFormat(gifhqStep), "workflow: preset names canonicalize case-insensitively");
            Check(WorkflowPlan.TryParseStep("format: jpg", out var jpgStep) && !WorkflowPlan.IsFfmpegFormat(jpgStep), "workflow: still formats stay still formats");
            Check(!WorkflowPlan.TryParseStep("format: bogus", out _), "workflow: unknown format still rejected");
            Check(WorkflowPlan.IsRecordingFile("a/b.MP4") && WorkflowPlan.IsRecordingFile("x.gif") && !WorkflowPlan.IsRecordingFile("x.png") && !WorkflowPlan.IsRecordingFile(null), "workflow: recording-file detection");
        }

        Console.WriteLine("ClickGate (tool selection never captures)");
        var gate = new ClickGate();
        gate.Arm(true);
        Check(!gate.Release(), "gate: the release ending the arming click is swallowed");
        gate.Press();
        Check(gate.Release(), "gate: the next full click of the user's own may confirm");
        var gate2 = new ClickGate();
        gate2.Arm(false);
        Check(gate2.Release(), "gate: keyboard-armed picker confirms on its first release");
        var gate3 = new ClickGate();
        gate3.Arm(true);
        gate3.Press();   // the inherited release was consumed by the old handler; a fresh press starts a fresh gesture
        Check(gate3.Release(), "gate: a fresh press before any release clears the wait");

        Console.WriteLine("RegionCrop (recording region resolution)");
        var rcMons = new[]
        {
            new MonitorInfo(1, new RectPx(0, 0, 1920, 1080), 96, true, "M1"),
            new MonitorInfo(2, new RectPx(1920, 0, 1920, 1080), 96, false, "M2"),
        };
        Check(RegionCrop.Resolve(new RectPx(100, 100, 800, 600), rcMons) is { MonitorIndex: 0 } rc1 && rc1.Crop == new RectPx(100, 100, 800, 600), "regioncrop: inside one monitor, crop is monitor-relative");
        Check(RegionCrop.Resolve(new RectPx(1800, 100, 400, 300), rcMons) is { MonitorIndex: 1 } rc2 && rc2.Crop == new RectPx(0, 100, 280, 300), "regioncrop: spanning picks the larger overlap and crops to it");
        Check(RegionCrop.Resolve(new RectPx(5000, 100, 400, 300), rcMons) is null, "regioncrop: off every monitor resolves to null");
        Check(RegionCrop.Resolve(RectPx.Empty, rcMons) is null, "regioncrop: empty rect resolves to null");
        Check(new Preferences { RecordingRegion = new[] { 1, 2 } }.Sanitize().RecordingRegion is null
           && new Preferences { RecordingRegion = new[] { 0, 0, 640, 480 } }.Sanitize().RecordingRegion is { Length: 4 }, "preferences: recording region sanitized like the capture rect");

        Console.WriteLine("Streaming audio (WavReader + PcmResampler)");
        {
            // Deterministic pseudo-random stereo samples.
            var src = new short[5000 * 2];
            uint seed = 12345;
            for (int i = 0; i < src.Length; i++) { seed = seed * 1664525 + 1013904223; src[i] = (short)(seed >> 17); }

            using var ms = new MemoryStream();
            using (var w = new WavWriter(ms, 48000, 2)) { w.Write(src.AsSpan(0, 4000)); w.Write(src.AsSpan(4000)); }
            ms.Position = 0;
            using var reader = new WavReader(ms, leaveOpen: true);
            Check(reader.SampleRate == 48000 && reader.Channels == 2 && reader.TotalFrames == 5000, "wavreader: header fields and frame count");
            var back = new List<short>();
            var chunk = new short[777];   // deliberately not frame- or chunk-aligned with the writes
            int n; while ((n = reader.Read(chunk)) > 0) back.AddRange(chunk.AsSpan(0, n).ToArray());
            Check(back.SequenceEqual(src), "wavreader: odd-sized reads reproduce the samples exactly");

            foreach (var (from, to) in new[] { (48000, 44100), (44100, 48000), (48000, 48000), (16000, 48000) })
            {
                var whole = PcmMix.Resample(src, 2, from, to);
                foreach (int chunkFrames in new[] { 1, 7, 1024 })
                {
                    var rs = new PcmResampler(2, from, to);
                    var got = new List<short>();
                    for (int f = 0; f < 5000; f += chunkFrames)
                    {
                        int take = Math.Min(chunkFrames, 5000 - f) * 2;
                        got.AddRange(rs.Process(src.AsSpan(f * 2, take)));
                    }
                    got.AddRange(rs.Finish());
                    Check(got.SequenceEqual(whole), $"resampler: {from}→{to} in {chunkFrames}-frame chunks is sample-identical to whole-array");
                }
            }
        }

        Console.WriteLine("Pause clock + WAV concatenation");
        {
            TimeSpan S(double s) => TimeSpan.FromSeconds(s);
            var clock = new PauseClock();
            Check(!clock.IsPaused && clock.MediaTime(S(5)) == S(5), "pauseclock: running media time equals wall time");
            clock.Pause(S(5));
            Check(clock.IsPaused && clock.MediaTime(S(9)) == S(5), "pauseclock: media time freezes at the pause point");
            clock.Pause(S(6));
            Check(clock.MediaTime(S(9)) == S(5), "pauseclock: a second pause while paused is a no-op");
            clock.Resume(S(9));
            Check(!clock.IsPaused && clock.MediaTime(S(9)) == S(5) && clock.MediaTime(S(11)) == S(7), "pauseclock: media time resumes where it froze");
            clock.Resume(S(10));
            Check(clock.MediaTime(S(11)) == S(7), "pauseclock: resume while running is a no-op");
            clock.Pause(S(12)); clock.Resume(S(14));
            Check(clock.MediaTime(S(20)) == S(14) && clock.PausedTotal(S(20)) == S(6), "pauseclock: multiple spans accumulate (4 s + 2 s)");

            // Two WAV segments joined end-to-end reproduce the concatenated samples.
            var segA = new short[2000]; var segB = new short[3000];
            for (int i = 0; i < segA.Length; i++) segA[i] = (short)(i % 997);
            for (int i = 0; i < segB.Length; i++) segB[i] = (short)(3000 - i % 991);
            using var inA = new MemoryStream(); using (var w = new WavWriter(inA, 44100, 2)) w.Write(segA);
            using var inB = new MemoryStream(); using (var w = new WavWriter(inB, 44100, 2)) w.Write(segB);
            using var joined = new MemoryStream();
            inA.Position = 0; inB.Position = 0;
            WavConcat.Concatenate(new Stream[] { inA, inB }, joined);
            joined.Position = 0;
            using var jr = new WavReader(joined, leaveOpen: true);
            Check(jr.SampleRate == 44100 && jr.TotalFrames == 2500, "wavconcat: joined header keeps rate, frames add up");
            var all = new List<short>(); var buf = new short[1024];
            int n; while ((n = jr.Read(buf)) > 0) all.AddRange(buf.AsSpan(0, n).ToArray());
            Check(all.SequenceEqual(segA.Concat(segB)), "wavconcat: joined samples are exactly segment A then B");
            using var inC = new MemoryStream(); using (var w = new WavWriter(inC, 48000, 2)) w.Write(segA);
            inA.Position = 0; inC.Position = 0;
            bool concatThrew = false;
            try { WavConcat.Concatenate(new Stream[] { inA, inC }, new MemoryStream()); } catch (InvalidDataException) { concatThrew = true; }
            Check(concatThrew, "wavconcat: mismatched sample rates are rejected");
        }


    }
}
