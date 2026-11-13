namespace SharpSnip.Core;

/// <summary>
/// 16-bit PCM helpers for recording: mixing two tracks (system sound + mic)
/// into one, and linear-interpolation resampling between device rates.
/// Interleaved samples throughout; frames stay aligned per channel.
/// </summary>
public static class PcmMix
{
    /// <summary>Sums two interleaved tracks sample-wise with clamping; the shorter track is silence past its end.</summary>
    public static short[] Mix(ReadOnlySpan<short> a, ReadOnlySpan<short> b)
    {
        int n = Math.Max(a.Length, b.Length);
        var result = new short[n];
        for (int i = 0; i < n; i++)
        {
            int sum = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
            result[i] = (short)Math.Clamp(sum, short.MinValue, short.MaxValue);
        }
        return result;
    }

    /// <summary>Linear-interpolation resample of interleaved PCM from one rate to another (same channel count).</summary>
    public static short[] Resample(ReadOnlySpan<short> samples, int channels, int fromRate, int toRate)
    {
        if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
        if (fromRate <= 0 || toRate <= 0) throw new ArgumentOutOfRangeException(nameof(fromRate));
        int inFrames = samples.Length / channels;
        if (inFrames == 0 || fromRate == toRate) return samples.ToArray();
        int outFrames = (int)((long)inFrames * toRate / fromRate);
        var result = new short[outFrames * channels];
        for (int f = 0; f < outFrames; f++)
        {
            double pos = (double)f * fromRate / toRate;
            int i0 = (int)pos;
            int i1 = Math.Min(i0 + 1, inFrames - 1);
            double frac = pos - i0;
            for (int c = 0; c < channels; c++)
            {
                double v = samples[i0 * channels + c] * (1 - frac) + samples[i1 * channels + c] * frac;
                result[f * channels + c] = (short)Math.Round(v);
            }
        }
        return result;
    }

    /// <summary>Converts float samples in [-1, 1] (the usual WASAPI mix format) to clamped 16-bit PCM.</summary>
    public static short FloatToPcm(float v) =>
        (short)Math.Clamp(MathF.Round(v * 32767f), -32768f, 32767f);
}
