using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// A live Windows.Graphics.Capture session over one monitor. Each frame is
/// handed to the FrameReady subscriber, which owns it and must dispose it.
/// Recordings include the cursor (the stills path excludes it on purpose).
/// </summary>
internal sealed class WgcStream : IDisposable
{
    private readonly Direct3D11CaptureFramePool _pool;
    private readonly GraphicsCaptureSession _session;
    private readonly IDirect3DDevice _device;
    private readonly IntPtr _d3d, _context;
    private bool _disposed;

    public SizeInt32 Size { get; }

    public event Action<Direct3D11CaptureFrame>? FrameReady;

    private WgcStream(Direct3D11CaptureFramePool pool, GraphicsCaptureSession session, IDirect3DDevice device, IntPtr d3d, IntPtr context, SizeInt32 size)
    {
        _pool = pool; _session = session; _device = device; _d3d = d3d; _context = context; Size = size;
    }

    public static WgcStream? StartMonitor(MonitorInfo monitor)
    {
        try
        {
            var hmon = WgcCapture.MonitorFromRect(monitor.Bounds);
            if (hmon == IntPtr.Zero) return null;
            var item = WgcCapture.CreateItemForMonitor(hmon);
            if (item is null) return null;
            var created = WgcCapture.CreateDevice();
            if (created is null) return null;
            var (device, d3d, context) = created.Value;
            var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
            var session = pool.CreateCaptureSession(item);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                session.IsCursorCaptureEnabled = true;
            var stream = new WgcStream(pool, session, device, d3d, context, item.Size);
            pool.FrameArrived += stream.OnFrameArrived;
            session.StartCapture();
            return stream;
        }
        catch { return null; }
    }

    /// <summary>Reads a frame back to tight BGRA pixels (region recording crops from these). Null when the readback fails.</summary>
    public byte[]? ReadPixels(Direct3D11CaptureFrame frame) => WgcCapture.ReadSurface(frame, _d3d, _context, Size);

    private void OnFrameArrived(Direct3D11CaptureFramePool pool, object _)
    {
        if (_disposed) return;
        var frame = pool.TryGetNextFrame();
        if (frame is null) return;
        var handler = FrameReady;
        if (handler is null) { frame.Dispose(); return; }
        handler(frame);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _session.Dispose(); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); }
        try { _pool.Dispose(); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); }
        try { _device.Dispose(); } catch (Exception ex) { Log.Debug("Ignored during cleanup: " + ex.Message); }
        if (_context != IntPtr.Zero) Marshal.Release(_context);
        if (_d3d != IntPtr.Zero) Marshal.Release(_d3d);
    }
}
