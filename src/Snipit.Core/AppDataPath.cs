namespace Snipit.Core;

public static class AppDataPath
{
    /// <summary>Selects the data directory without creating or moving any files.</summary>
    public static string Resolve(string appDirectory, string roamingDirectory)
    {
        string portable = Path.Combine(appDirectory, "data");
        if (Directory.Exists(portable)) return portable;

        // Keep existing settings and any pending Print Screen recovery record together.
        string legacy = Path.Combine(roamingDirectory, "SharpSnip");
        return Directory.Exists(legacy) ? legacy : Path.Combine(roamingDirectory, "Snipit");
    }
}
