using Snipit.Core;

namespace Snipit.SelfTest;

// Hardening checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Hardening()
    {
        Console.WriteLine("Theme contrast (TesselGUI r3 palette, copied from TesselTheme.cs)");
        double Cr(string a, string b) { ContrastRatio.TryParse(a, out var ca); ContrastRatio.TryParse(b, out var cb); return ContrastRatio.Between(ca, cb); }
        Check(Math.Abs(Cr("#000000", "#FFFFFF") - 21.0) < 0.01, "contrast formula: black/white = 21");
        foreach (var surface in new[] { "#1B1B1D", "#1F1F22", "#232326", "#2A2A2F" })
        {
            Check(Cr("#E6E3E1", surface) >= 4.5, $"foreground text on {surface} >= 4.5");
            if (surface is "#1B1B1D" or "#1F1F22") Check(Cr("#89868A", surface) >= 4.5, $"muted text on {surface} >= 4.5 (Muted must not be used on raised/selected surfaces)");
            Check(Cr("#E8939F", surface) >= 3.0, $"focus ring (#E8939F) on {surface} >= 3");
        }
        Check(Cr("#E8939F", "#960018") >= 3.0, "focus ring on action button fill >= 3");
        Check(Cr("#FFFFFF", "#960018") >= 4.5 && Cr("#FFFFFF", "#7E0014") >= 4.5, "white text on action fills >= 4.5");
        Check(Cr("#960018", "#1B1B1D") < 3.0, "old focus colour #960018 stays below 3:1 (why it was replaced)");


        Console.WriteLine("Hardening: CLI args, workflow quoting and trust, log policy");
        var cargs = new[] { "--out", "--clean", "--fullscreen" };
        Check(!CliArgs.Has(cargs, "--clean") && CliArgs.Has(cargs, "--fullscreen") && CliArgs.Value(cargs, "--out") is null, "flag in a value slot is not a flag");
        Check(CliArgs.Value(new[] { "--out", @"C:\a b\x.png" }, "--out") == @"C:\a b\x.png" && CliArgs.Value(new[] { "--out" }, "--out") is null, "value lookup");
        Check(CliArgs.Has(new[] { "--FULLSCREEN" }, "--fullscreen") && CliArgs.TakesValue("--record") && !CliArgs.TakesValue("--all"), "case-insensitive; value flags known");
        string sep = Path.DirectorySeparatorChar.ToString(), spDir = sep + "My Shots", spFile = spDir + sep + "a.png", plainFile = sep + "s" + sep + "a.png";
        Check(WorkflowPlan.ExpandArgs("-i %path% -o %dir%" + sep + "out.png", spFile) == "-i \"" + spFile + "\" -o \"" + spDir + "\"" + sep + "out.png", "unquoted placeholders get quoted");
        Check(WorkflowPlan.ExpandArgs("\"%path%\"", spFile) == "\"" + spFile + "\"", "already-quoted placeholders stay raw");
        Check(WorkflowPlan.ExpandArgs("%name%.%ext%", plainFile) == "a.png" && WorkflowPlan.ExpandArgs("%unknown%", plainFile) == "%unknown%", "plain values unquoted; unknown placeholder untouched");
        Check(WorkflowPlan.QuoteArg(@"C:\dir with space\") == "\"C:\\dir with space\\\\\"", "trailing backslash doubled inside quotes");
        var tp = new Preferences { ExternalEditorPath = @"C:\e.exe", Workflows = new() { new WorkflowDef { Name = "w", Steps = new() { "run: x.exe|%path%" } } } };
        var tkey = new byte[32]; for (int i = 0; i < 32; i++) tkey[i] = (byte)(i + 1);
        var tkey2 = new byte[32]; for (int i = 0; i < 32; i++) tkey2[i] = (byte)(100 + i);
        Check(WorkflowTrust.IsTampered(tp, tkey), "blank trust hash with an editor and a run step is NOT trusted");
        Check(!WorkflowTrust.IsTampered(new Preferences(), tkey), "blank trust hash with nothing to launch is fine");
        Check(!WorkflowTrust.IsTampered(new Preferences { Workflows = new() { new WorkflowDef { Name = "c", Steps = new() { "copy: D:\\x", "effect: Grayscale" } } } }, tkey), "blank hash with only non-launching steps is fine");
        WorkflowTrust.Approve(tp, tkey);
        Check(!WorkflowTrust.IsTampered(tp, tkey), "approved settings are trusted");
        Check(WorkflowTrust.IsTampered(tp, tkey2), "a hash made with another key (another PC or planted file) is rejected");
        tp.Workflows[0].Steps[0] = "run: evil.exe|%path%";
        Check(WorkflowTrust.IsTampered(tp, tkey), "changed run step is detected");
        WorkflowTrust.Approve(tp, tkey); tp.ExternalEditorPath = @"C:\other.exe";
        Check(WorkflowTrust.IsTampered(tp, tkey), "changed editor path is detected");
        WorkflowTrust.Approve(tp, tkey); tp.ImageFormat = "jpg"; tp.IntervalSeconds = 9;
        Check(!WorkflowTrust.IsTampered(tp, tkey), "unrelated settings do not trip the check");
        var legacyTrust = new Preferences { ExternalEditorPath = @"C:\e.exe", WorkflowTrust = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 1 })) };
        Check(WorkflowTrust.IsTampered(legacyTrust, tkey), "an old unkeyed hash no longer validates (one re-approval after upgrade)");
        var noKeyThrown = false; try { WorkflowTrust.Compute(tp, Array.Empty<byte>()); } catch (ArgumentException) { noKeyThrown = true; }
        Check(noKeyThrown, "an empty key is rejected");
        Check(WorkflowTrust.Launches("run") && WorkflowTrust.Launches("editor") && !WorkflowTrust.Launches("copy"), "which steps launch programs");
        Check(!LogPolicy.ShouldRoll(LogPolicy.MaxFileBytes - 1) && LogPolicy.ShouldRoll(LogPolicy.MaxFileBytes), "log roll threshold");
        var tj = System.Text.Json.JsonSerializer.Serialize(new Preferences { WorkflowsLocked = true });
        Check(!tj.Contains("WorkflowsLocked") && tj.Contains("WorkflowTrust"), "lock flag is never written to settings");


        Console.WriteLine("v0.12: colour formats and image frames");
        Check(ColorFormat.Format("HEX", 255, 128, 0) == "FF8000" && ColorFormat.Format("RGB", 255, 128, 0) == "rgb(255, 128, 0)" && ColorFormat.Format("HTML", 255, 128, 0) == "#FF8000", "colour hex/rgb/html");
        Check(ColorFormat.Format("C++", 0x11, 0x22, 0x33) == "0x332211" && ColorFormat.Format("Delphi", 0x11, 0x22, 0x33) == "$00332211", "colour C++/Delphi BGR order");
        Check(ColorFormat.Next("HEX") == "RGB" && ColorFormat.Next("Delphi") == "HEX" && ColorFormat.Next("bogus") == "RGB" && ColorFormat.Sanitize("x") == "HEX", "format cycling and sanitising");
        Check(ColorFormat.TryParseHex("#fa0", out var hc2) && hc2 == ((byte)0xFF, (byte)0xAA, (byte)0x00) && !ColorFormat.TryParseHex("zz", out _), "hex parse (short form)");
        var gr2 = ColorFormat.Gradient((0, 0, 0), (255, 255, 255), 3);
        Check(gr2.Length == 3 && gr2[1] == ((byte)128, (byte)128, (byte)128) && ColorFormat.Gradient((0, 0, 0), (1, 1, 1), 1).Length == 2, "gradient");
        Check(new Preferences { ColorFormatName = "nope" }.Sanitize().ColorFormatName == "HEX", "prefs sanitise colour format");

        {
        byte[] Solid(int w, int h, byte b, byte g, byte r, byte a) { var p = new byte[w * h * 4]; for (int i = 0; i < p.Length; i += 4) { p[i] = b; p[i + 1] = g; p[i + 2] = r; p[i + 3] = a; } return p; }
        var src3 = Solid(4, 3, 10, 20, 30, 255);
        var (bp, bw, bh) = ImageFrames.AddBorder(src3, 4, 3, 2, 1, 2, 3);
        Check(bw == 8 && bh == 7 && bp.Length == 8 * 7 * 4, "border grows canvas by 2t");
        Check(bp[0] == 3 && bp[1] == 2 && bp[2] == 1 && bp[3] == 255, "border corner is the border colour (BGRA)");
        int ci = ((2) * bw + 2) * 4;
        Check(bp[ci] == 10 && bp[ci + 1] == 20 && bp[ci + 2] == 30 && bp[ci + 3] == 255, "border keeps the original pixel in place");
        Check(ImageFrames.AddBorder(src3, 4, 3, -5, 0, 0, 0).Width == 4 && ImageFrames.AddBorder(src3, 4, 3, 99999, 0, 0, 0).Width == 4 + 2 * ImageFrames.MaxThickness, "border thickness clamped");
        bool bad = false; try { ImageFrames.AddBorder(new byte[5], 4, 3, 1, 0, 0, 0); } catch (ArgumentException) { bad = true; }
        Check(bad, "border rejects a mismatched buffer");

        var sh = Solid(10, 10, 200, 100, 50, 255);
        var (sp, sw, shh) = ImageFrames.AddShadow(sh, 10, 10, 4, 5, 5, 0.5);
        int mg = ImageFrames.ShadowMargin(4, 5, 5);
        Check(sw == 10 + 2 * mg && shh == 10 + 2 * mg && mg == 13, "shadow margin = 2*blur + max offset");
        int oi = ((mg + 3) * sw + mg + 3) * 4;
        Check(sp[oi] == 200 && sp[oi + 1] == 100 && sp[oi + 2] == 50 && sp[oi + 3] == 255, "shadow leaves opaque original pixels untouched");
        Check(sp[3] == 0, "shadow far corner stays transparent");
        int si = ((mg + 10 + 2) * sw + mg + 6) * 4;   // just below the image, inside the offset shadow
        Check(sp[si + 3] > 0 && sp[si] == 0 && sp[si + 1] == 0 && sp[si + 2] == 0, "shadow below/right of the image is black with some alpha");
        int up = ((mg - 1) * sw + mg + 5) * 4;        // just above the image, away from the offset
        Check(sp[up + 3] < sp[si + 3], "shadow is weaker on the side opposite the offset");
        var (s0, _, _) = ImageFrames.AddShadow(sh, 10, 10, 4, 5, 5, 0);
        Check(s0.Where((v, i) => i % 4 == 3 && v != 0).Count() == 100, "opacity 0: only the 100 original pixels carry alpha");
        var (sa, saw, _) = ImageFrames.AddShadow(sh, 10, 10, 4, 5, 5, 0.5);
        Check(sa.SequenceEqual(sp) && saw == sw, "shadow is deterministic");
        var (nb, nbw, nbh) = ImageFrames.AddShadow(sh, 10, 10, 0, 0, 0, 1.0);
        Check(nbw == 10 && nbh == 10 && nb.SequenceEqual(sh), "no blur, no offset, opaque image: unchanged");
        var tr = Solid(6, 6, 255, 255, 255, 0);
        var (tpx, _, _) = ImageFrames.AddShadow(tr, 6, 6, 3, 2, 2, 1.0);
        Check(tpx.Where((v, i) => i % 4 == 3 && v != 0).Count() == 0, "a fully transparent image casts no shadow");
        }

    }
}
