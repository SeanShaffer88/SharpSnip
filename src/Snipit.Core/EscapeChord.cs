namespace Snipit.Core;

/// <summary>
/// Detects a panic gesture: <see cref="Presses"/> presses of the same key within <see cref="WindowMs"/> milliseconds.
/// Fed with a millisecond tick on every press; returns true on the press that completes the chord, then starts over.
/// </summary>
public sealed class EscapeChord
{
    public const int Presses = 3;
    public const int WindowMs = 1500;

    private readonly long[] _ticks = new long[Presses];
    private int _count;

    public bool Press(long tickMs)
    {
        // Keep only presses still inside the window (and discard any that look like they are from the future: the tick source reset).
        int kept = 0;
        for (int i = 0; i < _count; i++)
            if (tickMs - _ticks[i] is >= 0 and < WindowMs) _ticks[kept++] = _ticks[i];
        _count = kept;
        _ticks[_count++] = tickMs;
        if (_count < Presses) return false;
        _count = 0;
        return true;
    }
}
