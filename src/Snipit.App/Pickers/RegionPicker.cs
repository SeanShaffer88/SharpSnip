using System.Windows.Media.Imaging;
using Snipit.Core;

namespace Snipit;

/// <summary>
/// Region picker (§3B): rubber-band with 8 handles, size read-out, magnifier loupe, and edge snapping to
/// visible windows. A normal drag captures on release. Shift stages the rectangle for editing.
/// </summary>
internal sealed class RegionPicker : PickerBase
{
    private enum Drag { None, New, Move, Resize }

    private const int HandleTol = 8, SnapTol = 8;
    private const int MinSelection = 4, DragThreshold = 4;
    private readonly bool _captureOnRelease;

    private Drag _drag;
    private RegionHandle _handle;
    private RectPx _rect = RectPx.Empty, _startRect;
    private int _anchorX, _anchorY, _downX, _downY, _x, _y;
    private bool _stageNewRegion, _dragMoved;
    private List<int> _snapX = new(), _snapY = new();
    private BitmapSource? _frozen;
    private RectPx _frozenRect;
    private RectPx _union;
    private readonly List<RectPx> _areas = new();

    public RegionPicker(PickerContext ctx, bool captureOnRelease) : base(ctx) => _captureOnRelease = captureOnRelease;
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

    protected override void Leave() { Ctx.Overlays.Areas(Array.Empty<RectPx>()); Ctx.Overlays.Loupe(null, RectPx.Empty, 0, 0); }

    protected override void RefreshHint() =>
        Ctx.Overlays.HintAt(_areas.Count > 0
            ? $"Region · {_areas.Count} saved · drag another or click selection to capture · [A] keep · [Backspace] remove · [Enter] capture"
            : !_rect.IsEmpty && _drag == Drag.None
                ? "Region · drag to adjust · click selection or [Enter] to finish · [A] keep · [Esc] back"
                : _captureOnRelease
                    ? $"Region · drag and release to capture · [Shift]+drag to adjust or add areas · [Space] delay {DelayText} · [Esc] back"
                    : "Region · drag to select · click selection or [Enter] to finish · [Esc] back", _x, _y);

    private void Visuals()
    {
        Ctx.Overlays.Areas(_areas);
        if (_rect.IsEmpty && _areas.Count > 0)
        {
            Ctx.Overlays.Dim(_ => true, null);
            Ctx.Overlays.Highlight(null);
            Ctx.Overlays.Adorn(null, false, null);
        }
        else if (_rect.IsEmpty)
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
        if (_drag == Drag.None) return;
        UpdateDrag(x, y);
        Visuals();
    }

    private void UpdateDrag(int x, int y)
    {
        _dragMoved |= Math.Abs((long)x - _downX) >= DragThreshold || Math.Abs((long)y - _downY) >= DragThreshold;
        if (!_dragMoved && _drag != Drag.New) return;
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
        }
    }

    private int SnapX(int v) => RegionGeometry.Snap(v, _snapX, SnapTol);
    private int SnapY(int v) => RegionGeometry.Snap(v, _snapY, SnapTol);

    protected override bool OnLeftDown(int x, int y)
    {
        _dragMoved = false;
        _downX = x; _downY = y; _startRect = _rect;
        var h = RegionGeometry.HitTest(_rect, x, y, HandleTol);
        if (h == RegionHandle.None)
        {
            _drag = Drag.New;
            _stageNewRegion = !_captureOnRelease || _areas.Count > 0 || ShiftDown();
            _anchorX = SnapX(x); _anchorY = SnapY(y);
            _rect = RectPx.Empty;
        }
        else if (h == RegionHandle.Body) _drag = Drag.Move;
        else { _drag = Drag.Resize; _handle = h; }
        Visuals();
        return true;
    }

    protected override bool OnLeftUp(int x, int y)
    {
        if (_drag == Drag.None) return true;
        _x = x; _y = y;
        UpdateDrag(x, y);
        bool confirm;
        if (_drag == Drag.New)
        {
            if (!_dragMoved || _rect.Width < MinSelection || _rect.Height < MinSelection) _rect = RectPx.Empty;
            confirm = !_stageNewRegion && !_rect.IsEmpty;
        }
        else
        {
            if (!_dragMoved) _rect = _startRect;
            confirm = !_dragMoved && _startRect.Contains(_downX, _downY) && _startRect.Contains(x, y);
        }
        _drag = Drag.None;
        if (confirm) { Confirm(); return true; }
        Visuals();
        return true;
    }

    protected override bool HandleKey(int vk)
    {
        if (vk == VkEnter) { if (_drag == Drag.None) Confirm(); return true; }
        if (vk == 0x41) // [A] keep this area and start another
        {
            if (_drag == Drag.None && !_rect.IsEmpty && _areas.Count < AreaMask.MaxAreas) { _areas.Add(_rect); _rect = RectPx.Empty; Visuals(); }
            return true;
        }
        if (vk == 0x08) // [Backspace] drop the last added area
        {
            if (_drag != Drag.None) return true;
            if (_rect.IsEmpty && _areas.Count > 0) _areas.RemoveAt(_areas.Count - 1);
            else _rect = RectPx.Empty;
            Visuals();
            return true;
        }
        return base.HandleKey(vk);
    }

    private void Confirm()
    {
        var areas = new List<RectPx>(_areas);
        if (!_rect.IsEmpty) areas.Add(_rect);
        if (areas.Count == 0) return;
        if (areas.Count == 1) { Finish(PickOutcome.Picked(new PickResult(areas[0], new CaptureMeta("Region")))); return; }
        Finish(PickOutcome.Picked(new PickResult(AreaMask.Bounds(areas), new CaptureMeta("Multi-area"), Areas: areas)));
    }
}
