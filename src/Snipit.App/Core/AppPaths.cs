using Snipit.Core;

namespace Snipit;

/// <summary>Where settings and logs live. A "data" folder next to the exe switches the app to portable mode.</summary>
internal static class AppPaths
{
    public static string DataDir { get; } = AppDataPath.Resolve(AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    public static string PrefsPath => Path.Combine(DataDir, "settings.json");
    public static string LogDir => Path.Combine(DataDir, "logs");
}
