using System.Windows.Media.Imaging;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>
/// Region picker (§3B): rubber-band with 8 handles, size read-out, magnifier loupe, and edge snapping to
/// visible windows. Drag outside to start over, inside to move, handles to resize. Enter / double-click captures.
/// </summary>
internal sealed class RegionPicker : PickerBase
{
    private enum Drag { None, New, Move, Resize }

    private const int HandleTol = 8, SnapTol = 8;

    private Drag _drag;
    private RegionHandle _handle;
    private RectPx _rect = RectPx.Empty, _startRect;
    private int _anchorX, _anchorY, _downX, _downY, _x, _y;
    private long _lastDownTick;
    private int _lastDownX, _lastDownY;
    private List<int> _snapX = new(), _snapY = new();
    private BitmapSource? _frozen;
    private RectPx _frozenRect;
    private RectPx _union;

    public RegionPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Region;

    protected override void Enter()
    {
        _union = VirtualDesktop.Union(Ctx.Monitors);
        (_x, _y) = MonitorEnumerator.CursorPos();

        foreach (var w in WindowEnumerator.Snapshot())
        {
            _snapX.Add(w.Frame.X); _snapX.Add(w.Frame.Right);
            _snapY.Add(w.Frame.Y); _snapY.Add(w.Frame.Bottom);
        }

        // Frozen frame for the loupe. Only safe when overlays are excluded from capture; otherwise the loupe is skipped.
        if (Ctx.Overlays.AllExcludedFromCapture)
        {
            try { var cap = CaptureEngine.Capture(_union, Ctx.Monitors, 0xFF000000u); _frozen = cap.Image; _frozenRect = cap.Rect; }
            catch (Exception ex) { Log.Warn("Loupe snapshot failed: " + ex.Message); }
        }

        Ctx.Overlays.Hud(null, 0, 0);
        Ctx.Overlays.Highlight(null);
        Ctx.Overlays.Dim(_ => false, null); // live preview, no dimmer until a region exists (§3B.1)
        Ctx.Overlays.Loupe(_frozen, _frozenRect, _x, _y);
        Visuals();
    }

    protected override void Leave() => Ctx.Overlays.Loupe(null, RectPx.Empty, 0, 0);

    protected override void RefreshHint() =>
        Ctx.Overlays.HintAt($"Region · drag to select · [Enter] capture · [Space] delay {DelayText} · [Esc] back", _x, _y);

    private void Visuals()
    {
        if (_rect.IsEmpty)
        {
            Ctx.Overlays.Dim(_ => false, null);
            Ctx.Overlays.Highlight(null);
            Ctx.Overlays.Adorn(null, false, null);
        }
        else
        {
            Ctx.Overlays.Dim(_ => true, _rect);
            Ctx.Overlays.Highlight(_rect);
            Ctx.Overlays.Adorn(_rect, _drag != Drag.New, $"{_rect.Width} × {_rect.Height}");
        }
        RefreshHint();
    }

    protected override void OnMove(int x, int y)
    {
        _x = x; _y = y;
        Ctx.Overlays.Loupe(_frozen, _frozenRect, x, y);
        switch (_drag)
        {
            case Drag.New:
                _rect = RectPx.FromCorners(_anchorX, _anchorY, SnapX(x), SnapY(y)).Intersect(_union);
                break;
            case Drag.Move:
                _rect = RegionGeometry.Move(_startRect, x - _downX, y - _downY, _union);
                break;
            case Drag.Resize:
                _rect = RegionGeometry.Resize(_startRect, _handle, SnapX(x), SnapY(y)).Intersect(_union);
                break;
            default: return;
        }
        Visuals();
    }

    private int SnapX(int v) => RegionGeometry.Snap(v, _snapX, SnapTol);
    private int SnapY(int v) => RegionGeometry.Snap(v, _snapY, SnapTol);

    protected override bool OnLeftDown(int x, int y)
    {
        long now = Environment.TickCount64;
        bool dbl = now - _lastDownTick <= NativeMethods.GetDoubleClickTime()
                   && Math.Abs(x - _lastDownX) <= NativeMethods.GetSystemMetrics(NativeMethods.SM_CXDOUBLECLK)
                   && Math.Abs(y - _lastDownY) <= NativeMethods.GetSystemMetrics(NativeMethods.SM_CYDOUBLECLK);
        _lastDownTick = now; _lastDownX = x; _lastDownY = y;
        if (dbl && !_rect.IsEmpty && _rect.Contains(x, y)) { Confirm(); return true; }

        _downX = x; _downY = y; _startRect = _rect;
        var h = RegionGeometry.HitTest(_rect, x, y, HandleTol);
        if (h == RegionHandle.None) { _drag = Drag.New; _anchorX = SnapX(x); _anchorY = SnapY(y); _rect = RectPx.Empty; }
        else if (h == RegionHandle.Body) _drag = Drag.Move;
        else { _drag = Drag.Resize; _handle = h; }
        Visuals();
        return true;
    }

    protected override bool OnLeftUp(int x, int y)
    {
        if (_drag == Drag.New && (_rect.Width < 4 || _rect.Height < 4)) _rect = RectPx.Empty; // stray click
        _drag = Drag.None;
        Visuals();
        return true;
    }

    protected override bool HandleKey(int vk)
    {
        if (vk == VkEnter) { Confirm(); return true; }
        return base.HandleKey(vk);
    }

    private void Confirm()
    {
        if (_rect.IsEmpty) return;
        Finish(PickOutcome.Picked(new PickResult(_rect, new CaptureMeta("Region"))));
    }
}
