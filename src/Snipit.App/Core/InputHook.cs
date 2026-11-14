using System.Runtime.InteropServices;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>
/// Low-level mouse + keyboard hooks (§3A.1), installed on the UI thread. Handlers return true to swallow
/// the event. Overlays are click-through, so this is the only thing that sees picker input.
/// Keys pressed with Ctrl/Alt/Win held are never swallowed, so system shortcuts keep working.
/// </summary>
internal sealed class InputHook : IDisposable
{
    private IntPtr _kb, _ms;
    private readonly NativeMethods.HookProc _kbProc, _msProc;
    private readonly HashSet<int> _swallowedKeys = new();
    private bool _leftSwallowed, _rightSwallowed;

    public Func<int, bool>? KeyDown;                 // vk
    public Func<int, int, bool>? LeftDown, LeftUp, RightDown;   // x, y
    public Action<int, int>? Move;                   // x, y
    public Func<int, int, int, bool>? Wheel;         // x, y, delta

    /// <summary>Called when Esc is pressed three times within <see cref="EscapeChord.WindowMs"/>, before any picker handler sees the key. The way out of a stuck overlay.</summary>
    public Action? PanicEscape;
    private readonly EscapeChord _chord = new();

    public long LastInputTick { get; private set; } = Environment.TickCount64;
    public bool IsActive => _kb != IntPtr.Zero || _ms != IntPtr.Zero;

    public InputHook()
    {
        _kbProc = KeyboardProc;
        _msProc = MouseProc;
    }

    public void Start()
    {
        if (IsActive) return;
        IntPtr mod = NativeMethods.GetModuleHandle(null);
        _kb = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbProc, mod, 0);
        _ms = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _msProc, mod, 0);
        if (_kb == IntPtr.Zero || _ms == IntPtr.Zero)
        {
            Log.Error($"SetWindowsHookEx failed, Win32 error {Marshal.GetLastWin32Error()}");
            Dispose();
            throw new InvalidOperationException("Could not install input hooks.");
        }
        LastInputTick = Environment.TickCount64;
    }

    /// <summary>Removes the current picker's handlers. Panic escape belongs to the whole session.</summary>
    public void ClearHandlers()
    {
        KeyDown = null; LeftDown = null; LeftUp = null; RightDown = null; Move = null; Wheel = null;
    }

    private IntPtr KeyboardProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            try
            {
                var s = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if ((s.flags & NativeMethods.LLKHF_INJECTED) == 0)
                {
                    int msg = wParam.ToInt32(), vk = (int)s.vkCode;
                    bool down = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
                    bool up = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
                    if (down)
                    {
                        LastInputTick = Environment.TickCount64;
                        if (vk == 0x1B && PanicEscape is { } panic && _chord.Press(LastInputTick)) { try { panic(); } catch (Exception ex) { Log.Error("Panic escape failed", ex); } }
                        if (!NativeMethods.CtrlAltWinDown() && KeyDown?.Invoke(vk) == true)
                        {
                            _swallowedKeys.Add(vk);
                            return (IntPtr)1;
                        }
                    }
                    else if (up && _swallowedKeys.Remove(vk))
                    {
                        return (IntPtr)1;
                    }
                }
            }
            catch (Exception ex) { Log.Error("Keyboard hook handler failed", ex); }
        }
        return NativeMethods.CallNextHookEx(_kb, code, wParam, lParam);
    }

    private IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            try
            {
                var s = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                if ((s.flags & NativeMethods.LLMHF_INJECTED) == 0)
                {
                    int x = s.pt.X, y = s.pt.Y;
                    switch (wParam.ToInt32())
                    {
                        case NativeMethods.WM_MOUSEMOVE:
                            Move?.Invoke(x, y);
                            break;
                        case NativeMethods.WM_LBUTTONDOWN:
                            LastInputTick = Environment.TickCount64;
                            if (LeftDown?.Invoke(x, y) == true) { _leftSwallowed = true; return (IntPtr)1; }
                            break;
                        case NativeMethods.WM_LBUTTONUP:
                            LastInputTick = Environment.TickCount64;
                            bool swallowUp = _leftSwallowed;
                            _leftSwallowed = false;
                            bool handled = LeftUp?.Invoke(x, y) == true;
                            if (swallowUp || handled) return (IntPtr)1;
                            break;
                        case NativeMethods.WM_RBUTTONDOWN:
                            LastInputTick = Environment.TickCount64;
                            if (RightDown?.Invoke(x, y) == true) { _rightSwallowed = true; return (IntPtr)1; }
                            break;
                        case NativeMethods.WM_RBUTTONUP:
                            if (_rightSwallowed) { _rightSwallowed = false; return (IntPtr)1; }
                            break;
                        case NativeMethods.WM_MOUSEWHEEL:
                            LastInputTick = Environment.TickCount64;
                            int delta = (short)((s.mouseData >> 16) & 0xFFFF);
                            if (Wheel?.Invoke(x, y, delta) == true) return (IntPtr)1;
                            break;
                    }
                }
            }
            catch (Exception ex) { Log.Error("Mouse hook handler failed", ex); }
        }
        return NativeMethods.CallNextHookEx(_ms, code, wParam, lParam);
    }

    public void Dispose()
    {
        ClearHandlers();
        PanicEscape = null;
        if (_kb != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_kb); _kb = IntPtr.Zero; }
        if (_ms != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_ms); _ms = IntPtr.Zero; }
        _swallowedKeys.Clear();
    }
}
