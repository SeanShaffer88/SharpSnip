using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// MODE_MENU (§2.3). Shows the mode bar — a tab hanging from the top-centre of
/// the monitor under the cursor, icon buttons with labels underneath — and
/// dispatches clicks and keys to the pickers. Never captures by itself.
/// Every action also stays on its key: E/R/W/S/F/D/C, 1..9, Space, Esc.
/// </summary>
internal sealed class MenuPicker : PickerBase
{
    private int _x, _y;
    private int _hover = -2; // -2 = nothing, -1 = close, >= 0 = button index
    private MonitorInfo? _barMonitor;

    public MenuPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Menu;

    private IReadOnlyList<ModeBarButton> Buttons => new[]
    {
        new ModeBarButton(ModeBarIcon.Element, "Element", "Element (E)"),
        new ModeBarButton(ModeBarIcon.Region, "Region", "Region (R)"),
        new ModeBarButton(ModeBarIcon.Window, "Window", "Window (W)"),
        new ModeBarButton(ModeBarIcon.FullScreen, "Full screen", "Full screen, this monitor (D); F = all monitors"),
        new ModeBarButton(ModeBarIcon.Freehand, "Freehand", "Freehand outline capture (H)"),
        new ModeBarButton(ModeBarIcon.Scrolling, "Scrolling", "Scrolling capture (C)"),
        new ModeBarButton(ModeBarIcon.Delay, Ctx.DelaySeconds > 0 ? $"{Ctx.DelaySeconds}s delay" : "Delay", "Delay (Space cycles)"),
    };

    protected override void Enter()
    {
        (_x, _y) = MonitorEnumerator.CursorPos();
        _barMonitor = VirtualDesktop.MonitorAt(Ctx.Monitors, _x, _y);
        Ctx.Overlays.Dim(_ => true, null);
        Ctx.Overlays.Highlight(null);
        Ctx.Overlays.Adorn(null, false, null);
        ShowBar();
    }

    protected override void Leave() => Ctx.Overlays.ModeBar(null, -2, _x, _y);

    protected override void RefreshHint() => ShowBar();

    private void ShowBar() => Ctx.Overlays.ModeBar(Buttons, _hover, _x, _y);

    /// <summary>Bar hit-test: physical px → bar-local units on the bar's monitor.</summary>
    private ModeBarLayout.Hit? HitAt(int x, int y)
    {
        if (_barMonitor is not { } m) return null;
        double s = m.Scale;
        double barLeft = (m.Bounds.Width / s - ModeBarLayout.BarWidth(Buttons.Count)) / 2;
        return ModeBarLayout.HitTest((x - m.Bounds.X) / s - barLeft, (y - m.Bounds.Y) / s, Buttons.Count);
    }

    protected override void OnMove(int x, int y)
    {
        var hit = HitAt(x, y);
        int hover = hit is null ? -2 : hit.Value.IsClose ? -1 : hit.Value.Index;
        if (hover == _hover) return;
        _hover = hover;
        ShowBar();
    }

    protected override bool OnLeftDown(int x, int y)
    {
        if (HitAt(x, y) is not { } hit) return true;
        if (hit.IsClose) { Finish(PickOutcome.Cancel()); return true; }
        switch (hit.Index)
        {
            case 0: Finish(PickOutcome.SwitchTo(PickMode.Element)); break;
            case 1: Finish(PickOutcome.SwitchTo(PickMode.Region)); break;
            case 2: Finish(PickOutcome.SwitchTo(PickMode.Window)); break;
            case 3: Finish(PickOutcome.SwitchTo(PickMode.FullScreen, FullScreenTarget.Current)); break;
            case 4: Finish(PickOutcome.SwitchTo(PickMode.Freehand)); break;
            case 5: Ctx.ScrollCapture = true; Finish(PickOutcome.SwitchTo(PickMode.Element)); break;
            case 6: Ctx.DelaySeconds = DelayCycle.Next(Ctx.DelaySeconds); RefreshHint(); break;
        }
        return true;
    }

    protected override bool HandleKey(int vk)
    {
        switch (vk)
        {
            case 0x46: Finish(PickOutcome.SwitchTo(PickMode.FullScreen, FullScreenTarget.All)); return true;
            case 0x44: Finish(PickOutcome.SwitchTo(PickMode.FullScreen, FullScreenTarget.Current)); return true;
            case 0x43: Ctx.ScrollCapture = true; Finish(PickOutcome.SwitchTo(PickMode.Element)); return true; // [C] scrolling capture
        }
        int n = vk is >= 0x31 and <= 0x39 ? vk - 0x30 : vk is >= 0x61 and <= 0x69 ? vk - 0x60 : 0;
        if (n > 0)
        {
            if (VirtualDesktop.ByIndex(Ctx.Monitors, n) is not null)
                Finish(PickOutcome.SwitchTo(PickMode.FullScreen, FullScreenTarget.Monitor(n)));
            return true;
        }
        return base.HandleKey(vk);
    }
}
