using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GameCompare.Persistence;

public sealed class GameRepository(IDbContextFactory<GameCompareDbContext> contextFactory)
    : IGameRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppDataPaths.RootDirectory);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<SyncResult> ApplySyncAsync(
        PlayniteImportBatch batch,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var existingGames = await database.Games
            .ToDictionaryAsync(game => game.PlayniteId, cancellationToken);
        var importedIds = batch.Games.Select(game => game.PlayniteId).ToHashSet();
        var updatedCount = 0;

        foreach (var imported in batch.Games)
        {
            if (!existingGames.TryGetValue(imported.PlayniteId, out var game))
            {
                game = new GameRecord
                {
                    PlayniteId = imported.PlayniteId
                };
                database.Games.Add(game);
            }
            else
            {
                updatedCount++;
            }

            CopyImportedValues(imported, game, batch.ImportedAt);
        }

        var deactivatedCount = 0;
        foreach (var game in existingGames.Values.Where(game => game.IsActiveCompleted))
        {
            if (importedIds.Contains(game.PlayniteId))
            {
                continue;
            }

            game.IsActiveCompleted = false;
            game.LastSyncedAt = batch.ImportedAt;
            deactivatedCount++;
        }

        var finishedAt = DateTimeOffset.UtcNow;
        database.SyncRuns.Add(new SyncRun
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            ImportedCount = batch.Games.Count,
            UpdatedCount = updatedCount,
            DeactivatedCount = deactivatedCount,
            FailedCount = 0,
            SourcePath = batch.SourcePath
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SyncResult(
            batch.Games.Count,
            updatedCount,
            deactivatedCount,
            0,
            finishedAt);
    }

    public async Task RecordFailedSyncAsync(
        string sourcePath,
        DateTimeOffset startedAt,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        database.SyncRuns.Add(new SyncRun
        {
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            FailedCount = 1,
            ErrorMessage = exception.Message,
            SourcePath = sourcePath
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameListItem>> GetActiveGamesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var games = await database.Games
            .AsNoTracking()
            .Where(game => game.IsActiveCompleted)
            .ToListAsync(cancellationToken);
        var ratings = await database.RatingStates
            .AsNoTracking()
            .ToDictionaryAsync(item => item.GameId, cancellationToken);

        return games
            .Select(game =>
            {
                ratings.TryGetValue(game.Id, out var rating);
                return new GameListItem(
                    game,
                    rating?.Rank,
                    rating?.Rating,
                    rating?.ComparisonCount ?? 0,
                    rating?.Uncertainty);
            })
            .OrderBy(item => item.Rank ?? int.MaxValue)
            .ThenBy(item => item.Game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<ComparisonEvent>> GetComparisonEventsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var events = await database.ComparisonEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return events
            .OrderBy(item => item.ComparedAt)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    public async Task AddComparisonEventAsync(
        ComparisonEvent comparisonEvent,
        CancellationToken cancellationToken = default)
    {
        if (comparisonEvent.LeftGameId == comparisonEvent.RightGameId)
        {
            throw new ArgumentException("A game cannot be compared with itself.", nameof(comparisonEvent));
        }

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        database.ComparisonEvents.Add(comparisonEvent);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceRatingStatesAsync(
        IReadOnlyCollection<RatingState> ratingStates,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.RatingStates.ExecuteDeleteAsync(cancellationToken);
        database.RatingStates.AddRange(ratingStates);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetGameNotePathAsync(
        Guid gameId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Note path cannot be empty.", nameof(relativePath));
        }

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var game = await database.Games.SingleOrDefaultAsync(
            item => item.Id == gameId,
            cancellationToken) ?? throw new InvalidOperationException("The selected game no longer exists.");
        game.NotePath = relativePath.Trim();
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<SyncRun?> GetLatestSyncRunAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var runs = await database.SyncRuns
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return runs.OrderByDescending(item => item.FinishedAt).FirstOrDefault();
    }

    private static void CopyImportedValues(
        ImportedGame imported,
        GameRecord destination,
        DateTimeOffset importedAt)
    {
        destination.Name = imported.Name;
        destination.SortingName = imported.SortingName;
        destination.ReleaseDate = imported.ReleaseDate;
        destination.Developers = imported.Developers;
        destination.Publishers = imported.Publishers;
        destination.Platforms = imported.Platforms;
        destination.Genres = imported.Genres;
        destination.Source = imported.Source;
        destination.CoverPath = imported.CoverPath;
        destination.PlaytimeSeconds = imported.PlaytimeSeconds;
        destination.LastActivityAt = imported.LastActivityAt;
        destination.PlayniteUserScore = imported.PlayniteUserScore;
        destination.IsActiveCompleted = true;
        destination.LastSyncedAt = importedAt;
    }
}
