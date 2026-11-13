using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Snipit.Core;
using Snipit.Native;
using WinRT;

namespace Snipit;

/// <summary>
/// Windows.Graphics.Capture stills, one monitor per call. This is the backend
/// that keeps working where GDI BitBlt returns black (hardware overlays, DRM
/// video, some games). Raw COM interop only — no third-party packages, in line
/// with the project's zero-dependency charter. Returns null on any failure and
/// lets the caller fall back to GDI. Runtime behaviour is Windows-only; see
/// AUDIT.md for what is and is not verified.
/// </summary>
internal static class WgcCapture
{
    public static bool IsSupported
    {
        get { try { return GraphicsCaptureSession.IsSupported(); } catch { return false; } }
    }

    // The D3D11 device is process-shared (§1.4 pre-warm): creating one per capture
    // made the first capture pay full device + pipeline setup. Captures are
    // serialized by the app (the session gate), so the shared context sees one
    // caller at a time; any failure drops the cache, that capture falls back to
    // GDI as before, and the next capture builds a fresh device.
    private static readonly object _deviceLock = new();
    private static (IDirect3DDevice Device, IntPtr D3d, IntPtr Context)? _shared;

    private static (IDirect3DDevice Device, IntPtr D3d, IntPtr Context)? SharedDevice()
    {
        lock (_deviceLock)
        {
            _shared ??= CreateDevice();
            return _shared;
        }
    }

    private static void InvalidateDevice()
    {
        lock (_deviceLock)
        {
            if (_shared is not { } s) return;
            _shared = null;
            try { s.Device.Dispose(); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); }
            if (s.Context != IntPtr.Zero) Marshal.Release(s.Context);
            if (s.D3d != IntPtr.Zero) Marshal.Release(s.D3d);
        }
    }

    /// <summary>
    /// §1.4 pre-warm, run once at startup on a background thread: create the
    /// shared device and pull one throwaway frame through the full WGC pipeline
    /// (broker, pool, session) so the user's first capture is served warm.
    /// Best-effort — any failure just leaves the cold path (and the GDI fallback).
    /// </summary>
    public static void Prewarm()
    {
        try
        {
            if (!IsSupported) return;
            var dev = SharedDevice();
            if (dev is null) return;
            MonitorEnumerator.Refresh();
            MonitorInfo? primary = null;
            foreach (var m in MonitorEnumerator.Current) { if (m.IsPrimary) { primary = m; break; } primary ??= m; }
            if (primary is null) return;
            var hmon = MonitorFromRect(primary.Bounds);
            if (hmon == IntPtr.Zero) return;
            var item = CreateItemForMonitor(hmon);
            if (item is null) return;
            using (var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(dev.Value.Device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size))
            using (var session = pool.CreateCaptureSession(item))
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                    session.IsCursorCaptureEnabled = false;
                using var arrived = new ManualResetEventSlim();
                pool.FrameArrived += (p, _) => { using var f = p.TryGetNextFrame(); arrived.Set(); };
                session.StartCapture();
                arrived.Wait(TimeSpan.FromSeconds(2));
            }
            Log.Info("WGC pre-warm complete");
        }
        catch (Exception ex) { Log.Info("WGC pre-warm skipped: " + ex.Message); }
    }

    /// <summary>The monitor's full frame as tight BGRA (stride = width×4), or null when WGC could not serve it.</summary>
    public static (byte[] Bgra, int Width, int Height)? TryCaptureMonitor(MonitorInfo monitor)
    {
        try
        {
            var hmon = MonitorFromRect(monitor.Bounds);
            if (hmon == IntPtr.Zero) return null;
            var item = CreateItemForMonitor(hmon);
            if (item is null) return null;
            var dev = SharedDevice();
            if (dev is null) return null;
            using (var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(dev.Value.Device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size))
            using (var session = pool.CreateCaptureSession(item))
            {
                // IsBorderRequired exists only in newer SDK contracts than the 19041
                // target, so it cannot be set from here; for a full-monitor item the
                // border overlay sits on the screen edge, outside the captured frame.
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                    session.IsCursorCaptureEnabled = false; // match GDI: the cursor is not part of a still

                using var arrived = new ManualResetEventSlim();
                Direct3D11CaptureFrame? frame = null;
                pool.FrameArrived += (p, _) => { frame ??= p.TryGetNextFrame(); arrived.Set(); };
                session.StartCapture();
                if (!arrived.Wait(TimeSpan.FromSeconds(2)) || frame is null) return null;
                using (frame)
                {
                    var pixels = ReadSurface(frame, dev.Value.D3d, dev.Value.Context, item.Size);
                    if (pixels is null || LooksBlack(pixels)) return null;
                    return (pixels, item.Size.Width, item.Size.Height);
                }
            }
        }
        catch { InvalidateDevice(); return null; }
    }

    /// <summary>A frame with no visible content at all (every sampled RGB byte 0) means the path failed, not that the screen is black.</summary>
    private static bool LooksBlack(byte[] bgra)
    {
        for (int i = 0; i + 2 < bgra.Length; i += 997 * 4)
            if (bgra[i] != 0 || bgra[i + 1] != 0 || bgra[i + 2] != 0) return false;
        return true;
    }

    /// <summary>Creates the D3D11 device + WinRT device pair the capture paths share. Caller releases the raw pointers.</summary>
    internal static (IDirect3DDevice Device, IntPtr D3d, IntPtr Context)? CreateDevice()
    {
        IntPtr d3d = IntPtr.Zero, context = IntPtr.Zero;
        try
        {
            int hr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT, IntPtr.Zero, 0, D3D11_SDK_VERSION,
                out d3d, out _, out context);
            if (hr < 0 || d3d == IntPtr.Zero) return null;
            var device = CreateDirect3DDevice(d3d);
            if (device is null) return null;
            return (device, d3d, context);
        }
        catch
        {
            if (context != IntPtr.Zero) Marshal.Release(context);
            if (d3d != IntPtr.Zero) Marshal.Release(d3d);
            return null;
        }
    }

    internal static byte[]? ReadSurface(Direct3D11CaptureFrame frame, IntPtr d3dDevice, IntPtr context, SizeInt32 size)
    {
        IntPtr tex = IntPtr.Zero, staging = IntPtr.Zero;
        try
        {
            var access = QueryInterface<IDirect3DDxgiInterfaceAccess>(frame.Surface);
            if (access is null) return null;
            var texIid = typeof(ID3D11Texture2D).GUID;
            if (access.GetInterface(ref texIid, out tex) < 0 || tex == IntPtr.Zero) return null;

            var device = (ID3D11Device)Marshal.GetTypedObjectForIUnknown(d3dDevice, typeof(ID3D11Device));
            var ctx = (ID3D11DeviceContext)Marshal.GetTypedObjectForIUnknown(context, typeof(ID3D11DeviceContext));
            var desc = new TEXTURE2D_DESC
            {
                Width = (uint)size.Width, Height = (uint)size.Height, MipLevels = 1, ArraySize = 1,
                Format = DXGI_FORMAT_B8G8R8A8_UNORM, SampleCount = 1, SampleQuality = 0,
                Usage = D3D11_USAGE_STAGING, BindFlags = 0, CPUAccessFlags = D3D11_CPU_ACCESS_READ, MiscFlags = 0,
            };
            if (device.CreateTexture2D(ref desc, IntPtr.Zero, out staging) < 0 || staging == IntPtr.Zero) return null;

            ctx.CopyResource(staging, tex);
            if (ctx.Map(staging, 0, D3D11_MAP_READ, 0, out var mapped) < 0 || mapped.pData == IntPtr.Zero) return null;
            try
            {
                var bytes = new byte[size.Width * size.Height * 4];
                for (int row = 0; row < size.Height; row++)
                    Marshal.Copy(mapped.pData + row * (int)mapped.RowPitch, bytes, row * size.Width * 4, size.Width * 4);
                return bytes;
            }
            finally { ctx.Unmap(staging, 0); }
        }
        catch { return null; }
        finally
        {
            if (staging != IntPtr.Zero) Marshal.Release(staging);
            if (tex != IntPtr.Zero) Marshal.Release(tex);
        }
    }

    internal static IntPtr MonitorFromRect(RectPx bounds)
    {
        var rc = new NativeMethods.RECT { Left = bounds.X, Top = bounds.Y, Right = bounds.X + bounds.Width, Bottom = bounds.Y + bounds.Height };
        return MonitorFromRectNative(ref rc, MONITOR_DEFAULTTONULL);
    }

    internal static GraphicsCaptureItem? CreateItemForMonitor(IntPtr hmon)
    {
        string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        if (WindowsCreateString(className, (uint)className.Length, out IntPtr classId) < 0) return null;
        IntPtr factoryPtr = IntPtr.Zero;
        try
        {
            var interopIid = typeof(IGraphicsCaptureItemInterop).GUID;
            if (RoGetActivationFactory(classId, ref interopIid, out factoryPtr) < 0 || factoryPtr == IntPtr.Zero) return null;
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetTypedObjectForIUnknown(factoryPtr, typeof(IGraphicsCaptureItemInterop));
            var itemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760"); // IGraphicsCaptureItem
            if (interop.CreateForMonitor(hmon, ref itemIid, out IntPtr itemPtr) < 0 || itemPtr == IntPtr.Zero) return null;
            return MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPtr);
        }
        catch { return null; }
        finally
        {
            if (factoryPtr != IntPtr.Zero) Marshal.Release(factoryPtr);
            WindowsDeleteString(classId);
        }
    }

    internal static IDirect3DDevice? CreateDirect3DDevice(IntPtr d3dDevice)
    {
        IntPtr dxgi = IntPtr.Zero, inspectable = IntPtr.Zero;
        try
        {
            var dxgiIid = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C"); // IDXGIDevice
            if (Marshal.QueryInterface(d3dDevice, in dxgiIid, out dxgi) < 0 || dxgi == IntPtr.Zero) return null;
            if (CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable) < 0 || inspectable == IntPtr.Zero) return null;
            return MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
        }
        catch { return null; }
        finally { if (dxgi != IntPtr.Zero) Marshal.Release(dxgi); }
    }

    private static T? QueryInterface<T>(object winrtObject) where T : class
    {
        IntPtr unk = Marshal.GetIUnknownForObject(winrtObject);
        try
        {
            var iid = typeof(T).GUID;
            if (Marshal.QueryInterface(unk, in iid, out IntPtr ptr) < 0 || ptr == IntPtr.Zero) return null;
            try { return (T)Marshal.GetTypedObjectForIUnknown(ptr, typeof(T)); }
            finally { Marshal.Release(ptr); }
        }
        catch { return null; }
        finally { Marshal.Release(unk); }
    }

    // ---- native + COM interop ---------------------------------------------------

    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D11_SDK_VERSION = 7;
    private const int DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    private const int D3D11_USAGE_STAGING = 3;
    private const uint D3D11_CPU_ACCESS_READ = 0x20000;
    private const int D3D11_MAP_READ = 1;
    private const uint MONITOR_DEFAULTTONULL = 0;

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int chosenLevel, out IntPtr context);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr direct3DDevice);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, uint length, out IntPtr reference);

    [DllImport("combase.dll")]
    private static extern void WindowsDeleteString(IntPtr reference);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("user32.dll", EntryPoint = "MonitorFromRect")]
    private static extern IntPtr MonitorFromRectNative(ref NativeMethods.RECT rect, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct TEXTURE2D_DESC
    {
        public uint Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CPUAccessFlags, MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MAPPED_SUBRESOURCE { public IntPtr pData; public uint RowPitch; public uint DepthPitch; }

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(IntPtr hwnd, ref Guid iid, out IntPtr result);
        [PreserveSig] int CreateForMonitor(IntPtr hmon, ref Guid iid, out IntPtr result);
    }

    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig] int GetInterface(ref Guid iid, out IntPtr ppv);
    }

    [ComImport, Guid("6F15AAF2-D208-4E89-9AB4-489535D34F9C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11Texture2D { }

    [ComImport, Guid("DB6F6DDB-AC77-4E88-8253-819DF9BBF140"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11Device
    {
        [PreserveSig] int CreateBuffer(IntPtr desc, IntPtr initialData, out IntPtr buffer);
        [PreserveSig] int CreateTexture1D(IntPtr desc, IntPtr initialData, out IntPtr texture);
        [PreserveSig] int CreateTexture2D(ref TEXTURE2D_DESC desc, IntPtr initialData, out IntPtr texture);
    }

    /// <summary>Only Map, Unmap and CopyResource are ever called; the other slots exist to keep the vtable order honest.</summary>
    [ComImport, Guid("C0BFA96C-E089-44FB-8EAF-26F8796190DA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11DeviceContext
    {
        [PreserveSig] void S03(); [PreserveSig] void S04(); [PreserveSig] void S05(); [PreserveSig] void S06();
        [PreserveSig] void S07(); [PreserveSig] void S08(); [PreserveSig] void S09(); [PreserveSig] void S10();
        [PreserveSig] void S11(); [PreserveSig] void S12(); [PreserveSig] void S13();
        [PreserveSig] int Map(IntPtr resource, uint subresource, int mapType, uint mapFlags, out MAPPED_SUBRESOURCE mapped);
        [PreserveSig] void Unmap(IntPtr resource, uint subresource);
        [PreserveSig] void S16(); [PreserveSig] void S17(); [PreserveSig] void S18(); [PreserveSig] void S19();
        [PreserveSig] void S20(); [PreserveSig] void S21(); [PreserveSig] void S22(); [PreserveSig] void S23();
        [PreserveSig] void S24(); [PreserveSig] void S25(); [PreserveSig] void S26(); [PreserveSig] void S27();
        [PreserveSig] void S28(); [PreserveSig] void S29(); [PreserveSig] void S30(); [PreserveSig] void S31();
        [PreserveSig] void S32(); [PreserveSig] void S33(); [PreserveSig] void S34(); [PreserveSig] void S35();
        [PreserveSig] void S36(); [PreserveSig] void S37(); [PreserveSig] void S38(); [PreserveSig] void S39();
        [PreserveSig] void S40(); [PreserveSig] void S41(); [PreserveSig] void S42(); [PreserveSig] void S43();
        [PreserveSig] void S44(); [PreserveSig] void S45(); [PreserveSig] void S46();
        [PreserveSig] void CopyResource(IntPtr dst, IntPtr src);
    }
}
