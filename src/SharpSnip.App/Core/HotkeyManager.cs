using System.Windows.Interop;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

/// <summary>Message-only window that owns the global hotkeys and watches for display changes.</summary>
internal sealed class HotkeyManager : IDisposable
{
    public const int IdMenu = 1, IdFullScreen = 2;

    private readonly HwndSource _source;
    private readonly HashSet<int> _registered = new();

    public event Action<int>? HotkeyPressed;
    public event Action? DisplayChanged;

    public HotkeyManager()
    {
        var p = new HwndSourceParameters("SharpSnipMessageWindow") { ParentWindow = NativeMethods.HWND_MESSAGE, Width = 0, Height = 0 };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    /// <summary>Register (or re-register) a hotkey. Returns false when another app already owns it.</summary>
    public bool Register(int id, HotkeySpec spec)
    {
        Unregister(id);
        if (!NativeMethods.RegisterHotKey(_source.Handle, id, (uint)spec.Mods | NativeMethods.MOD_NOREPEAT, (uint)spec.VirtualKey))
        {
            Log.Warn($"RegisterHotKey({spec}) failed, Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
            return false;
        }
        _registered.Add(id);
        return true;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id)) NativeMethods.UnregisterHotKey(_source.Handle, id);
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
        foreach (int id in _registered.ToList()) Unregister(id);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
