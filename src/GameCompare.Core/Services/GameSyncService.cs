using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;

namespace GameCompare.Core.Services;

public sealed class GameSyncService(IPlayniteImporter importer, IGameRepository repository)
{
    public async Task<SyncResult> SynchronizeAsync(
        string playnitePath,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var batch = await importer.ImportCompletedGamesAsync(playnitePath, cancellationToken);
            return await repository.ApplySyncAsync(batch, startedAt, cancellationToken);
        }
        catch (Exception exception)
        {
            await repository.RecordFailedSyncAsync(playnitePath, startedAt, exception, cancellationToken);
            throw;
        }
    }
}
