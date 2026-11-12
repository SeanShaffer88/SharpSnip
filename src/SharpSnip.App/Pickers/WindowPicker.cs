using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

/// <summary>Window picker (§3C): top-level window under the cursor; [C] toggles client area only.</summary>
internal sealed class WindowPicker : PickerBase
{
    private List<WindowEntry> _windows = new();
    private WindowEntry? _hover;
    private bool _clientOnly;
    private int _x, _y;

    public WindowPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Window;

    protected override void Enter()
    {
        _windows = WindowEnumerator.Snapshot();
        (_x, _y) = MonitorEnumerator.CursorPos();
        Ctx.Overlays.Adorn(null, false, null);
        Ctx.Overlays.Hud(null, 0, 0);
        Update();
    }

    protected override void OnMove(int x, int y)
    {
        _x = x; _y = y;
        var w = WindowEnumerator.At(_windows, x, y);
        if (w?.Hwnd != _hover?.Hwnd) { _hover = w; Update(); }
        else Ctx.Overlays.HintAt(HintText(), x, y);
    }

    private RectPx? Rect => _hover is null ? null : _clientOnly ? _hover.Client : _hover.Frame;

    private void Update()
    {
        _hover ??= WindowEnumerator.At(_windows, _x, _y);
        var r = Rect;
        Ctx.Overlays.Highlight(r);
        Ctx.Overlays.Dim(_ => true, r);
        RefreshHint();
    }

    private string HintText() =>
        (_hover is null ? "No window here" : $"Window ▸ '{(_hover.Title.Length > 40 ? _hover.Title[..39] + "…" : _hover.Title)}'") +
        $"   [C] {(_clientOnly ? "client only" : "full frame")} · [Space] delay {DelayText} · [Click] capture · [Esc] back";

    protected override void RefreshHint() => Ctx.Overlays.HintAt(HintText(), _x, _y);

    protected override bool HandleKey(int vk)
    {
        if (vk == 0x43) { _clientOnly = !_clientOnly; Update(); return true; }
        return base.HandleKey(vk);
    }

    protected override bool OnLeftUp(int x, int y)
    {
        var w = _hover;
        var r = Rect;
        if (w is null || r is not { } rect || rect.IsEmpty) return true;
        Finish(PickOutcome.Picked(new PickResult(rect, new CaptureMeta("Window", WindowEnumerator.ProcessName(w.ProcessId), w.Title))));
        return true;
    }
}
