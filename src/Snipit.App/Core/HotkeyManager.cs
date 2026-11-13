using System.Windows.Interop;
using Snipit.Native;

namespace Snipit;

/// <summary>
/// Message-only window that owns Snipit's one global hotkey — Print Screen
/// opens the mode menu (owner direction, 2026-11-13: no other hotkeys exist) —
/// and watches for display changes.
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    public const int IdMenu = 1;
    private const uint VkSnapshot = 0x2C;   // VK_SNAPSHOT: the Print Screen key

    private readonly HwndSource _source;
    private bool _registered;

    public event Action<int>? HotkeyPressed;
    public event Action? DisplayChanged;

    public HotkeyManager()
    {
        var p = new HwndSourceParameters("SnipitMessageWindow") { ParentWindow = NativeMethods.HWND_MESSAGE, Width = 0, Height = 0 };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    /// <summary>Registers Print Screen as the menu hotkey. False when another app already owns the key (logged, never surfaced — the tray still opens the menu).</summary>
    public bool RegisterPrintScreen()
    {
        UnregisterPrintScreen();
        if (!NativeMethods.RegisterHotKey(_source.Handle, IdMenu, NativeMethods.MOD_NOREPEAT, VkSnapshot))
        {
            Log.Warn($"RegisterHotKey(PrintScreen) failed, Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
            return false;
        }
        _registered = true;
        return true;
    }

    public void UnregisterPrintScreen()
    {
        if (_registered) { NativeMethods.UnregisterHotKey(_source.Handle, IdMenu); _registered = false; }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            handled = true;
            HotkeyPressed?.Invoke(wParam.ToInt32());
        }
        else if (msg == NativeMethods.WM_DISPLAYCHANGE)
        {
            MonitorEnumerator.Refresh();
            DisplayChanged?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterPrintScreen();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
