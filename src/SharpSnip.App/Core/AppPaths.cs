namespace SharpSnip;

/// <summary>Where settings and logs live. A "data" folder next to the exe switches the app to portable mode.</summary>
internal static class AppPaths
{
    public static string DataDir { get; } = ResolveDataDir();
    public static string PrefsPath => Path.Combine(DataDir, "settings.json");
    public static string LogDir => Path.Combine(DataDir, "logs");

    private static string ResolveDataDir()
    {
        string portable = Path.Combine(AppContext.BaseDirectory, "data");
        if (Directory.Exists(portable)) return portable;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SharpSnip");
    }
}
