namespace GameCompare.Playnite;

public static class PlaynitePathDetector
{
    public static string? DetectDefaultPath()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Playnite"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Playnite")
        };

        return candidates.FirstOrDefault(IsPlayniteDataDirectory);
    }

    public static bool IsPlayniteDataDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Directory.Exists(Path.Combine(path, "library")) &&
        File.Exists(Path.Combine(path, "library", "games.db")) &&
        File.Exists(Path.Combine(path, "library", "completionstatuses.db"));
}
