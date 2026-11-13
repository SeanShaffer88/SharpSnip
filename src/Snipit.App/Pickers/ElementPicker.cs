using System.Windows.Threading;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>Element picker (§3A): hover highlights a UIA element, click captures exactly its rectangle.</summary>
internal sealed class ElementPicker : PickerBase
{
    private readonly UiaTreeWalker _walker = new();
    private DispatcherTimer? _timer;
    private int _curX, _curY, _lastX = int.MinValue, _lastY = int.MinValue;
    private bool _busy;
    private long _pauseUntil;
    private UiaSnapshot? _snap;

    public ElementPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Element;

    protected override void Enter()
    {
        (_curX, _curY) = MonitorEnumerator.CursorPos();
        Ctx.Overlays.Dim(_ => true, null);
        Ctx.Overlays.Adorn(null, false, null);
        Ctx.Overlays.Hud(null, 0, 0);
        RefreshHint();
        // Sample every ~50 ms (§3A.2) instead of on every mouse move; stops when the picker ends.
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => _ = SampleAsync();
        _timer.Start();
    }

    protected override void Leave()
    {
        _timer?.Stop();
        _timer = null;
    }

    protected override void OnMove(int x, int y) { _curX = x; _curY = y; }

    private async Task SampleAsync()
    {
        if (_busy || Environment.TickCount64 < _pauseUntil) return;
        if (Math.Abs(_curX - _lastX) < 2 && Math.Abs(_curY - _lastY) < 2) return;
        _busy = true;
        int x = _curX, y = _curY;
        _lastX = x; _lastY = y;
        try
        {
            var s = await Ctx.Uia.RunAsync(() => _walker.Hover(x, y)).WaitAsync(TimeSpan.FromSeconds(1));
            Apply(s);
        }
        catch (TimeoutException)
        {
            Log.Warn("UIA hover timed out (>1 s); pausing sampling briefly");
            _pauseUntil = Environment.TickCount64 + 500;
            Apply(null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warn("UIA hover failed: " + ex.Message);
            Apply(null);
        }
        finally { _busy = false; }
    }

    private async Task NavigateAsync(Func<UiaSnapshot?> op)
    {
        try
        {
            Apply(await Ctx.Uia.RunAsync(op).WaitAsync(TimeSpan.FromSeconds(1)));
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { Log.Warn("UIA navigation failed: " + ex.Message); }
    }

    private void Apply(UiaSnapshot? s)
    {
        _snap = s;
        if (s is null || s.Rect.IsEmpty)
        {
            Ctx.Overlays.Highlight(null);
            Ctx.Overlays.Dim(_ => true, null);
        }
        else
        {
            Ctx.Overlays.Highlight(s.Rect);
            Ctx.Overlays.Dim(_ => true, s.Rect);
        }
        RefreshHint();
    }

    protected override void RefreshHint()
    {
        string head = _snap is null || _snap.Rect.IsEmpty
            ? "No UI element here — [R] region"
            : $"{_snap.ControlType} ▸ '{Trim(_snap.Name)}'{(_snap.Fallback ? "  (no UIA — will switch to region)" : "")}";
        string lead = Ctx.ScrollCapture ? "Scrolling capture — click the scrollable element.   " : "";
        Ctx.Overlays.HintAt($"{lead}{head}   [Tab] cycle · [PgUp/Dn] parent/child · [Ctrl+Wheel] expand · [Space] delay {DelayText} · [Esc] back", _curX, _curY);
    }

    private static string Trim(string s) => s.Length <= 40 ? s : s[..39] + "…";

    protected override bool HandleKey(int vk)
    {
        int x = _curX, y = _curY;
        switch (vk)
        {
            case VkTab: bool rev = ShiftDown(); _ = NavigateAsync(() => _walker.Next(rev)); return true;
            case VkPgUp: _ = NavigateAsync(() => _walker.ParentStep()); return true;
            case VkPgDn: _ = NavigateAsync(() => _walker.ChildStep(x, y)); return true;
        }
        return base.HandleKey(vk);
    }

    protected override bool OnWheel(int x, int y, int delta)
    {
        if (!NativeMethods.KeyDown(NativeMethods.VK_CONTROL)) return false;
        _ = NavigateAsync(delta > 0 ? _walker.Expand : _walker.Contract);
        return true;
    }

    protected override bool OnLeftUp(int x, int y)
    {
        _ = ConfirmAsync();
        return true;
    }

    private async Task ConfirmAsync()
    {
        var s = _snap;
        // §3A.3: nothing usable (no rect, or app exposes no UIA) → fall back to the region picker.
        if (s is null || s.Rect.IsEmpty || s.Fallback) { Finish(PickOutcome.SwitchTo(PickMode.Region)); return; }
        string? path = null;
        try { path = await Ctx.Uia.RunAsync(_walker.CurrentPath).WaitAsync(TimeSpan.FromSeconds(1)); }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { }
        if (Ctx.ScrollCapture)
        {
            try { Ctx.ScrollTarget = await Ctx.Uia.RunAsync(_walker.CurrentElement).WaitAsync(TimeSpan.FromSeconds(1)); }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { }
            var scrollMeta = new CaptureMeta("Scrolling", WindowEnumerator.ProcessName(s.ProcessId), s.Name, s.ControlType, s.AutomationId, s.ClassName, path);
            Finish(PickOutcome.Picked(new PickResult(s.Rect, scrollMeta)));
            return;
        }
        var meta = new CaptureMeta("Element", WindowEnumerator.ProcessName(s.ProcessId), s.Name, s.ControlType, s.AutomationId, s.ClassName, path);
        Finish(PickOutcome.Picked(new PickResult(s.Rect, meta)));
    }
}
