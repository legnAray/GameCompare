namespace GameCompare.Persistence;

public static class AppDataPaths
{
    public static string RootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameCompare");

    public static string DatabasePath => Path.Combine(RootDirectory, "gamecompare.db");

    public static string SettingsPath => Path.Combine(RootDirectory, "settings.json");
}
