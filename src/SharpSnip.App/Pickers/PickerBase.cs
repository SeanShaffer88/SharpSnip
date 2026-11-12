using SharpSnip.Core;

namespace SharpSnip;

internal sealed record CaptureMeta(string Mode, string? App = null, string? ElementName = null, string? ControlType = null,
                                   string? AutomationId = null, string? ClassName = null, string? UiaPath = null);

/// <summary>A confirmed selection. <see cref="Resolve"/> re-computes the rect after a countdown (display may change).</summary>
internal sealed record PickResult(RectPx Rect, CaptureMeta Meta, Func<IReadOnlyList<MonitorInfo>, RectPx?>? Resolve = null);

internal enum PickKind { Cancel, Switch, Picked }

internal sealed record PickOutcome(PickKind Kind, PickMode Next = PickMode.Menu, FullScreenTarget? Target = null, PickResult? Result = null)
{
    public static PickOutcome Cancel() => new(PickKind.Cancel);
    public static PickOutcome SwitchTo(PickMode m, FullScreenTarget? t = null) => new(PickKind.Switch, m, t);
    public static PickOutcome Picked(PickResult r) => new(PickKind.Picked, Result: r);
}

internal sealed class PickerContext
{
    public required OverlayHost Overlays { get; init; }
    public required InputHook Hook { get; init; }
    public required Preferences Prefs { get; init; }
    public required UiaWorker Uia { get; init; }
    public int DelaySeconds { get; set; }
    public FullScreenTarget FullTarget { get; set; } = FullScreenTarget.Current;
    public IReadOnlyList<MonitorInfo> Monitors => MonitorEnumerator.Current;
}

/// <summary>
/// One picker owns the input hook and overlays until it finishes. Keys are swallowed while a picker is
/// active (so typing never leaks into the app underneath) except bare modifiers. Right-click = Esc.
/// </summary>
internal abstract class PickerBase
{
    protected const int VkTab = 0x09, VkEnter = 0x0D, VkEsc = 0x1B, VkSpace = 0x20, VkPgUp = 0x21, VkPgDn = 0x22;

    private TaskCompletionSource<PickOutcome>? _tcs;

    protected PickerBase(PickerContext ctx) => Ctx = ctx;

    protected PickerContext Ctx { get; }
    public abstract PickMode Mode { get; }

    public async Task<PickOutcome> RunAsync(CancellationToken ct)
    {
        _tcs = new TaskCompletionSource<PickOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => _tcs.TrySetResult(PickOutcome.Cancel()));
        var h = Ctx.Hook;
        h.ClearHandlers();
        h.KeyDown = HandleKey;
        h.LeftDown = OnLeftDown;
        h.LeftUp = OnLeftUp;
        h.RightDown = (_, _) => { Finish(PickOutcome.Cancel()); return true; };
        h.Move = OnMove;
        h.Wheel = OnWheel;
        try
        {
            Enter();
            return await _tcs.Task;
        }
        finally
        {
            h.ClearHandlers();
            Leave();
        }
    }

    protected void Finish(PickOutcome outcome) => _tcs?.TrySetResult(outcome);

    protected abstract void Enter();
    protected virtual void Leave() { }
    protected abstract void RefreshHint();
    protected virtual bool OnLeftDown(int x, int y) => true;
    protected virtual bool OnLeftUp(int x, int y) => true;
    protected virtual void OnMove(int x, int y) { }
    protected virtual bool OnWheel(int x, int y, int delta) => false;

    protected static bool ShiftDown() => Native.NativeMethods.KeyDown(Native.NativeMethods.VK_SHIFT);

    protected string DelayText => Ctx.DelaySeconds > 0 ? $"{Ctx.DelaySeconds} s" : "off";

    /// <summary>Shared keys: Esc, mode hops (E/R/W/F), Space = delay. Subclasses override and fall back to this.</summary>
    protected virtual bool HandleKey(int vk)
    {
        switch (vk)
        {
            case VkEsc: Finish(PickOutcome.Cancel()); return true;
            case VkSpace: Ctx.DelaySeconds = DelayCycle.Next(Ctx.DelaySeconds); RefreshHint(); return true;
            case 0x45: if (Mode != PickMode.Element) Finish(PickOutcome.SwitchTo(PickMode.Element)); return true;
            case 0x52: if (Mode != PickMode.Region) Finish(PickOutcome.SwitchTo(PickMode.Region)); return true;
            case 0x53: if (Mode != PickMode.FixedRegion) Finish(PickOutcome.SwitchTo(PickMode.FixedRegion)); return true;
            case 0x57: if (Mode != PickMode.Window) Finish(PickOutcome.SwitchTo(PickMode.Window)); return true;
            case 0x46: if (Mode != PickMode.FullScreen) Finish(PickOutcome.SwitchTo(PickMode.FullScreen, FullScreenTarget.Current)); return true;
            // bare modifiers pass through
            case 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5: return false;
            default: return true; // swallow everything else
        }
    }
}
