using SharpSnip.Core;

namespace SharpSnip;

/// <summary>Full-screen picker (§3D.B): choose monitor / all monitors and a delay, then capture.</summary>
internal sealed class FullScreenPicker : PickerBase
{
    private FullScreenTarget _target;
    private int _x, _y;

    public FullScreenPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.FullScreen;

    protected override void Enter()
    {
        (_x, _y) = MonitorEnumerator.CursorPos();
        _target = Ctx.FullTarget;
        // "Current monitor" is fixed at activation so moving the mouse never changes the target.
        if (_target.Kind == FullScreenKind.CurrentMonitor && VirtualDesktop.MonitorAt(Ctx.Monitors, _x, _y) is { } m)
            _target = FullScreenTarget.Monitor(m.Index);
        Ctx.Overlays.Adorn(null, false, null);
        Ctx.Overlays.Hud(null, 0, 0);
        Render();
    }

    private int CursorMonitorIndex => VirtualDesktop.MonitorAt(Ctx.Monitors, _x, _y)?.Index ?? 1;

    private void Render()
    {
        var monitors = Ctx.Monitors;
        var t = _target;
        bool all = t.Kind == FullScreenKind.AllMonitors;
        bool IsTarget(MonitorInfo m) => all || (t.Kind == FullScreenKind.MonitorIndex && m.Index == t.MonitorIndex);

        Ctx.Overlays.Highlight(null);
        Ctx.Overlays.Dim(m => !IsTarget(m), null);   // target stays clear, the rest is dimmed
        Ctx.Overlays.Outline(IsTarget);
        Ctx.Overlays.Chips(m => m.Label);
        RefreshHint();
    }

    protected override void RefreshHint()
    {
        var monitors = Ctx.Monitors;
        string what = _target.Kind == FullScreenKind.AllMonitors
            ? $"All monitors ({monitors.Count})"
            : $"Monitor {_target.MonitorIndex} of {monitors.Count}";
        string text = $"Full screen · {what} · [Tab] next · [A] all · [Space] delay: {DelayText} · [Enter] capture · [Esc] cancel";
        var host = VirtualDesktop.ByIndex(monitors, _target.Kind == FullScreenKind.MonitorIndex ? _target.MonitorIndex : CursorMonitorIndex)
                   ?? monitors.FirstOrDefault();
        if (host is not null) Ctx.Overlays.HintOn(text, host);
    }

    protected override void OnMove(int x, int y) { _x = x; _y = y; }

    protected override bool HandleKey(int vk)
    {
        int count = Ctx.Monitors.Count;
        switch (vk)
        {
            case VkTab: _target = _target.Cycle(count, CursorMonitorIndex, ShiftDown() ? -1 : 1); Render(); return true;
            case 0x41: _target = FullScreenTarget.All; Render(); return true;
            case VkEnter: Confirm(); return true;
        }
        int n = vk is >= 0x31 and <= 0x39 ? vk - 0x30 : vk is >= 0x61 and <= 0x69 ? vk - 0x60 : 0;
        if (n > 0)
        {
            if (VirtualDesktop.ByIndex(Ctx.Monitors, n) is not null) { _target = FullScreenTarget.Monitor(n); Render(); }
            return true;
        }
        return base.HandleKey(vk);
    }

    protected override bool OnLeftUp(int x, int y) { Confirm(); return true; }

    private void Confirm()
    {
        var target = _target;
        var monitors = Ctx.Monitors;
        RectPx? rect = VirtualDesktop.ResolveTarget(target, monitors, _x, _y);
        if (rect is null) return;
        var meta = new CaptureMeta(target.Kind == FullScreenKind.AllMonitors ? "Full screen (all monitors)" : $"Full screen (monitor {target.MonitorIndex})");
        Finish(PickOutcome.Picked(new PickResult(rect.Value, meta, ms => VirtualDesktop.ResolveTarget(target, ms, 0, 0))));
    }
}
