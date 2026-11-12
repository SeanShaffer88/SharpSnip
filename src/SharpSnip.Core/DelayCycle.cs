namespace SharpSnip.Core;

/// <summary>Capture delay presets: 0 → 3 → 5 → 10 → 0 seconds.</summary>
public static class DelayCycle
{
    public static readonly IReadOnlyList<int> Steps = new[] { 0, 3, 5, 10 };

    public static int Next(int current)
    {
        int idx = Steps.ToList().IndexOf(current);
        return Steps[(idx + 1) % Steps.Count];
    }

    public static int Sanitize(int value) => Steps.Contains(value) ? value : 0;
}
