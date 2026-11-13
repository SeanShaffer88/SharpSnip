using Snipit.Core;

namespace Snipit;

/// <summary>Fixed-size region: a W×H frame follows the cursor (arrows nudge); click or Enter captures it.</summary>
internal sealed class FixedRegionPicker : PickerBase
{
    private int _x, _y, _offX, _offY;
    private RectPx _union, _rect;

    public FixedRegionPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.FixedRegion;

    protected override void Enter()
    {
        _union = VirtualDesktop.Union(Ctx.Monitors);
        (_x, _y) = MonitorEnumerator.CursorPos();
        Ctx.Overlays.Hud(null, 0, 0);
        Update();
    }

    protected override void OnMove(int x, int y) { _x = x; _y = y; Update(); }

    private void Update()
    {
        _rect = FixedRegion.Place(Ctx.Prefs.FixedWidth, Ctx.Prefs.FixedHeight, _x + _offX, _y + _offY, _union);
        Ctx.Overlays.Dim(_ => true, _rect);
        Ctx.Overlays.Highlight(_rect);
        Ctx.Overlays.Adorn(_rect, false, $"{_rect.Width} × {_rect.Height}");
        RefreshHint();
    }

    protected override void RefreshHint() =>
        Ctx.Overlays.HintAt($"Fixed {Ctx.Prefs.FixedWidth}×{Ctx.Prefs.FixedHeight} · move mouse · [Arrows] nudge (Shift = 10 px) · [Enter]/click capture · [Space] delay {DelayText} · [Esc] back", _x, _y);

    protected override bool HandleKey(int vk)
    {
        int step = ShiftDown() ? 10 : 1;
        switch (vk)
        {
            case 0x25: _offX -= step; Update(); return true;   // left
            case 0x26: _offY -= step; Update(); return true;   // up
            case 0x27: _offX += step; Update(); return true;   // right
            case 0x28: _offY += step; Update(); return true;   // down
            case VkEnter: Confirm(); return true;
        }
        return base.HandleKey(vk);
    }

    protected override bool OnLeftUp(int x, int y) { Confirm(); return true; }

    private void Confirm() =>
        Finish(PickOutcome.Picked(new PickResult(_rect, new CaptureMeta($"Fixed region {_rect.Width}×{_rect.Height}"))));
}
