using Snipit.Core;

namespace Snipit.SelfTest;

static partial class Tests
{
    internal static int Passed, Failed;

    static void Check(bool ok, string name)
    {
        if (ok) { Passed++; Console.WriteLine("  ok   " + name); }
        else { Failed++; Console.WriteLine("  FAIL " + name); }
    }

    // Mixed-DPI, L-shaped layout: 1920x1080@100% at origin, 2560x1440@150% to the right, raised by 300px.
    static readonly MonitorInfo m1 = new(1, new RectPx(0, 0, 1920, 1080), 96, true, "A");
    static readonly MonitorInfo m2 = new(2, new RectPx(1920, -300, 2560, 1440), 144, false, "B");
    static readonly MonitorInfo[] mons = { m1, m2 };
}
