using GameCompare.Core.Models;

namespace GameCompare.Core.Abstractions;

public interface IPlayniteImporter
{
    Task<PlayniteImportBatch> ImportCompletedGamesAsync(
        string playnitePath,
        CancellationToken cancellationToken = default);
}
