using GameCompare.Core.Models;
using GameCompare.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCompare.Core.Tests;

public sealed class GameRepositoryTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        "GameCompare.Tests",
        $"{Guid.NewGuid():N}.db");
    private GameRepository repository = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var options = new DbContextOptionsBuilder<GameCompareDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;
        repository = new GameRepository(new TestContextFactory(options));
        await repository.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ApplySync_UpsertsAndDeactivatesWithoutDeletingHistory()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstBatch = Batch(
            Imported(firstId, "First"),
            Imported(secondId, "Second"));

        var firstResult = await repository.ApplySyncAsync(firstBatch, DateTimeOffset.UtcNow);
        var initialGames = await repository.GetActiveGamesAsync();
        var firstGame = initialGames.Single(item => item.Game.PlayniteId == firstId);
        await repository.AddComparisonEventAsync(new ComparisonEvent
        {
            LeftGameId = firstGame.Game.Id,
            RightGameId = initialGames.Single(item => item.Game.PlayniteId == secondId).Game.Id,
            Outcome = ComparisonOutcome.Left
        });

        var secondResult = await repository.ApplySyncAsync(
            Batch(Imported(firstId, "First renamed")),
            DateTimeOffset.UtcNow);
        var activeGames = await repository.GetActiveGamesAsync();
        var events = await repository.GetComparisonEventsAsync();

        Assert.Equal(2, firstResult.ImportedCount);
        Assert.Equal(1, secondResult.UpdatedCount);
        Assert.Equal(1, secondResult.DeactivatedCount);
        Assert.Single(activeGames);
        Assert.Equal("First renamed", activeGames[0].Game.Name);
        Assert.Single(events);
    }

    [Fact]
    public async Task ReplaceRatingStates_UpdatesRankingProjection()
    {
        await repository.ApplySyncAsync(Batch(Imported(Guid.NewGuid(), "Rated")), DateTimeOffset.UtcNow);
        var game = (await repository.GetActiveGamesAsync()).Single().Game;

        await repository.ReplaceRatingStatesAsync(new[]
        {
            new RatingState
            {
                GameId = game.Id,
                Rank = 1,
                Rating = 1532,
                ComparisonCount = 2,
                Uncertainty = 0.5,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        });

        var item = (await repository.GetActiveGamesAsync()).Single();
        Assert.Equal(1, item.Rank);
        Assert.Equal(1532, item.Rating);
        Assert.Equal(2, item.ComparisonCount);
    }

    private static PlayniteImportBatch Batch(params ImportedGame[] games) => new(
        "test",
        "test/library",
        games,
        DateTimeOffset.UtcNow);

    private static ImportedGame Imported(Guid id, string name) => new(
        id,
        name,
        null,
        new DateOnly(2024, 1, 2),
        "Developer",
        "Publisher",
        "PC",
        "RPG",
        "Steam",
        null,
        3600,
        null,
        null);

    private sealed class TestContextFactory(DbContextOptions<GameCompareDbContext> options)
        : IDbContextFactory<GameCompareDbContext>
    {
        public GameCompareDbContext CreateDbContext() => new(options);
    }
}
