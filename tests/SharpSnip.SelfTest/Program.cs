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
var p = new Preferences { ImageFormat = "gif", HotkeyMenu = "nonsense", DefaultDelaySeconds = 4, DimmerOpacity = 5, MatteColor = "Pink", FullScreenTarget = "Monitor:3", EditorBounds = new[] { 1, 2 } }.Sanitize();
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

Console.WriteLine("OutputNaming");
Check(OutputNaming.FileName(new DateTime(2026, 11, 12, 9, 8, 7, 6), "png") == "20261112-090807-006.png", "timestamp name");
Check(OutputNaming.ResolveFolder("", @"C:\Users\x").EndsWith("SharpSnip") && OutputNaming.ResolveFolder("D:\\shots", "x") == "D:\\shots", "output folder resolution");

Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
