using System.Diagnostics;
using Snipit.Core;

namespace Snipit;

/// <summary>Anything the host can drive as "the active recording".</summary>
internal interface IRecording : IDisposable
{
    string OutputPath { get; }
    /// <summary>Set when stop finished but something went wrong (e.g. audio could not be added); null when clean.</summary>
    string? Warning => null;
    TimeSpan Elapsed { get; }
    bool IsPaused { get; }
    void SetPaused(bool paused);
    Task StopAsync();
    event Action? LimitReached;
}

/// <summary>
/// Watches a recording's elapsed time and file size on a 1 s timer and raises
/// Reached once when a limit trips (Core RecordingPlan.LimitHit does the math).
/// </summary>
internal sealed class LimitWatcher : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly Stopwatch _clock = new();
    private readonly int _maxSeconds;
    private readonly long _maxBytes;
    private readonly Func<long> _sizeOf;
    private bool _fired;

    public event Action? Reached;

    public LimitWatcher(int maxSeconds, long maxBytes, Func<long> sizeOf)
    {
        _maxSeconds = maxSeconds; _maxBytes = maxBytes; _sizeOf = sizeOf;
        _clock.Start();
        _timer = new System.Threading.Timer(Check, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private bool _sizeWarned;

    private void Check(object? _)
    {
        if (_fired) return;
        long size = 0;
        try { size = _sizeOf(); } catch (Exception ex) { if (!_sizeWarned) { _sizeWarned = true; Log.Warn("Recording size check failed: " + ex.Message); } }
        if (RecordingPlan.LimitHit(_clock.Elapsed, _maxSeconds, size, _maxBytes))
        {
            _fired = true;
            Log.Info($"Recording limit reached ({_clock.Elapsed.TotalSeconds:0} s, {size} bytes)");
            Reached?.Invoke();
        }
    }

    public void Dispose() => _timer.Dispose();
}
