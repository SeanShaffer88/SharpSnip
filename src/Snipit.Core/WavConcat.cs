namespace Snipit.Core;

/// <summary>
/// Joins same-format WAV streams end-to-end (a paused recording's audio
/// segments) by streaming: reads and writes in chunks, so segment count and
/// length cost no memory. Every input must share the first one's sample rate
/// and channel count.
/// </summary>
public static class WavConcat
{
    public static void Concatenate(IReadOnlyList<Stream> inputs, Stream output)
    {
        if (inputs is null || inputs.Count == 0) throw new ArgumentException("At least one input is required.", nameof(inputs));
        using var first = new WavReader(inputs[0], leaveOpen: true);
        int rate = first.SampleRate, channels = first.Channels;
        using var writer = new WavWriter(output, rate, channels);
        var buffer = new short[8192 * channels];
        Copy(first, writer, buffer);
        for (int i = 1; i < inputs.Count; i++)
        {
            using var reader = new WavReader(inputs[i], leaveOpen: true);
            if (reader.SampleRate != rate || reader.Channels != channels)
                throw new InvalidDataException("All WAV segments must share a sample rate and channel count.");
            Copy(reader, writer, buffer);
        }
    }

    private static void Copy(WavReader reader, WavWriter writer, short[] buffer)
    {
        int n;
        while ((n = reader.Read(buffer)) > 0) writer.Write(buffer.AsSpan(0, n));
    }
}
