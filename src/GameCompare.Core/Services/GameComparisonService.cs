using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;

namespace GameCompare.Core.Services;

public sealed class GameComparisonService(
    IGameRepository repository,
    IRankingAlgorithm rankingAlgorithm,
    IPairSelectionStrategy pairSelectionStrategy)
{
    public async Task<GamePair?> GetNextPairAsync(CancellationToken cancellationToken = default)
    {
        var games = await repository.GetActiveGamesAsync(cancellationToken);
        var events = await repository.GetComparisonEventsAsync(cancellationToken);
        return pairSelectionStrategy.SelectNext(games, events);
    }

    public async Task RecordAsync(
        GamePair pair,
        ComparisonOutcome outcome,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var comparisonEvent = new ComparisonEvent
        {
            LeftGameId = pair.Left.Game.Id,
            RightGameId = pair.Right.Game.Id,
            Outcome = outcome,
            ComparedAt = DateTimeOffset.UtcNow,
            AlgorithmVersion = rankingAlgorithm.AlgorithmVersion,
            SessionId = sessionId
        };

        await repository.AddComparisonEventAsync(comparisonEvent, cancellationToken);
        if (outcome != ComparisonOutcome.Skip)
        {
            await RebuildRatingsAsync(cancellationToken);
        }
    }

    public async Task RebuildRatingsAsync(CancellationToken cancellationToken = default)
    {
        var games = await repository.GetActiveGamesAsync(cancellationToken);
        var events = await repository.GetComparisonEventsAsync(cancellationToken);
        var states = rankingAlgorithm.Calculate(
            games.Select(item => item.Game).ToArray(),
            events.ToArray(),
            DateTimeOffset.UtcNow);
        await repository.ReplaceRatingStatesAsync(states, cancellationToken);
    }
}
