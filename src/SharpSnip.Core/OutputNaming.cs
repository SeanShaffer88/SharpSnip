namespace SharpSnip.Core;

public static class OutputNaming
{
    public static string FileName(DateTime local, string extension) =>
        $"{local:yyyyMMdd-HHmmss-fff}.{extension.TrimStart('.')}";

    public static string DefaultFolder(string userProfile) => Path.Combine(userProfile, "Pictures", "SharpSnip");

    public static string ResolveFolder(string configured, string userProfile) =>
        string.IsNullOrWhiteSpace(configured) ? DefaultFolder(userProfile) : configured;
}
