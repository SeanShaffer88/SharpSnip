using System.Diagnostics;
using System.Text;
using Snipit.Core;

namespace Snipit.Native;

internal sealed record WindowEntry(IntPtr Hwnd, RectPx Frame, RectPx Client, string Title, int ProcessId);

/// <summary>Visible top-level windows in z-order (topmost first), excluding our own and cloaked ones.</summary>
internal static class WindowEnumerator
{
    public static List<WindowEntry> Snapshot()
    {
        var list = new List<WindowEntry>();
        uint self = (uint)Environment.ProcessId;
        NativeMethods.EnumWindows((h, _) =>
        {
            if (!NativeMethods.IsWindowVisible(h) || NativeMethods.IsIconic(h)) return true;
            if (NativeMethods.DwmGetWindowAttribute(h, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            NativeMethods.GetWindowThreadProcessId(h, out uint pid);
            if (pid == self) return true;
            long ex = NativeMethods.GetWindowLongPtr(h, NativeMethods.GWL_EXSTYLE).ToInt64();
            string title = TitleOf(h);
            if (title.Length == 0 && (ex & (NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE)) != 0) return true;
            var frame = FrameOf(h);
            if (frame.IsEmpty) return true;
            list.Add(new WindowEntry(h, frame, ClientOf(h), title, (int)pid));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static RectPx FrameOf(IntPtr h)
    {
        NativeMethods.RECT r;
        if (NativeMethods.DwmGetWindowAttribute(h, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out r, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) == 0)
            return RectPx.FromLtrb(r.Left, r.Top, r.Right, r.Bottom);
        return NativeMethods.GetWindowRect(h, out r) ? RectPx.FromLtrb(r.Left, r.Top, r.Right, r.Bottom) : RectPx.Empty;
    }

    public static RectPx ClientOf(IntPtr h)
    {
        if (!NativeMethods.GetClientRect(h, out var r)) return RectPx.Empty;
        var tl = new NativeMethods.POINT { X = 0, Y = 0 };
        if (!NativeMethods.ClientToScreen(h, ref tl)) return RectPx.Empty;
        return new RectPx(tl.X, tl.Y, r.Right - r.Left, r.Bottom - r.Top);
    }

    public static string TitleOf(IntPtr h)
    {
        var sb = new StringBuilder(256);
        int n = NativeMethods.GetWindowText(h, sb, sb.Capacity);
        return n > 0 ? sb.ToString() : "";
    }

    public static WindowEntry? At(IReadOnlyList<WindowEntry> list, int x, int y) =>
        list.FirstOrDefault(w => w.Frame.Contains(x, y));

    public static string? ProcessName(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.ProcessName; }
        catch (Exception) { return null; }
    }
}
