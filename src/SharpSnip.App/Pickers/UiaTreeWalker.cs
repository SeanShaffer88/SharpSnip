using System.Windows.Automation;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

internal sealed record UiaSnapshot(RectPx Rect, string ControlType, string Name, string AutomationId, string ClassName, int ProcessId, bool Fallback);

/// <summary>
/// Element selection state (§3A). Every member must run on the <see cref="UiaWorker"/> thread.
/// Hover → element under cursor; Tab → sibling; PgUp/PgDn and Ctrl+Wheel → ancestors/children.
/// </summary>
internal sealed class UiaTreeWalker
{
    private static readonly HashSet<string> Containers = new() { "ToolBar", "Pane", "Group", "List", "Tree", "Tab", "MenuBar" };
    private readonly TreeWalker _walker = TreeWalker.ControlViewWalker;
    private readonly Stack<AutomationElement> _descent = new();
    private AutomationElement? _cur;

    /// <summary>The element the selection state currently points at (scrolling capture needs the live element, not just its rectangle).</summary>
    public AutomationElement? CurrentElement() => _cur;

    public UiaSnapshot? Hover(int x, int y)
    {
        _descent.Clear();
        AutomationElement? el = null;
        try { el = AutomationElement.FromPoint(new System.Windows.Point(x, y)); }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or System.Runtime.InteropServices.COMException) { }

        // Our own click-through overlay can win the hit test; find the real window underneath instead.
        if (el is not null && SafePid(el) == Environment.ProcessId) el = HitByWindowList(x, y);

        if (el is null) { _cur = null; return HwndFallback(x, y); }

        for (int i = 0; i < 6 && RectOf(el).IsEmpty; i++)
        {
            var p = Parent(el);
            if (p is null) break;
            el = p;
        }
        _cur = el;
        return Snap(el, false);
    }

    public UiaSnapshot? Next(bool reverse)
    {
        if (_cur is null) return null;
        try
        {
            var sib = reverse ? _walker.GetPreviousSibling(_cur) : _walker.GetNextSibling(_cur);
            if (sib is null)
            {
                var parent = Parent(_cur);
                if (parent is not null) sib = reverse ? _walker.GetLastChild(parent) : _walker.GetFirstChild(parent);
            }
            if (sib is not null && !RectOf(sib).IsEmpty) { _cur = sib; _descent.Clear(); }
        }
        catch (ElementNotAvailableException) { }
        return Snap(_cur, false);
    }

    public UiaSnapshot? ParentStep()
    {
        if (_cur is null) return null;
        var p = Parent(_cur);
        if (p is not null && !RectOf(p).IsEmpty) { _descent.Push(_cur); _cur = p; }
        return Snap(_cur, false);
    }

    public UiaSnapshot? ChildStep(int x, int y)
    {
        if (_cur is null) return null;
        if (_descent.Count > 0) { _cur = _descent.Pop(); return Snap(_cur, false); }
        try
        {
            AutomationElement? first = null;
            for (var c = _walker.GetFirstChild(_cur); c is not null; c = _walker.GetNextSibling(c))
            {
                var r = RectOf(c);
                if (r.IsEmpty) continue;
                first ??= c;
                if (r.Contains(x, y)) { first = c; break; }
            }
            if (first is not null) _cur = first;
        }
        catch (ElementNotAvailableException) { }
        return Snap(_cur, false);
    }

    /// <summary>Ctrl+Wheel up: climb to the next container-type ancestor with a larger rectangle.</summary>
    public UiaSnapshot? Expand()
    {
        if (_cur is null) return null;
        var curRect = RectOf(_cur);
        for (var p = Parent(_cur); p is not null; p = Parent(p))
        {
            if (Containers.Contains(TypeName(p)) && RectOf(p) != curRect && !RectOf(p).IsEmpty)
            {
                _descent.Push(_cur); _cur = p;
                break;
            }
        }
        return Snap(_cur, false);
    }

    /// <summary>Ctrl+Wheel down: undo one expansion.</summary>
    public UiaSnapshot? Contract()
    {
        if (_cur is null) return null;
        if (_descent.Count > 0) _cur = _descent.Pop();
        return Snap(_cur, false);
    }

    public string? CurrentPath()
    {
        if (_cur is null) return null;
        var parts = new List<string>();
        int depth = 0;
        for (AutomationElement? e = _cur; e is not null && depth < 12; e = Parent(e), depth++)
        {
            string name = SafeName(e);
            parts.Add(name.Length > 0 ? $"{TypeName(e)}[{name}]" : TypeName(e));
        }
        parts.Reverse();
        return string.Join(" / ", parts);
    }

    // ---- helpers -------------------------------------------------------------------------------

    private AutomationElement? Parent(AutomationElement el)
    {
        try
        {
            var p = _walker.GetParent(el);
            if (p is null || Automation.Compare(p, AutomationElement.RootElement)) return null;
            return p;
        }
        catch (ElementNotAvailableException) { return null; }
    }

    private UiaSnapshot? HwndFallback(int x, int y)
    {
        var w = WindowEnumerator.At(WindowEnumerator.Snapshot(), x, y);
        if (w is null) return null;
        return new UiaSnapshot(w.Frame, "Window", w.Title, "", "", w.ProcessId, true);
    }

    private AutomationElement? HitByWindowList(int x, int y)
    {
        var w = WindowEnumerator.At(WindowEnumerator.Snapshot(), x, y);
        if (w is null) return null;
        try
        {
            var el = AutomationElement.FromHandle(w.Hwnd);
            for (int guard = 0; guard < 32; guard++)
            {
                AutomationElement? hit = null;
                for (var c = _walker.GetFirstChild(el); c is not null; c = _walker.GetNextSibling(c))
                    if (RectOf(c).Contains(x, y)) { hit = c; break; }
                if (hit is null) break;
                el = hit;
            }
            return el;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException) { return null; }
    }

    private static RectPx RectOf(AutomationElement el)
    {
        try
        {
            var r = el.Current.BoundingRectangle;
            if (r.IsEmpty || double.IsInfinity(r.X) || double.IsInfinity(r.Y) || double.IsInfinity(r.Width) || double.IsInfinity(r.Height))
                return RectPx.Empty;
            return RectPx.FromLtrb((int)Math.Floor(r.Left), (int)Math.Floor(r.Top), (int)Math.Ceiling(r.Right), (int)Math.Ceiling(r.Bottom));
        }
        catch (ElementNotAvailableException) { return RectPx.Empty; }
    }

    private static string TypeName(AutomationElement el)
    {
        try
        {
            string n = el.Current.ControlType.ProgrammaticName;
            return n.StartsWith("ControlType.", StringComparison.Ordinal) ? n[12..] : n;
        }
        catch (ElementNotAvailableException) { return "Unknown"; }
    }

    private static string SafeName(AutomationElement el)
    {
        try { return el.Current.Name ?? ""; } catch (ElementNotAvailableException) { return ""; }
    }

    private static int SafePid(AutomationElement el)
    {
        try { return el.Current.ProcessId; } catch (ElementNotAvailableException) { return -1; }
    }

    private static UiaSnapshot? Snap(AutomationElement? el, bool fallback)
    {
        if (el is null) return null;
        try
        {
            var c = el.Current;
            return new UiaSnapshot(RectOf(el), TypeName(el), c.Name ?? "", c.AutomationId ?? "", c.ClassName ?? "", c.ProcessId, fallback);
        }
        catch (ElementNotAvailableException) { return null; }
    }
}
