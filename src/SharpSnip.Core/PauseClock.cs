namespace SharpSnip.Core;

/// <summary>
/// Maps a recording's wall clock to its media clock by subtracting paused
/// spans: while paused the media time is frozen at the pause point, and after
/// resuming it continues from exactly there, so paused time never appears in
/// the output. Callers pass times from one consistent zero (the session's
/// first frame). Pure and deterministic — the recorders feed it frame times.
/// </summary>
public sealed class PauseClock
{
    private TimeSpan _accumulated;
    private TimeSpan? _pauseStart;

    public bool IsPaused => _pauseStart is not null;

    /// <summary>Begins a pause at wall time <paramref name="now"/>; a second Pause while paused is a no-op.</summary>
    public void Pause(TimeSpan now) => _pauseStart ??= now;

    /// <summary>Ends the open pause at wall time <paramref name="now"/>; Resume while running is a no-op.</summary>
    public void Resume(TimeSpan now)
    {
        if (_pauseStart is not { } start) return;
        _pauseStart = null;
        if (now > start) _accumulated += now - start;
    }

    /// <summary>The media time corresponding to wall time <paramref name="wall"/> (frozen while paused).</summary>
    public TimeSpan MediaTime(TimeSpan wall) => _pauseStart is { } start ? start - _accumulated : wall - _accumulated;

    /// <summary>Total time spent paused up to wall time <paramref name="now"/>, including an open pause.</summary>
    public TimeSpan PausedTotal(TimeSpan now) =>
        _accumulated + (_pauseStart is { } start && now > start ? now - start : TimeSpan.Zero);
}
