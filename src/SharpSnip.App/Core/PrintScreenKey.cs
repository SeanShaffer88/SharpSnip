using Microsoft.Win32;
using SharpSnip.Core;

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

    /// <summary>
    /// Gives the key back using the recorded state in <paramref name="prefs"/> and clears that record.
    /// Returns false (and touches nothing) when no override is recorded. The caller saves prefs.
    /// </summary>
    public static bool Restore(Preferences prefs)
    {
        if (!prefs.PrintScreenPriorRecorded) return false;
        Write(Prior(prefs.PrintScreenSnippingPrior));
        prefs.PrintScreenPriorRecorded = false;
        prefs.PrintScreenSnippingPrior = -1;
        return true;
    }

    /// <summary>
    /// Crash / kill / logoff path: reads the settings file itself, restores, saves. Safe to call any
    /// number of times (a no-op once restored). Never throws.
    /// </summary>
    public static bool RestoreFromFile()
    {
        try
        {
            var prefs = PrefsStore.Load(AppPaths.PrefsPath, out _);
            if (!Restore(prefs)) return false;
            PrefsStore.Save(AppPaths.PrefsPath, prefs);
            Log.Info("Print Screen switch restored from the settings file");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not restore the Print Screen switch from the settings file", ex);
            return false;
        }
    }

    /// <summary>The recorded pre-takeover state as a Write argument: -1 means the value was absent.</summary>
    public static int? Prior(int recorded) => recorded < 0 ? null : recorded;
}
