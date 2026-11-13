using SharpSnip.Core;

namespace SharpSnip;

/// <summary>MODE_MENU HUD (§2.3). Chooses a picker; never captures by itself.</summary>
internal sealed class MenuPicker : PickerBase
{
    private int _x, _y;

    public MenuPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Menu;

    protected override void Enter()
    {
        (_x, _y) = MonitorEnumerator.CursorPos();
        Ctx.Overlays.Dim(_ => true, null);
        Ctx.Overlays.Highlight(null);
        Ctx.Overlays.Adorn(null, false, null);
        RefreshHint();
    }

    protected override void RefreshHint()
    {
        string text =
            "SharpSnip — choose a mode\n" +
            "[E] Element    [R] Region    [W] Window\n" +
            "[C] Scrolling capture (element)\n" +
            $"[S] Fixed-size region ({Ctx.Prefs.FixedWidth}×{Ctx.Prefs.FixedHeight})\n" +
            "[F] Full screen (all monitors)\n" +
            "[D] Full screen (this monitor)\n" +
            "[1..9] Full screen (monitor N)\n" +
            $"[Space] delay: {DelayText}    [Esc] cancel";
        Ctx.Overlays.Hud(text, _x, _y);
    }

    protected override void OnMove(int x, int y) { }

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
