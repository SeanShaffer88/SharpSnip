namespace SharpSnip.Core;

[Flags]
public enum HotkeyMods { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

/// <summary>Global hotkey. Modifier bits match Win32 MOD_ALT/CONTROL/SHIFT/WIN.</summary>
public readonly record struct HotkeySpec(HotkeyMods Mods, int VirtualKey)
{
    private const int VkSnapshot = 0x2C;

    public static bool TryParse(string? text, out HotkeySpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var mods = HotkeyMods.None;
        int vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "win": case "windows": case "super": mods |= HotkeyMods.Win; continue;
                case "ctrl": case "control": mods |= HotkeyMods.Ctrl; continue;
                case "alt": mods |= HotkeyMods.Alt; continue;
                case "shift": mods |= HotkeyMods.Shift; continue;
            }
            if (vk != 0) return false; // two non-modifier keys
            vk = ParseKey(raw);
            if (vk == 0) return false;
        }
        if (vk == 0) return false;
        // A bare letter/digit would hijack normal typing; only Print Screen and F-keys may stand alone.
        bool mayBeBare = vk == VkSnapshot || (vk >= 0x70 && vk <= 0x87);
        if (mods == HotkeyMods.None && !mayBeBare) return false;

        spec = new HotkeySpec(mods, vk);
        return true;
    }

    private static int ParseKey(string key)
    {
        if (key.Length == 1)
        {
            char c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z') return c;
            if (c is >= '0' and <= '9') return c;
            return 0;
        }
        var k = key.ToLowerInvariant();
        if (k is "printscreen" or "prtsc" or "prtscn" or "snapshot") return VkSnapshot;
        if (k.Length is 2 or 3 && k[0] == 'f' && int.TryParse(k.AsSpan(1), out int f) && f is >= 1 and <= 24)
            return 0x70 + f - 1;
        return 0;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Mods.HasFlag(HotkeyMods.Ctrl)) parts.Add("Ctrl");
        if (Mods.HasFlag(HotkeyMods.Alt)) parts.Add("Alt");
        if (Mods.HasFlag(HotkeyMods.Shift)) parts.Add("Shift");
        if (Mods.HasFlag(HotkeyMods.Win)) parts.Add("Win");
        if (VirtualKey == VkSnapshot) parts.Add("PrintScreen");
        else if (VirtualKey >= 0x70 && VirtualKey <= 0x87) parts.Add("F" + (VirtualKey - 0x70 + 1));
        else parts.Add(((char)VirtualKey).ToString());
        return string.Join('+', parts);
    }
}
