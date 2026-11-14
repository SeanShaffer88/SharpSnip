using System.Windows.Media.Imaging;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>
/// Freehand picker: draw an outline with the mouse; on release the outline's
/// bounding box is captured and everything outside the outline is made
/// transparent (Core PolygonMask). Too-small scribbles are discarded and the
/// picker stays up. A plain click instead starts a polygon: click each corner,
/// then click the first point, double-click or press Enter to finish (Core PolygonBuilder).
/// </summary>
internal sealed class FreehandPicker : PickerBase
{
    private readonly List<(int X, int Y)> _points = new();
    private readonly PolygonBuilder _poly = new();
    private bool _drawing;
    private int _x, _y;
    private BitmapSource? _frozen;
    private RectPx _frozenRect;

    public FreehandPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Freehand;

    protected override void Enter()
    {
        (_x, _y) = MonitorEnumerator.CursorPos();
        if (Ctx.Overlays.AllExcludedFromCapture)
        {
            try
            {
                var union = VirtualDesktop.Union(Ctx.Monitors);
                var cap = CaptureEngine.Capture(union, Ctx.Monitors, 0xFF000000u);
                _frozen = cap.Image; _frozenRect = cap.Rect;
            }
            catch (Exception ex) { Log.Warn("Loupe snapshot failed: " + ex.Message); }
        }
        Ctx.Overlays.Dim(_ => true, null);
        Ctx.Overlays.Highlight(null);
        Ctx.Overlays.Loupe(_frozen, _frozenRect, _x, _y);
        RefreshHint();
    }

    protected override void Leave()
    {
        Ctx.Overlays.Trace(null);
        Ctx.Overlays.Loupe(null, RectPx.Empty, 0, 0);
    }

    protected override void RefreshHint() =>
        Ctx.Overlays.HintAt(_poly.Count == 0
            ? $"Freehand · drag an outline, or click points for a polygon · [Space] delay {DelayText} · [Esc] back"
            : "Polygon · click points · click the first point, double-click the last point or [Enter] to finish · [Backspace] undo · [Esc] clear", _x, _y);

    protected override void OnMove(int x, int y)
    {
        _x = x; _y = y;
        Ctx.Overlays.Loupe(_frozen, _frozenRect, x, y);
        if (_poly.Count > 0) { TracePolygon(); return; }
        if (!_drawing) return;
        var last = _points[^1];
        if (Math.Abs(x - last.X) + Math.Abs(y - last.Y) < 3) return; // keep the outline light
        _points.Add((x, y));
        Ctx.Overlays.Trace(_points);
    }

    private void TracePolygon()
    {
        var pts = new List<(int X, int Y)>(_poly.Points) { (_x, _y) };
        Ctx.Overlays.Trace(pts);
    }

    private void FinishPolygon()
    {
        var polygon = _poly.Points.ToArray();
        Finish(PickOutcome.Picked(new PickResult(PolygonMask.Bounds(polygon), new CaptureMeta("Polygon"), Polygon: polygon)));
    }

    protected override bool HandleKey(int vk)
    {
        if (_poly.Count > 0)
        {
            switch (vk)
            {
                case VkEsc: _poly.Clear(); Ctx.Overlays.Trace(null); RefreshHint(); return true;
                case 0x08: _poly.Undo(); if (_poly.Count == 0) Ctx.Overlays.Trace(null); else TracePolygon(); RefreshHint(); return true;
                case 0x0D: if (_poly.CanClose) FinishPolygon(); return true;
            }
        }
        return base.HandleKey(vk);
    }

    protected override bool OnLeftDown(int x, int y)
    {
        if (_poly.Count > 0) return true;
        _drawing = true;
        _points.Clear();
        _points.Add((x, y));
        return true;
    }

    protected override bool OnLeftUp(int x, int y)
    {
        _x = x; _y = y;
        if (_poly.Count > 0)
        {
            if (_poly.Click(x, y, Environment.TickCount64, NativeMethods.GetDoubleClickTime()) == PolygonBuilder.Result.Closed)
                FinishPolygon();
            else
                TracePolygon();
            return true;
        }
        if (!_drawing) return true;
        _drawing = false;
        _points.Add((x, y));
        var bounds = PolygonMask.Bounds(_points);
        if (bounds.Width <= 4 && bounds.Height <= 4)
        {
            // a click, not a drag: this is the first vertex of a click-by-click polygon
            _points.Clear();
            _poly.Click(x, y, Environment.TickCount64, NativeMethods.GetDoubleClickTime());
            TracePolygon(); RefreshHint();
            return true;
        }
        if (_points.Count < 8 || bounds.Width < 8 || bounds.Height < 8)
        {
            _points.Clear();
            Ctx.Overlays.Trace(null);
            return true; // a stray scribble, not a capture — stay in the picker
        }
        var polygon = _points.ToArray();
        Finish(PickOutcome.Picked(new PickResult(bounds, new CaptureMeta("Freehand"), Polygon: polygon)));
        return true;
    }
}
