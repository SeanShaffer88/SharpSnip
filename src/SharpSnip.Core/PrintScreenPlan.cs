namespace SharpSnip.Core;

/// <summary>
/// The hotkey side of taking the Print Screen key over from Snipping Tool:
/// switching takeover on points the menu hotkey at Print Screen and remembers
/// the key it replaces; switching it off gives the remembered key back. The
/// Windows setting itself is flipped by the app layer; what happens to the
/// keys is decided here so it is testable without Windows.
/// </summary>
public static class PrintScreenPlan
{
    public const string KeyText = "PrintScreen";

    public static bool IsPrintScreen(string? hotkeyText) =>
        HotkeySpec.TryParse(hotkeyText, out var s) && HotkeySpec.TryParse(KeyText, out var p) && s.Equals(p);

    /// <summary>
    /// Menu hotkey and the value to remember when takeover is switched on.
    /// An already-remembered key is kept (re-enabling never overwrites the
    /// original), and enabling while Print Screen is already the menu key
    /// remembers nothing new.
    /// </summary>
    public static (string MenuHotkey, string Remembered) Enable(string currentMenuHotkey, string? alreadyRemembered)
    {
        if (IsPrintScreen(currentMenuHotkey)) return (KeyText, alreadyRemembered ?? "");
        return (KeyText, string.IsNullOrEmpty(alreadyRemembered) ? currentMenuHotkey : alreadyRemembered);
    }

    /// <summary>Menu hotkey when takeover is switched off: the remembered one, when there is a valid one.</summary>
    public static string Disable(string currentMenuHotkey, string? remembered) =>
        !string.IsNullOrEmpty(remembered) && HotkeySpec.TryParse(remembered, out _) ? remembered : currentMenuHotkey;
}
