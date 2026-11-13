using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// Colour picker: move over anything on screen, read its colour in a magnifier, press 1-5 or Tab to change the
/// output format, click or Enter to copy it. The monitor under the cursor is captured once (overlays are excluded
/// from capture, so the colour is the real one) and sampled from that frozen frame.
/// </summary>
internal sealed class ColorPickerPicker : PickerBase
{
    private readonly Dictionary<int, (BitmapSource Image, RectPx Rect)> _shots = new();
    private int _x, _y;
    private string _format = ColorFormat.Default;

    public ColorPickerPicker(PickerContext ctx) : base(ctx) { }
    public override PickMode Mode => PickMode.Menu;

    /// <summary>The text that was copied (set when the pick is confirmed).</summary>
    public string? Chosen { get; private set; }
    public string FormatName => _format;

    protected override void Enter()
    {
        _format = ColorFormat.Sanitize(Ctx.Prefs.ColorFormatName);
        (_x, _y) = MonitorEnumerator.CursorPos();
        Ctx.Overlays.Adorn(null, false, null);
        Ctx.Overlays.Hud(null, 0, 0);
        Update();
    }

    protected override void OnMove(int x, int y) { _x = x; _y = y; Update(); }

    private (BitmapSource Image, RectPx Rect)? ShotUnderCursor()
    {
        var m = VirtualDesktop.MonitorAt(Ctx.Monitors, _x, _y);
        if (m is null) return null;
        if (!_shots.TryGetValue(m.Index, out var shot))
        {
            var cap = CaptureEngine.Capture(m.Bounds, Ctx.Monitors, 0xFF000000u);
            BitmapSource img = cap.Image.Format == PixelFormats.Bgra32 ? cap.Image : new FormatConvertedBitmap(cap.Image, PixelFormats.Bgra32, null, 0);
            img.Freeze();
            shot = (img, cap.Rect);
            _shots[m.Index] = shot;
        }
        return shot;
    }

    private (byte R, byte G, byte B)? Sample()
    {
        try
        {
            if (ShotUnderCursor() is not { } s) return null;
            int px = _x - s.Rect.X, py = _y - s.Rect.Y;
            if (px < 0 || py < 0 || px >= s.Image.PixelWidth || py >= s.Image.PixelHeight) return null;
            var buf = new byte[4];
            s.Image.CopyPixels(new Int32Rect(px, py, 1, 1), buf, 4, 0);
            return (buf[2], buf[1], buf[0]);
        }
        catch (Exception ex)
        {
            Log.Warn("Colour sample failed: " + ex.Message);
            return null;
        }
    }

    private void Update()
    {
        Ctx.Overlays.Highlight(new RectPx(_x - 4, _y - 4, 9, 9));
        if (ShotUnderCursor() is { } s) Ctx.Overlays.Loupe(s.Image, s.Rect, _x, _y);
        RefreshHint();
    }

    protected override void RefreshHint()
    {
        string text = Sample() is { } c ? ColorFormat.Format(_format, c.R, c.G, c.B) : "(no colour here)";
        Ctx.Overlays.HintAt($"{text}  ·  format {_format} ([1]-[5] or [Tab]) · click/Enter copy · [Esc] cancel", _x, _y);
    }

    protected override bool HandleKey(int vk)
    {
        switch (vk)
        {
            case VkEsc: Finish(PickOutcome.Cancel()); return true;
            case VkEnter: Confirm(); return true;
            case VkTab: _format = ColorFormat.Next(_format); RefreshHint(); return true;
            case 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5: return false;   // bare modifiers pass through
        }
        int n = vk is >= 0x31 and <= 0x35 ? vk - 0x30 : vk is >= 0x61 and <= 0x65 ? vk - 0x60 : 0;
        if (n > 0) { _format = ColorFormat.Names[n - 1]; RefreshHint(); }
        return true;   // everything else is swallowed; this picker has no mode hops
    }

    protected override bool OnLeftUp(int x, int y) { _x = x; _y = y; Confirm(); return true; }

    private void Confirm()
    {
        if (Sample() is not { } c) return;   // nothing under the cursor: keep picking
        Chosen = ColorFormat.Format(_format, c.R, c.G, c.B);
        Ctx.Prefs.ColorFormatName = _format;
        Finish(PickOutcome.Picked(new PickResult(new RectPx(_x, _y, 1, 1), new CaptureMeta("Colour"))));
    }
}
