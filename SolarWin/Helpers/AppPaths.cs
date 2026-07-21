namespace SolarWin.Helpers;

/// <summary>Local app data roots for unpackaged / packaged SolarWin.</summary>
public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SolarWin");

    public static string CacheDirectory => Path.Combine(RootDirectory, "cache");

    public static string AccountsFilePath => Path.Combine(RootDirectory, "accounts.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }
}
