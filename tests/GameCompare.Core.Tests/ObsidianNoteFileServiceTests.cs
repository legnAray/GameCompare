using GameCompare.Core.Models;
using GameCompare.Core.Services;

namespace GameCompare.Core.Tests;

public sealed class ObsidianNoteFileServiceTests
{
    [Fact]
    public async Task EnsureNote_CreatesTemplateAndNeverOverwritesExistingContent()
    {
        var vaultPath = CreateVault();
        try
        {
            var game = Game("NieR:Automata");
            var service = new ObsidianNoteFileService();

            var created = await service.EnsureNoteAsync(
                game,
                vaultPath,
                ObsidianNoteFileService.DefaultNotesDirectory);
            var initialContent = await File.ReadAllTextAsync(created.FullPath);
            await File.AppendAllTextAsync(created.FullPath, "\n我的正文不会被覆盖。\n");

            var reopened = await service.EnsureNoteAsync(
                game,
                vaultPath,
                ObsidianNoteFileService.DefaultNotesDirectory);
            var finalContent = await File.ReadAllTextAsync(reopened.FullPath);

            Assert.True(created.Created);
            Assert.False(reopened.Created);
            Assert.Equal(created.FullPath, reopened.FullPath);
            Assert.Contains($"playnite_id: \"{game.PlayniteId:D}\"", initialContent);
            Assert.Contains("title: \"NieR:Automata\"", initialContent);
            Assert.Contains("## 玩后感", initialContent);
            Assert.Contains("我的正文不会被覆盖。", finalContent);
        }
        finally
        {
            Directory.Delete(vaultPath, true);
        }
    }

    [Fact]
    public async Task EnsureNote_FindsNoteMovedInsideConfiguredDirectory()
    {
        var vaultPath = CreateVault();
        try
        {
            var game = Game("Moved game");
            var service = new ObsidianNoteFileService();
            var created = await service.EnsureNoteAsync(
                game,
                vaultPath,
                ObsidianNoteFileService.DefaultNotesDirectory);
            game.NotePath = created.RelativePath;

            var movedDirectory = Path.Combine(vaultPath, "GameCompare", "Games", "整理后");
            Directory.CreateDirectory(movedDirectory);
            var movedPath = Path.Combine(movedDirectory, Path.GetFileName(created.FullPath));
            File.Move(created.FullPath, movedPath);

            var rediscovered = await service.EnsureNoteAsync(
                game,
                vaultPath,
                ObsidianNoteFileService.DefaultNotesDirectory);

            Assert.False(rediscovered.Created);
            Assert.Equal(Path.GetFullPath(movedPath), rediscovered.FullPath);
            Assert.Contains("整理后", rediscovered.RelativePath);
        }
        finally
        {
            Directory.Delete(vaultPath, true);
        }
    }

    [Theory]
    [InlineData("../Outside")]
    [InlineData("..\\Outside")]
    [InlineData(".")]
    public void ResolveNotesRoot_RejectsPathsOutsideOrAtVaultRoot(string notesDirectory)
    {
        var vaultPath = CreateVault();
        try
        {
            var service = new ObsidianNoteFileService();
            Assert.Throws<ArgumentException>(() =>
                service.ResolveNotesRoot(vaultPath, notesDirectory));
        }
        finally
        {
            Directory.Delete(vaultPath, true);
        }
    }

    private static string CreateVault()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "GameCompare.Tests",
            "Obsidian",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static GameRecord Game(string name) => new()
    {
        Id = Guid.NewGuid(),
        PlayniteId = Guid.NewGuid(),
        Name = name,
        ReleaseDate = new DateOnly(2017, 3, 17),
        Developers = "PlatinumGames",
        Publishers = "Square Enix",
        Platforms = "PC",
        Genres = "Action RPG",
        Source = "Steam"
    };
}
