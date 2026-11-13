namespace SharpSnip.Core;

/// <summary>
/// Streaming counterpart of <see cref="PcmMix.Resample"/>: the same
/// linear-interpolation math, fed in chunks. An output frame is emitted only
/// once both input frames it interpolates between have arrived; Finish()
/// flushes the tail with the same end-clamping the whole-array version uses,
/// so chunked and whole-array results are sample-identical. Consumed input is
/// dropped as the read position advances, keeping memory bounded by the chunk
/// size rather than the recording length.
/// </summary>
public sealed class PcmResampler
{
    private readonly int _channels, _fromRate, _toRate;
    private readonly bool _passthrough;
    private readonly List<short> _buf = new();
    private long _baseFrame;   // absolute frame index of _buf[0]
    private long _totalIn;     // frames fed so far
    private long _nextOut;     // next output frame index to emit
    private bool _finished;

    public PcmResampler(int channels, int fromRate, int toRate)
    {
        if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
        if (fromRate <= 0 || toRate <= 0) throw new ArgumentOutOfRangeException(nameof(fromRate));
        _channels = channels; _fromRate = fromRate; _toRate = toRate;
        _passthrough = fromRate == toRate;
    }

    /// <summary>Feeds interleaved input; returns the output frames that are now fully determined (possibly none).</summary>
    public short[] Process(ReadOnlySpan<short> input)
    {
        if (_finished) throw new InvalidOperationException("The resampler is finished.");
        if (input.Length % _channels != 0) throw new ArgumentException("Sample count must align to whole frames.", nameof(input));
        if (_passthrough) return input.ToArray();
        _buf.AddRange(input.ToArray());
        _totalIn += input.Length / _channels;
        return Emit(final: false);
    }

    /// <summary>Flushes the remaining output (the end-clamped tail). Call once, after the last Process.</summary>
    public short[] Finish()
    {
        if (_finished) return Array.Empty<short>();
        _finished = true;
        if (_passthrough) return Array.Empty<short>();
        return Emit(final: true);
    }

    private short[] Emit(bool final)
    {
        long totalOut = final ? _totalIn * _toRate / _fromRate : long.MaxValue;
        var output = new List<short>();
        while (_nextOut < totalOut)
        {
            double pos = (double)_nextOut * _fromRate / _toRate;
            long i0 = (long)pos;
            // Before the flush, both interpolation frames must really have arrived;
            // the clamp to the last frame applies only to the true end of the input.
            if (!final && i0 + 1 > _totalIn - 1) break;
            long i1 = Math.Min(i0 + 1, _totalIn - 1);
            double frac = pos - i0;
            int b0 = (int)((i0 - _baseFrame) * _channels), b1 = (int)((i1 - _baseFrame) * _channels);
            for (int c = 0; c < _channels; c++)
            {
                double v = _buf[b0 + c] * (1 - frac) + _buf[b1 + c] * frac;
                output.Add((short)Math.Round(v));
            }
            _nextOut++;
        }
        // Drop input frames the next output can no longer touch.
        long nextI0 = (long)((double)_nextOut * _fromRate / _toRate);
        long dead = Math.Min(nextI0 - _baseFrame, _totalIn - _baseFrame);
        if (dead > 4096)
        {
            _buf.RemoveRange(0, (int)(dead * _channels));
            _baseFrame += dead;
        }
        return output.ToArray();
    }
}
