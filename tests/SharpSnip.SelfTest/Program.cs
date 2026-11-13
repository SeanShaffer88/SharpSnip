using SharpSnip.Core;

// Dependency-free logic tests. Run: dotnet run --project tests/SharpSnip.SelfTest
int passed = 0, failed = 0;

void Check(bool ok, string name)
{
    if (ok) { passed++; Console.WriteLine("  ok   " + name); }
    else { failed++; Console.WriteLine("  FAIL " + name); }
}

// Mixed-DPI, L-shaped layout: 1920x1080@100% at origin, 2560x1440@150% to the right, raised by 300px.
var m1 = new MonitorInfo(1, new RectPx(0, 0, 1920, 1080), 96, true, "A");
var m2 = new MonitorInfo(2, new RectPx(1920, -300, 2560, 1440), 144, false, "B");
var mons = new[] { m1, m2 };

Console.WriteLine("RectPx");
Check(new RectPx(0, 0, 10, 10).Intersect(new RectPx(5, 5, 10, 10)) == new RectPx(5, 5, 5, 5), "intersect overlap");
Check(new RectPx(0, 0, 10, 10).Intersect(new RectPx(10, 0, 5, 5)).IsEmpty, "touching edges do not intersect");
Check(new RectPx(0, 0, 10, 10).Union(new RectPx(20, 20, 5, 5)) == new RectPx(0, 0, 25, 25), "union");
Check(RectPx.Empty.Union(new RectPx(1, 2, 3, 4)) == new RectPx(1, 2, 3, 4), "union with empty");
Check(RectPx.FromCorners(10, 10, 0, 0) == new RectPx(0, 0, 10, 10), "corners normalise");
Check(new RectPx(0, 0, 10, 10).Contains(9, 9) && !new RectPx(0, 0, 10, 10).Contains(10, 9), "contains is half-open");

Console.WriteLine("VirtualDesktop");
Check(VirtualDesktop.Union(mons) == new RectPx(0, -300, 4480, 1440), "union rect spans both monitors");
Check(m2.ScalePercent == 150 && m2.Label.StartsWith("2 · 2560×1440 · 150%"), "monitor label");
Check(m1.Label.EndsWith("PRIMARY"), "primary flag in label");
var allRect = VirtualDesktop.ResolveTarget(FullScreenTarget.All, mons, 0, 0);
Check(allRect == new RectPx(0, -300, 4480, 1440), "resolve AllMonitors");
Check(VirtualDesktop.ResolveTarget(FullScreenTarget.Current, mons, 3000, 100) == m2.Bounds, "resolve CurrentMonitor under cursor");
Check(VirtualDesktop.ResolveTarget(FullScreenTarget.Current, mons, 99999, 0) == m2.Bounds, "cursor off-screen snaps to nearest");
Check(VirtualDesktop.ResolveTarget(FullScreenTarget.Monitor(1), mons, 0, 0) == m1.Bounds, "resolve Monitor(1)");
Check(VirtualDesktop.ResolveTarget(FullScreenTarget.Monitor(3), mons, 0, 0) is null, "missing monitor resolves to null");
Check(VirtualDesktop.ResolveTarget(FullScreenTarget.All, Array.Empty<MonitorInfo>(), 0, 0) is null, "no monitors resolves to null");
var places = VirtualDesktop.Placements(allRect!.Value, mons);
Check(places.Count == 2, "two placements");
Check(places[0].DestX == 0 && places[0].DestY == 300, "monitor 1 dest offset");
Check(places[1].DestX == 1920 && places[1].DestY == 0, "monitor 2 dest offset");
Check(places.Sum(p => p.Source.Area) == 1920L * 1080 + 2560L * 1440, "no overlap, 1:1 pixels");
Check(VirtualDesktop.HasGaps(allRect.Value, mons), "L-shaped layout reports gaps");
Check(!VirtualDesktop.HasGaps(m1.Bounds, mons), "single monitor has no gaps");
var flat = new[] { m1, new MonitorInfo(2, new RectPx(1920, 0, 1920, 1080), 96, false, "C") };
Check(!VirtualDesktop.HasGaps(VirtualDesktop.Union(flat), flat), "flat layout has no gaps");
var span = VirtualDesktop.Placements(new RectPx(1800, 0, 400, 100), mons);
Check(span.Count == 2 && span[0].Source.Width == 120 && span[1].Source.Width == 280, "region spanning two monitors splits correctly");

Console.WriteLine("FullScreenTarget");
Check(FullScreenTarget.Current.Cycle(3, 1, +1) == FullScreenTarget.Monitor(2), "tab from current monitor 1 -> 2");
Check(FullScreenTarget.Monitor(3).Cycle(3, 1, +1) == FullScreenTarget.Monitor(1), "tab wraps forward");
Check(FullScreenTarget.Monitor(1).Cycle(3, 1, -1) == FullScreenTarget.Monitor(3), "shift+tab wraps backward");
Check(FullScreenTarget.All.Cycle(3, 2, +1) == FullScreenTarget.Monitor(1), "tab from all -> monitor 1");
Check(FullScreenTarget.All.Cycle(3, 2, -1) == FullScreenTarget.Monitor(3), "shift+tab from all -> last");
Check(FullScreenTarget.Parse("Monitor:2") == FullScreenTarget.Monitor(2), "parse monitor");
Check(FullScreenTarget.Parse("garbage") == FullScreenTarget.Current, "parse garbage -> current");
Check(FullScreenTarget.Parse("Monitor:12") == FullScreenTarget.Current, "parse out-of-range -> current");
Check(FullScreenTarget.Parse(FullScreenTarget.All.Format()) == FullScreenTarget.All, "format/parse round trip");

Console.WriteLine("DelayCycle");
Check(DelayCycle.Next(0) == 3 && DelayCycle.Next(3) == 5 && DelayCycle.Next(5) == 10 && DelayCycle.Next(10) == 0, "0>3>5>10>0");
Check(DelayCycle.Next(7) == 0, "unknown value restarts at 0");
Check(DelayCycle.Sanitize(4) == 0 && DelayCycle.Sanitize(5) == 5, "sanitize");

Console.WriteLine("HotkeySpec");
Check(HotkeySpec.TryParse("Win+Shift+X", out var h) && h.Mods == (HotkeyMods.Win | HotkeyMods.Shift) && h.VirtualKey == 'X', "parse Win+Shift+X");
Check(HotkeySpec.TryParse("ctrl + alt + 5", out h) && h.VirtualKey == '5', "parse with spaces/case");
Check(HotkeySpec.TryParse("PrintScreen", out h) && h.VirtualKey == 0x2C && h.Mods == HotkeyMods.None, "bare PrintScreen allowed");
Check(HotkeySpec.TryParse("F13", out h) && h.VirtualKey == 0x7C, "bare F-key allowed");
Check(!HotkeySpec.TryParse("X", out _), "bare letter rejected");
Check(!HotkeySpec.TryParse("Win+Shift", out _), "modifiers only rejected");
Check(!HotkeySpec.TryParse("Win+A+B", out _), "two keys rejected");
Check(!HotkeySpec.TryParse("", out _) && !HotkeySpec.TryParse(null, out _), "empty rejected");
Check(HotkeySpec.TryParse("Win+Shift+F", out h) && h.ToString() == "Shift+Win+F", "canonical ToString");

Console.WriteLine("RegionGeometry");
var r = new RectPx(100, 100, 200, 100);
Check(RegionGeometry.HitTest(r, 100, 100, 6) == RegionHandle.TopLeft, "hit top-left");
Check(RegionGeometry.HitTest(r, 300, 200, 6) == RegionHandle.BottomRight, "hit bottom-right");
Check(RegionGeometry.HitTest(r, 200, 98, 6) == RegionHandle.Top, "hit top edge");
Check(RegionGeometry.HitTest(r, 200, 150, 6) == RegionHandle.Body, "hit body");
Check(RegionGeometry.HitTest(r, 500, 500, 6) == RegionHandle.None, "miss");
Check(RegionGeometry.Resize(r, RegionHandle.BottomRight, 400, 300) == new RectPx(100, 100, 300, 200), "resize bottom-right");
Check(RegionGeometry.Resize(r, RegionHandle.Left, 50, 0) == new RectPx(50, 100, 250, 100), "resize left");
Check(RegionGeometry.Resize(r, RegionHandle.Right, 40, 0) == new RectPx(40, 100, 60, 100), "resize past opposite edge flips");
Check(RegionGeometry.Move(r, -500, 0, new RectPx(0, 0, 1000, 1000)).X == 0, "move clamps left");
Check(RegionGeometry.Move(r, 5000, 5000, new RectPx(0, 0, 1000, 1000)) == new RectPx(800, 900, 200, 100), "move clamps right/bottom");
Check(RegionGeometry.Snap(103, new[] { 100, 500 }, 8) == 100, "snap within tolerance");
Check(RegionGeometry.Snap(120, new[] { 100, 500 }, 8) == 120, "no snap outside tolerance");

Console.WriteLine("AppStateMachine");
var sm = new AppStateMachine();
Check(sm.State == AppState.Idle, "starts idle");
Check(!sm.TryTransition(AppState.Editing), "idle -> editing refused");
Check(sm.TryTransition(AppState.ModeMenu) && sm.TryTransition(AppState.ElementPicking), "menu -> element");
Check(sm.TryTransition(AppState.RegionPicking) && sm.TryTransition(AppState.FullScreenPicking), "picker hopping");
Check(sm.TryTransition(AppState.ModeMenu), "picker -> menu (Esc)");
Check(sm.TryTransition(AppState.WindowPicking) && sm.TryTransition(AppState.Captured), "window -> captured");
Check(sm.TryTransition(AppState.Editing) && sm.TryTransition(AppState.Exporting) && sm.TryTransition(AppState.Idle), "captured -> editing -> exporting -> idle");
Check(sm.TryTransition(AppState.FullScreenInstant) && sm.TryTransition(AppState.Exporting) && sm.TryTransition(AppState.Idle), "instant skips editor");
Check(!new AppStateMachine().TryTransition(AppState.Captured), "idle -> captured refused");

Console.WriteLine("Preferences");
var p = new Preferences { ImageFormat = "webp", HotkeyMenu = "nonsense", DefaultDelaySeconds = 4, DimmerOpacity = 5, MatteColor = "Pink", FullScreenTarget = "Monitor:3", EditorBounds = new[] { 1, 2 } }.Sanitize();
Check(p.ImageFormat == "png" && p.HotkeyMenu == "Win+Shift+X" && p.DefaultDelaySeconds == 0, "sanitize format/hotkey/delay");
Check(p.DimmerOpacity == 0.9 && p.MatteColor == "Transparent" && p.EditorBounds is null, "sanitize opacity/matte/bounds");
Check(p.FullScreenTarget == "CurrentMonitor", "persisted target is never a specific monitor index");
Check(new Preferences { MatteColor = "Black" }.MatteArgb == 0xFF000000u && new Preferences().MatteArgb == 0u, "matte argb");

string dir = Path.Combine(Path.GetTempPath(), "sharpsnip-selftest-" + Guid.NewGuid().ToString("N"));
string file = Path.Combine(dir, "prefs.json");
try
{
    var fresh = PrefsStore.Load(file, out var w0);
    Check(w0 is null && fresh.HotkeyMenu == "Win+Shift+X", "missing file -> defaults, no warning");
    PrefsStore.Save(file, new Preferences { ImageFormat = "jpg", DefaultDelaySeconds = 5, FullScreenTarget = "AllMonitors" });
    var back = PrefsStore.Load(file, out var w1);
    Check(w1 is null && back.ImageFormat == "jpg" && back.DefaultDelaySeconds == 5 && back.FullScreenTarget == "AllMonitors", "save/load round trip");
    PrefsStore.Save(file, new Preferences { ImageFormat = "png" });
    Check(PrefsStore.Load(file, out _).ImageFormat == "png" && !File.Exists(file + ".tmp"), "overwrite is atomic, no temp left");
    File.WriteAllText(file, "{ not json");
    var quarantined = PrefsStore.Load(file, out var w2);
    Check(w2 is not null && quarantined.HotkeyMenu == "Win+Shift+X" && !File.Exists(file)
          && Directory.GetFiles(dir, "prefs.json.bad-*").Length == 1, "corrupt file quarantined, defaults loaded");
    File.WriteAllText(file, "{ \"ImageFormat\": \"jpg\", // comment\n \"Unknown\": 1, }");
    Check(PrefsStore.Load(file, out var w3).ImageFormat == "jpg" && w3 is null, "comments, trailing commas, unknown keys tolerated");
}
finally { try { Directory.Delete(dir, true); } catch (IOException) { } }

Console.WriteLine("Hotkey defaults");
var defs = new Preferences();
var all4 = new[] { defs.HotkeyMenu, defs.HotkeyFullScreen, defs.HotkeyActiveWindow, defs.HotkeyRepeatLast };
Check(all4.All(k => HotkeySpec.TryParse(k, out _)), "all four default hotkeys parse");
Check(all4.Select(k => { HotkeySpec.TryParse(k, out var sp); return sp; }).Distinct().Count() == 4, "default hotkeys are all different");
var bad = new Preferences { HotkeyActiveWindow = "x", HotkeyRepeatLast = "" }.Sanitize();
Check(bad.HotkeyActiveWindow == "Win+Shift+W" && bad.HotkeyRepeatLast == "Win+Shift+R", "bad new hotkeys fall back to defaults");
string oldFile = Path.Combine(Path.GetTempPath(), "sharpsnip-v01-" + Guid.NewGuid().ToString("N") + ".json");
File.WriteAllText(oldFile, "{ \"SchemaVersion\": 1, \"HotkeyMenu\": \"Win+Shift+X\", \"ImageFormat\": \"jpg\" }");
var migrated = PrefsStore.Load(oldFile, out var wOld);
Check(wOld is null && migrated.ImageFormat == "jpg" && migrated.HotkeyActiveWindow == "Win+Shift+W", "v0.1 settings file loads and gains new defaults");
File.Delete(oldFile);

Console.WriteLine("FixedRegion");
var desk = new RectPx(0, -300, 4480, 1440);
Check(FixedRegion.Place(1280, 720, 2000, 400, desk) == new RectPx(1360, 40, 1280, 720), "centred on cursor");
Check(FixedRegion.Place(1280, 720, 0, -300, desk) == new RectPx(0, -300, 1280, 720), "clamped to top-left corner");
Check(FixedRegion.Place(1280, 720, 99999, 99999, desk) == new RectPx(3200, 420, 1280, 720), "clamped to bottom-right corner");
Check(FixedRegion.Place(9000, 9000, 10, 10, desk) == desk, "oversized request shrinks to the desktop");
Check(FixedRegion.ClampSize(1) == 16 && FixedRegion.ClampSize(99999) == 16384 && FixedRegion.ClampSize(800) == 800, "size clamp");

Console.WriteLine("IntervalPlan");
Check(IntervalPlan.SanitizeSeconds(0) == 1 && IntervalPlan.SanitizeSeconds(999999) == 86400 && IntervalPlan.SanitizeSeconds(30) == 30, "seconds clamp");
Check(IntervalPlan.FolderName(new DateTime(2026, 11, 12, 9, 8, 7)) == "Interval-20261112-090807", "folder name");
Check(IntervalPlan.FrameName(7, ".png") == "frame-00007.png", "frame name");
Check(string.CompareOrdinal(IntervalPlan.FrameName(9, "png"), IntervalPlan.FrameName(10, "png")) < 0, "frames sort in order");

Console.WriteLine("HotkeyList");
Check(HotkeyList.TryParse(HotkeyList.Default, out var hl) && hl.Count == 4, "default list parses to 4");
Check(HotkeyList.TryParse("", out hl) && hl.Count == 0, "empty list is valid");
Check(!HotkeyList.TryParse("Ctrl+Alt+1;Ctrl+Alt+1", out _), "duplicates rejected");
Check(!HotkeyList.TryParse("Ctrl+Alt+1;nonsense", out hl) && hl.Count == 0, "bad entry rejected");
Check(!HotkeyList.TryParse(string.Join(';', Enumerable.Range(1, 10).Select(i => "Ctrl+Alt+Shift+F" + i)), out _), "more than 9 rejected");
Check(HotkeyList.TryParse(" Ctrl+Alt+1 ; Ctrl+Alt+2 ;", out hl) && hl.Count == 2, "spaces and trailing separator tolerated");
var pk = new Preferences { HotkeyMonitors = "junk", FixedWidth = 1, FixedHeight = 99999, IntervalSeconds = 0 }.Sanitize();
Check(pk.HotkeyMonitors == HotkeyList.Default && pk.FixedWidth == 16 && pk.FixedHeight == 16384 && pk.IntervalSeconds == 1, "sanitize new fields");
var allKeys = new[] { defs.HotkeyMenu, defs.HotkeyFullScreen, defs.HotkeyActiveWindow, defs.HotkeyRepeatLast }
    .Concat(HotkeyList.Default.Split(';')).Select(k => { HotkeySpec.TryParse(k, out var sp); return sp; });
Check(allKeys.Distinct().Count() == 8, "monitor hotkey defaults do not clash with the other four");
Check(AppStateMachine.ForMode(PickMode.FixedRegion) == AppState.RegionPicking, "fixed region counts as region picking");

Console.WriteLine("ImageFormats");
Check(ImageFormats.Normalize("PNG") == "png" && ImageFormats.Normalize(".Jpg") == "jpg", "normalize case and leading dot");
Check(ImageFormats.Normalize("jpeg") == "jpg" && ImageFormats.Normalize(".JPEG") == "jpg", "jpeg alias canonicalises to jpg");
Check(ImageFormats.Normalize("TIFF") == "tif" && ImageFormats.Normalize(".tiff") == "tif", "tiff alias canonicalises to tif");
Check(ImageFormats.Normalize(" bmp ") == "bmp" && ImageFormats.Normalize("gif") == "gif" && ImageFormats.Normalize("tif") == "tif", "bmp/gif/tif supported");
Check(ImageFormats.Normalize("webp") is null && ImageFormats.Normalize("") is null && ImageFormats.Normalize(null) is null, "unsupported/empty format -> null");
Check(ImageFormats.All.SequenceEqual(new[] { "png", "jpg", "bmp", "gif", "tif", "pdf", "apng" }), "format list is png/jpg/bmp/gif/tif/pdf/apng");
Check(ImageFormats.FromPath(@"C:\shots\a.BMP", "png") == "bmp" && ImageFormats.FromPath("a.jpeg", "png") == "jpg", "format from path extension");
Check(ImageFormats.FromPath("a.tiff", "png") == "tif" && ImageFormats.FromPath("a.gif", "jpg") == "gif", "tiff/gif path extensions");
Check(ImageFormats.FromPath("a.webp", "bmp") == "bmp" && ImageFormats.FromPath("noext", "gif") == "gif", "unknown/missing extension falls back to preference");
Check(ImageFormats.FromPath("noext", "junk") == "png", "invalid fallback falls back to png");
Check(ImageFormats.FlattensAlpha("jpg") && ImageFormats.FlattensAlpha("bmp") && ImageFormats.FlattensAlpha("gif")
      && !ImageFormats.FlattensAlpha("png") && !ImageFormats.FlattensAlpha("tif"), "alpha flattened for jpg/bmp/gif (pdf checked with its own section)");
Check(new Preferences { ImageFormat = "bmp" }.Sanitize().Extension == "bmp"
      && new Preferences { ImageFormat = "GIF" }.Sanitize().ImageFormat == "gif", "sanitize: bmp kept, case normalised");
Check(new Preferences { ImageFormat = "jpeg" }.Sanitize().Extension == "jpg"
      && new Preferences { ImageFormat = "tiff" }.Sanitize().Extension == "tif"
      && new Preferences { ImageFormat = "webp" }.Sanitize().Extension == "png", "sanitize: aliases canonicalise, unsupported -> png");
Check(OutputNaming.FileName(new DateTime(2026, 11, 12, 9, 8, 7, 6), "jpeg") == "20261112-090807-006.jpg"
      && OutputNaming.FileName(new DateTime(2026, 11, 12, 9, 8, 7, 6), ".GIF") == "20261112-090807-006.gif"
      && OutputNaming.FileName(new DateTime(2026, 11, 12, 9, 8, 7, 6), "bogus") == "20261112-090807-006.png", "file names use canonical extension");
Check(IntervalPlan.FrameName(3, "tiff") == "frame-00003.tif", "interval frame name canonicalises extension");

Console.WriteLine("OutputNaming");
Check(OutputNaming.FileName(new DateTime(2026, 11, 12, 9, 8, 7, 6), "png") == "20261112-090807-006.png", "timestamp name");
Check(OutputNaming.ResolveFolder("", @"C:\Users\x\Pictures") == @"C:\Users\x\Pictures" && OutputNaming.ResolveFolder("  ", @"C:\Users\x\Pictures") == @"C:\Users\x\Pictures" && OutputNaming.ResolveFolder("D:\\shots", "x") == "D:\\shots", "output folder resolution: unset = Pictures, set wins");

Console.WriteLine("GifPlan");
Check(GifPlan.DelayHundredths(5) == 500 && GifPlan.DelayHundredths(1) == 100, "delay seconds -> hundredths");
Check(GifPlan.DelayHundredths(0) == GifPlan.MinDelayHundredths && GifPlan.DelayHundredths(10_000) == 65_535, "delay clamped to GIF field range");
Check(GifPlan.FileName == "animation.gif" && GifPlan.IsAnimatedFormat("gif") && !GifPlan.IsAnimatedFormat("png"), "animated run only for gif");

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
string lrFile = Path.Combine(Path.GetTempPath(), "sharpsnip-lr-" + Guid.NewGuid().ToString("N") + ".json");
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

Console.WriteLine("PrintScreenPlan");
{
    Check(PrintScreenPlan.IsPrintScreen("PrintScreen") && PrintScreenPlan.IsPrintScreen("printscreen"), "prtsc plan: recognises Print Screen, any case");
    Check(!PrintScreenPlan.IsPrintScreen("Ctrl+PrintScreen") && !PrintScreenPlan.IsPrintScreen("Win+Shift+X") && !PrintScreenPlan.IsPrintScreen(null), "prtsc plan: modified/other keys are not Print Screen");
    var (menu, rem) = PrintScreenPlan.Enable("Win+Shift+X", "");
    Check(menu == "PrintScreen" && rem == "Win+Shift+X", "prtsc plan: enable points menu at Print Screen and remembers the old key");
    (menu, rem) = PrintScreenPlan.Enable("PrintScreen", "Win+Shift+X");
    Check(menu == "PrintScreen" && rem == "Win+Shift+X", "prtsc plan: enable while already on Print Screen keeps the remembered key");
    (menu, rem) = PrintScreenPlan.Enable("Win+Shift+Z", "Win+Shift+Q");
    Check(rem == "Win+Shift+Q", "prtsc plan: re-enable never overwrites the original remembered key");
    Check(PrintScreenPlan.Disable("PrintScreen", "Win+Shift+X") == "Win+Shift+X", "prtsc plan: disable restores the remembered key");
    Check(PrintScreenPlan.Disable("PrintScreen", "") == "PrintScreen", "prtsc plan: disable with nothing remembered keeps the current key");
    Check(PrintScreenPlan.Disable("Win+Shift+Z", "junk!!") == "Win+Shift+Z", "prtsc plan: disable ignores an invalid remembered key");
    var sp = new Preferences { PrintScreenSnippingPrior = 7, HotkeyMenuBeforeTakeover = "junk!!" }.Sanitize();
    Check(sp.PrintScreenSnippingPrior == 1 && sp.HotkeyMenuBeforeTakeover == "", "prefs sanitize: prior clamps to [-1,1], invalid remembered key dropped");
    sp = new Preferences { PrintScreenSnippingPrior = -5, HotkeyMenuBeforeTakeover = "Win+Shift+X" }.Sanitize();
    Check(sp.PrintScreenSnippingPrior == -1 && sp.HotkeyMenuBeforeTakeover == "Win+Shift+X", "prefs sanitize: -1 (value absent) and a valid remembered key survive");
    Check(new Preferences().ShowTrayIcon && new Preferences().PrintScreenTakeover, "prefs defaults: tray icon shown, Print Screen takeover ON by default");
}

Console.WriteLine("ModeBarLayout");
{
    Check(ModeBarLayout.BarWidth(6) == 6 + 6 * 76 + 13 + 46 + 6, "mode bar: width = padding + buttons + separator + close");
    Check(ModeBarLayout.BarWidth(0) == 0, "mode bar: no buttons -> no bar");
    var hit = ModeBarLayout.HitTest(ModeBarLayout.ButtonLeft(0) + 38, 46, 6);
    Check(hit is { Index: 0, IsClose: false }, "mode bar: first button centre hits button 0");
    hit = ModeBarLayout.HitTest(ModeBarLayout.ButtonLeft(5) + 75.9, 46, 6);
    Check(hit is { Index: 5, IsClose: false }, "mode bar: last button's right edge still hits it");
    Check(ModeBarLayout.HitTest(ModeBarLayout.ButtonLeft(6) + 6, 46, 6) is null, "mode bar: separator hits nothing");
    hit = ModeBarLayout.HitTest(ModeBarLayout.CloseLeft(6) + 23, 46, 6);
    Check(hit is { IsClose: true }, "mode bar: close zone hits close");
    Check(ModeBarLayout.HitTest(ModeBarLayout.CloseLeft(6) + 46, 46, 6) is null, "mode bar: past close hits nothing");
    Check(ModeBarLayout.HitTest(40, ModeBarLayout.BarHeight, 6) is null && ModeBarLayout.HitTest(40, -1, 6) is null, "mode bar: above/below hits nothing");
    Check(ModeBarLayout.HitTest(3, 46, 6) is null, "mode bar: left padding hits nothing");
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

Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
