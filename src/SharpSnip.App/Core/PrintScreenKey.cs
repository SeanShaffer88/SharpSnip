using Microsoft.Win32;

namespace SharpSnip;

/// <summary>
/// The Windows switch that gives the Print Screen key to Snipping Tool:
/// HKCU\Control Panel\Keyboard, DWORD PrintScreenKeyForSnippingEnabled
/// (1 = Snipping Tool gets the key, 0 = the key is free for apps to register
/// as a hotkey). Read returns null when the value does not exist.
/// </summary>
internal static class PrintScreenKey
{
    private const string KeyPath = @"Control Panel\Keyboard";
    private const string ValueName = "PrintScreenKeyForSnippingEnabled";

    public static int? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as int?;
    }

    /// <summary>Pass null to remove the value (its state before takeover, when it was absent).</summary>
    public static void Write(int? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (value is null) key.DeleteValue(ValueName, throwOnMissingValue: false);
        else key.SetValue(ValueName, value.Value, RegistryValueKind.DWord);
    }

    /// <summary>The recorded pre-takeover state as a Write argument: -1 means the value was absent.</summary>
    public static int? Prior(int recorded) => recorded < 0 ? null : recorded;
}
