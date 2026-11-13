using System.Windows;
using System.Windows.Threading;
using Snipit.Core;

namespace Snipit;

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
    private bool _dragging, _saveQueued;
    private double _grab; // cursor offset from the bar's left edge when the drag began, in bar units
    private double _savedCenter;

    public MenuPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Menu;

    private IReadOnlyList<ModeBarButton> Buttons => new[]
    {
        new ModeBarButton(ModeBarIcon.Element, "Element", "Element (E)", "E"),
        new ModeBarButton(ModeBarIcon.Region, "Region", "Region (R)", "R"),
        new ModeBarButton(ModeBarIcon.Window, "Window", "Window (W)", "W"),
        new ModeBarButton(ModeBarIcon.FullScreen, "Full screen", "Full screen, this monitor (D); F = all monitors", "D"),
        new ModeBarButton(ModeBarIcon.Freehand, "Freehand", "Freehand outline capture (H)", "H"),
        new ModeBarButton(ModeBarIcon.Scrolling, "Scrolling", "Scrolling capture (C)", "C"),
        new ModeBarButton(ModeBarIcon.Delay, Ctx.DelaySeconds > 0 ? $"{Ctx.DelaySeconds}s delay" : "Delay", "Delay (Space cycles)", "Sp"),
    };

    protected override void Enter()
    {
        (_x, _y) = MonitorEnumerator.CursorPos();
        _barMonitor = VirtualDesktop.MonitorAt(Ctx.Monitors, _x, _y);
        _savedCenter = Ctx.Prefs.ModeBarCenter;
        Ctx.Overlays.Dim(_ => true, null);
        Ctx.Overlays.Highlight(null);
        Ctx.Overlays.Adorn(null, false, null);
        ShowBar();
    }

    protected override void Leave() { _dragging = false; SaveBarPlace(); Ctx.Overlays.ModeBar(null, -2, _x, _y); }

    protected override void RefreshHint() => ShowBar();

    private void ShowBar() => Ctx.Overlays.ModeBar(Buttons, _hover, _x, _y, Ctx.Prefs.ModeBarCenter);

    /// <summary>Bar hit-test: physical px → bar-local units on the bar's monitor.</summary>
    private ModeBarLayout.Hit? HitAt(int x, int y)
    {
        if (_barMonitor is not { } m) return null;
        double s = m.Scale;
        return ModeBarLayout.HitTest((x - m.Bounds.X) / s - BarLeft(m), (y - m.Bounds.Y) / s, Buttons.Count);
    }

    private double BarLeft(MonitorInfo m) =>
        ModeBarLayout.Left(Ctx.Prefs.ModeBarCenter, m.Bounds.Width / m.Scale, ModeBarLayout.BarWidth(Buttons.Count));

    protected override void OnMove(int x, int y)
    {
        if (_dragging && _barMonitor is { } dm)
        {
            double s = dm.Scale, w = dm.Bounds.Width / s;
            double left = (x - dm.Bounds.X) / s - _grab;
            double c = ModeBarLayout.CenterFor(left, w, ModeBarLayout.BarWidth(Buttons.Count));
            if (c != Ctx.Prefs.ModeBarCenter) { Ctx.Prefs.ModeBarCenter = c; ShowBar(); }
            return;
        }
        var hit = HitAt(x, y);
        int hover = hit is null ? -2 : hit.Value.IsGrip ? -3 : hit.Value.IsClose ? -1 : hit.Value.Index;
        if (hover == _hover) return;
        _hover = hover;
        ShowBar();
    }

    protected override bool OnLeftDown(int x, int y)
    {
        if (HitAt(x, y) is not { } hit) return true;
        if (hit.IsGrip && _barMonitor is { } gm)
        {
            _dragging = true; _hover = -3;
            _grab = (x - gm.Bounds.X) / gm.Scale - BarLeft(gm);
            ShowBar();
            return true;
        }
        if (hit.IsClose) { Finish(PickOutcome.Cancel()); return true; }
        switch (hit.Index)
        {
            case 0: Finish(PickOutcome.SwitchTo(PickMode.Element, viaMouse: true)); break;
            case 1: Finish(PickOutcome.SwitchTo(PickMode.Region, viaMouse: true)); break;
            case 2: Finish(PickOutcome.SwitchTo(PickMode.Window, viaMouse: true)); break;
            case 3: Finish(PickOutcome.SwitchTo(PickMode.FullScreen, FullScreenTarget.Current, viaMouse: true)); break;
            case 4: Finish(PickOutcome.SwitchTo(PickMode.Freehand, viaMouse: true)); break;
            case 5: Ctx.ScrollCapture = true; Finish(PickOutcome.SwitchTo(PickMode.Element, viaMouse: true)); break;
            case 6: Ctx.DelaySeconds = DelayCycle.Next(Ctx.DelaySeconds); RefreshHint(); break;
        }
        return true;
    }

    protected override bool OnLeftUp(int x, int y)
    {
        if (!_dragging) return true;
        _dragging = false;
        if (!_saveQueued && Ctx.Prefs.ModeBarCenter != _savedCenter)
        {
            // Leave the native mouse callback before saving preferences to disk.
            _saveQueued = true;
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _saveQueued = false;
                if (!_dragging) SaveBarPlace();
            }, DispatcherPriority.Normal);
        }
        return true;
    }

    private void SaveBarPlace()
    {
        double center = Ctx.Prefs.ModeBarCenter;
        if (center == _savedCenter) return;
        try
        {
            Ctx.SavePrefs?.Invoke();
            _savedCenter = center;
        }
        catch (Exception ex) { Log.Warn("Could not save mode bar position: " + ex.Message); }
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
