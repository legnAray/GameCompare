using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;

namespace GameCompare.Core.Services;

public sealed class BalancedPairSelectionStrategy : IPairSelectionStrategy
{
    private const int RecentPairWindow = 50;

    public GamePair? SelectNext(
        IReadOnlyList<GameListItem> games,
        IReadOnlyList<ComparisonEvent> recentEvents)
    {
        if (games.Count < 2)
        {
            return null;
        }

        var minimumCount = games.Min(game => game.ComparisonCount);
        var firstPool = games
            .Where(game => game.ComparisonCount <= minimumCount + 1)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        var first = firstPool[0];

        var recentPairs = recentEvents
            .OrderByDescending(item => item.ComparedAt)
            .Take(RecentPairWindow)
            .Select(item => PairKey.Create(item.LeftGameId, item.RightGameId))
            .ToHashSet();

        var candidates = games.Where(game => game.Game.Id != first.Game.Id).ToList();
        var nonRecentCandidates = candidates
            .Where(game => !recentPairs.Contains(PairKey.Create(first.Game.Id, game.Game.Id)))
            .ToList();

        if (nonRecentCandidates.Count > 0)
        {
            candidates = nonRecentCandidates;
        }

        var firstRating = first.Rating ?? 1500d;
        var second = candidates
            .OrderBy(game => game.ComparisonCount)
            .ThenBy(game => Math.Abs((game.Rating ?? 1500d) - firstRating))
            .ThenBy(_ => Random.Shared.Next())
            .First();

        return Random.Shared.Next(2) == 0
            ? new GamePair(first, second)
            : new GamePair(second, first);
    }

    private readonly record struct PairKey(Guid First, Guid Second)
    {
        public static PairKey Create(Guid left, Guid right) =>
            left.CompareTo(right) <= 0 ? new PairKey(left, right) : new PairKey(right, left);
    }
}
