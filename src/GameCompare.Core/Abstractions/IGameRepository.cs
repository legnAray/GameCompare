using GameCompare.Core.Models;

namespace GameCompare.Core.Abstractions;

public interface IGameRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<SyncResult> ApplySyncAsync(
        PlayniteImportBatch batch,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default);

    Task RecordFailedSyncAsync(
        string sourcePath,
        DateTimeOffset startedAt,
        Exception exception,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameListItem>> GetActiveGamesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComparisonEvent>> GetComparisonEventsAsync(
        CancellationToken cancellationToken = default);

    Task AddComparisonEventAsync(
        ComparisonEvent comparisonEvent,
        CancellationToken cancellationToken = default);

    Task ReplaceRatingStatesAsync(
        IReadOnlyCollection<RatingState> ratingStates,
        CancellationToken cancellationToken = default);

    Task<SyncRun?> GetLatestSyncRunAsync(CancellationToken cancellationToken = default);
}
