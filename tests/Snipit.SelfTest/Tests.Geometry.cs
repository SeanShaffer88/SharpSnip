using Snipit.Core;

namespace Snipit.SelfTest;

// Geometry checks. Each file is one area; Program.cs runs them in order.
static partial class Tests
{
    internal static void Geometry()
    {
        Console.WriteLine("RectPx");
        Check(new RectPx(0, 0, 10, 10).Intersect(new RectPx(5, 5, 10, 10)) == new RectPx(5, 5, 5, 5), "intersect overlap");
        Check(new RectPx(0, 0, 10, 10).Intersect(new RectPx(10, 0, 5, 5)).IsEmpty, "touching edges do not intersect");
        Check(new RectPx(0, 0, 10, 10).Union(new RectPx(20, 20, 5, 5)) == new RectPx(0, 0, 25, 25), "union");
        Check(RectPx.Empty.Union(new RectPx(1, 2, 3, 4)) == new RectPx(1, 2, 3, 4), "union with empty");
        Check(RectPx.FromCorners(10, 10, 0, 0) == new RectPx(0, 0, 10, 10), "corners normalise");
        Check(new RectPx(0, 0, 10, 10).Contains(9, 9) && !new RectPx(0, 0, 10, 10).Contains(10, 9), "contains is half-open");

        Console.WriteLine("AppDataPath");
        {
            var fixture = Directory.CreateTempSubdirectory("snipit-data-test-");
            string portable = Path.Combine(fixture.FullName, "data");
            string legacy = Path.Combine(fixture.FullName, "SharpSnip");
            string current = Path.Combine(fixture.FullName, "Snipit");
            try
            {
                Check(AppDataPath.Resolve(fixture.FullName, fixture.FullName) == current &&
                      !Directory.EnumerateFileSystemEntries(fixture.FullName).Any(), "fresh installs select Snipit without creating a directory");
                Directory.CreateDirectory(current);
                Check(AppDataPath.Resolve(fixture.FullName, fixture.FullName) == current, "existing Snipit state is selected");
                Directory.CreateDirectory(legacy);
                Check(AppDataPath.Resolve(fixture.FullName, fixture.FullName) == legacy, "legacy state takes precedence when both directories exist");
                Directory.Delete(current);
                Check(AppDataPath.Resolve(fixture.FullName, fixture.FullName) == legacy, "legacy settings stay in place");
                Directory.CreateDirectory(portable);
                Check(AppDataPath.Resolve(fixture.FullName, fixture.FullName) == portable, "portable data takes precedence over legacy state");
            }
            finally
            {
                foreach (string path in new[] { portable, legacy, current })
                    if (Directory.Exists(path)) Directory.Delete(path);
                fixture.Delete();
            }
        }

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

    }
}
