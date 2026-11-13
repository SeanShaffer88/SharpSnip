namespace Snipit.Core;

/// <summary>
/// Decides whether a mouse-button release may confirm a capture, based purely
/// on the event stream — never on a physical-state probe. When a picker takes
/// over from a click (a mode-bar button, pressed on button-DOWN), that click's
/// release is still in flight; probing GetAsyncKeyState at takeover races it —
/// the probe can run after the hardware release but before the queued release
/// event is delivered, arming the picker for a phantom capture (owner field
/// reports on v0.8.0 and v0.9.0: choosing a tool took the screenshot). Armed
/// via mouse, the gate swallows exactly the inherited release; a fresh press
/// always starts a fresh gesture, so one deliberate click still captures.
/// This is the Snipping Tool contract: selecting a tool never captures —
/// capture takes its own complete click afterwards.
/// </summary>
public sealed class ClickGate
{
    private bool _awaitRelease;

    /// <summary>Call at picker takeover. viaMouse = the switch here came from a click whose release is pending.</summary>
    public void Arm(bool viaMouse) => _awaitRelease = viaMouse;

    /// <summary>A button press: ends any wait — the gesture starting now is the picker's own.</summary>
    public void Press() => _awaitRelease = false;

    /// <summary>A button release. True = it belongs to a gesture this picker saw start (may confirm); false = the inherited release ending the arming click (swallow it).</summary>
    public bool Release()
    {
        if (!_awaitRelease) return true;
        _awaitRelease = false;
        return false;
    }
}
