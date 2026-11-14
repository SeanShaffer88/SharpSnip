using Snipit.Core;

namespace Snipit.SelfTest;

// Settings checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Settings()
    {
        Console.WriteLine("Preferences");
        var p = new Preferences { ImageFormat = "webp", DefaultDelaySeconds = 4, DimmerOpacity = 5, MatteColor = "Pink", FullScreenTarget = "Monitor:3", EditorBounds = new[] { 1, 2 } }.Sanitize();
        Check(p.ImageFormat == "png" && p.DefaultDelaySeconds == 0, "sanitize format/delay");
        Check(p.DimmerOpacity == 0.9 && p.MatteColor == "Transparent" && p.EditorBounds is null, "sanitize opacity/matte/bounds");
        Check(p.FullScreenTarget == "CurrentMonitor", "persisted target is never a specific monitor index");
        Check(new Preferences { MatteColor = "Black" }.MatteArgb == 0xFF000000u && new Preferences().MatteArgb == 0u, "matte argb");

        string dir = Path.Combine(Path.GetTempPath(), "snipit-selftest-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "prefs.json");
        try
        {
            var fresh = PrefsStore.Load(file, out var w0);
            Check(w0 is null && fresh.ImageFormat == "png", "missing file -> defaults, no warning");
            PrefsStore.Save(file, new Preferences { ImageFormat = "jpg", DefaultDelaySeconds = 5, FullScreenTarget = "AllMonitors" });
            var back = PrefsStore.Load(file, out var w1);
            Check(w1 is null && back.ImageFormat == "jpg" && back.DefaultDelaySeconds == 5 && back.FullScreenTarget == "AllMonitors", "save/load round trip");
            PrefsStore.Save(file, new Preferences { ImageFormat = "png" });
            Check(PrefsStore.Load(file, out _).ImageFormat == "png" && !File.Exists(file + ".tmp"), "overwrite is atomic, no temp left");
            File.WriteAllText(file, "{ not json");
            var quarantined = PrefsStore.Load(file, out var w2);
            Check(w2 is not null && quarantined.ImageFormat == "png" && !File.Exists(file)
                  && Directory.GetFiles(dir, "prefs.json.bad-*").Length == 1, "corrupt file quarantined, defaults loaded");
            File.WriteAllText(file, "{ \"ImageFormat\": \"jpg\", // comment\n \"Unknown\": 1, }");
            Check(PrefsStore.Load(file, out var w3).ImageFormat == "jpg" && w3 is null, "comments, trailing commas, unknown keys tolerated");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }

        Console.WriteLine("Print Screen override record");
        var defs = new Preferences();
        Check(!defs.PrintScreenPriorRecorded && defs.PrintScreenSnippingPrior == -1, "override record starts unrecorded, prior -1");
        string oldFile = Path.Combine(Path.GetTempPath(), "snipit-v01-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(oldFile, "{ \"SchemaVersion\": 1, \"HotkeyMenu\": \"Win+Shift+X\", \"PrintScreenTakeover\": true, \"ImageFormat\": \"jpg\" }");
        var migrated = PrefsStore.Load(oldFile, out var wOld);
        Check(wOld is null && migrated.ImageFormat == "jpg", "v0.1 settings file loads (removed hotkey keys are ignored)");
        File.Delete(oldFile);
        string recFile = Path.Combine(Path.GetTempPath(), "snipit-rec-" + Guid.NewGuid().ToString("N") + ".json");
        PrefsStore.Save(recFile, new Preferences { PrintScreenPriorRecorded = true, PrintScreenSnippingPrior = 1 });
        var recBack = PrefsStore.Load(recFile, out _);
        Check(recBack.PrintScreenPriorRecorded && recBack.PrintScreenSnippingPrior == 1, "override record round-trips through save/load");
        File.Delete(recFile);

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

        var pk = new Preferences { FixedWidth = 1, FixedHeight = 99999, IntervalSeconds = 0 }.Sanitize();
        Check(pk.FixedWidth == 16 && pk.FixedHeight == 16384 && pk.IntervalSeconds == 1, "sanitize new fields");
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

    }
}
