using LiteDB;

namespace GameCompare.Playnite.Tests;

public sealed class PlayniteImporterTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "GameCompare.Playnite.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportCompletedGamesAsync_MapsCompletedGameAndSkipsOtherStatuses()
    {
        var completedId = Guid.NewGuid();
        var playingId = Guid.NewGuid();
        var developerId = Guid.NewGuid();
        var publisherId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var genreId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var completedGameId = Guid.NewGuid();
        var excludedGameId = Guid.NewGuid();
        var library = CreateLibrary();

        WriteStatuses(library, completedId, playingId);
        WriteLookup(library, "companies.db", "Company", (developerId, "Studio"), (publisherId, "Publisher"));
        WriteLookup(library, "platforms.db", "Platform", (platformId, "PC (Windows)"));
        WriteLookup(library, "genres.db", "Genre", (genreId, "Role-playing"));
        WriteLookup(library, "sources.db", "GameSource", (sourceId, "Steam"));

        var relativeCover = Path.Combine(completedGameId.ToString(), "cover.jpg");
        var coverPath = Path.Combine(library, "files", relativeCover);
        Directory.CreateDirectory(Path.GetDirectoryName(coverPath)!);
        File.WriteAllBytes(coverPath, new byte[] { 0xff, 0xd8, 0xff, 0xd9 });

        using (var database = new LiteDatabase(Path.Combine(library, "games.db")))
        {
            var games = database.GetCollection("Game");
            games.Insert(GameDocument(
                completedGameId,
                completedId,
                "A Completed Game",
                developerId,
                publisherId,
                platformId,
                genreId,
                sourceId,
                relativeCover));
            games.Insert(new BsonDocument
            {
                ["_id"] = excludedGameId,
                ["Name"] = "Still Playing",
                ["CompletionStatusId"] = playingId
            });
        }

        var batch = await new PlayniteImporter().ImportCompletedGamesAsync(root);

        var game = Assert.Single(batch.Games);
        Assert.Equal(completedGameId, game.PlayniteId);
        Assert.Equal("A Completed Game", game.Name);
        Assert.Equal(new DateOnly(2024, 7, 9), game.ReleaseDate);
        Assert.Equal("Studio", game.Developers);
        Assert.Equal("Publisher", game.Publishers);
        Assert.Equal("PC (Windows)", game.Platforms);
        Assert.Equal("Role-playing", game.Genres);
        Assert.Equal("Steam", game.Source);
        Assert.Equal(Path.GetFullPath(coverPath), game.CoverPath);
        Assert.Equal((ulong)7200, game.PlaytimeSeconds);
        Assert.Equal(91, game.PlayniteUserScore);
        Assert.Equal(2024, game.LastActivityAt?.Year);
    }

    [Fact]
    public async Task ImportCompletedGamesAsync_WhenCompletedStatusIsMissing_ThrowsHelpfulError()
    {
        var library = CreateLibrary();
        WriteStatuses(library, Guid.NewGuid(), Guid.NewGuid(), includeCompleted: false);
        WriteLookup(library, "companies.db", "Company", (Guid.NewGuid(), "Company"));
        WriteLookup(library, "platforms.db", "Platform", (Guid.NewGuid(), "Platform"));
        WriteLookup(library, "genres.db", "Genre", (Guid.NewGuid(), "Genre"));
        WriteLookup(library, "sources.db", "GameSource", (Guid.NewGuid(), "Source"));
        using (var database = new LiteDatabase(Path.Combine(library, "games.db")))
        {
            database.GetCollection("Game").Insert(new BsonDocument
            {
                ["_id"] = Guid.NewGuid(),
                ["Name"] = "Not completed",
                ["CompletionStatusId"] = Guid.NewGuid()
            });
        }

        var exception = await Assert.ThrowsAsync<PlayniteImportException>(() =>
            new PlayniteImporter().ImportCompletedGamesAsync(root));

        Assert.Contains("Completed", exception.Message);
    }

    [Fact]
    public async Task ImportCompletedGamesAsync_WithInvalidDirectory_ThrowsHelpfulError()
    {
        var exception = await Assert.ThrowsAsync<PlayniteImportException>(() =>
            new PlayniteImporter().ImportCompletedGamesAsync(Path.Combine(root, "missing")));

        Assert.Contains("Playnite", exception.Message);
    }

    private string CreateLibrary()
    {
        var library = Path.Combine(root, "library");
        Directory.CreateDirectory(library);
        return library;
    }

    private static void WriteStatuses(
        string library,
        Guid completedId,
        Guid playingId,
        bool includeCompleted = true)
    {
        using var database = new LiteDatabase(Path.Combine(library, "completionstatuses.db"));
        var statuses = database.GetCollection("CompletionStatus");
        statuses.Insert(new BsonDocument
        {
            ["_id"] = completedId,
            ["Name"] = includeCompleted ? "Completed" : "Finished"
        });
        statuses.Insert(new BsonDocument
        {
            ["_id"] = playingId,
            ["Name"] = "Playing"
        });
    }

    private static void WriteLookup(
        string library,
        string fileName,
        string collectionName,
        params (Guid Id, string Name)[] values)
    {
        using var database = new LiteDatabase(Path.Combine(library, fileName));
        var collection = database.GetCollection(collectionName);
        foreach (var (id, name) in values)
        {
            collection.Insert(new BsonDocument
            {
                ["_id"] = id,
                ["Name"] = name
            });
        }
    }

    private static BsonDocument GameDocument(
        Guid gameId,
        Guid statusId,
        string name,
        Guid developerId,
        Guid publisherId,
        Guid platformId,
        Guid genreId,
        Guid sourceId,
        string coverPath) => new()
    {
        ["_id"] = gameId,
        ["Name"] = name,
        ["SortingName"] = "Completed Game, A",
        ["CompletionStatusId"] = statusId,
        ["DeveloperIds"] = GuidArray(developerId),
        ["PublisherIds"] = GuidArray(publisherId),
        ["PlatformIds"] = GuidArray(platformId),
        ["GenreIds"] = GuidArray(genreId),
        ["SourceId"] = sourceId,
        ["ReleaseDate"] = "2024-7-9",
        ["CoverImage"] = coverPath,
        ["Playtime"] = 7200L,
        ["LastActivity"] = new DateTime(2024, 7, 10, 12, 0, 0, DateTimeKind.Utc),
        ["UserScore"] = 91
    };

    private static BsonArray GuidArray(params Guid[] values)
    {
        var result = new BsonArray();
        foreach (var value in values)
        {
            result.Add(value);
        }
        return result;
    }
}
