using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;

namespace GameCompare.Core;

public sealed class EloRankingAlgorithm : IRankingAlgorithm
{
    public const string Version = "elo-v1-k32";
    private const double InitialRating = 1500;
    private const double KFactor = 32;

    public string AlgorithmVersion => Version;

    public IReadOnlyList<RatingState> Calculate(
        IReadOnlyCollection<GameRecord> games,
        IReadOnlyCollection<ComparisonEvent> events,
        DateTimeOffset calculatedAt)
    {
        var gameById = games.ToDictionary(game => game.Id);
        var ratings = games.ToDictionary(game => game.Id, _ => InitialRating);
        var counts = games.ToDictionary(game => game.Id, _ => 0);

        foreach (var comparison in events
                     .Where(item => item.Outcome is ComparisonOutcome.Left or ComparisonOutcome.Right)
                     .OrderBy(item => item.ComparedAt)
                     .ThenBy(item => item.Id))
        {
            if (comparison.LeftGameId == comparison.RightGameId ||
                !ratings.ContainsKey(comparison.LeftGameId) ||
                !ratings.ContainsKey(comparison.RightGameId))
            {
                continue;
            }

            var leftRating = ratings[comparison.LeftGameId];
            var rightRating = ratings[comparison.RightGameId];
            var leftExpected = ExpectedScore(leftRating, rightRating);
            var rightExpected = 1 - leftExpected;
            var leftActual = comparison.Outcome == ComparisonOutcome.Left ? 1d : 0d;
            var rightActual = 1 - leftActual;

            ratings[comparison.LeftGameId] = leftRating + KFactor * (leftActual - leftExpected);
            ratings[comparison.RightGameId] = rightRating + KFactor * (rightActual - rightExpected);
            counts[comparison.LeftGameId]++;
            counts[comparison.RightGameId]++;
        }

        var orderedIds = ratings.Keys
            .Where(id => counts[id] > 0)
            .OrderByDescending(id => ratings[id])
            .ThenBy(id => gameById[id].Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var result = new List<RatingState>(orderedIds.Count);
        for (var index = 0; index < orderedIds.Count; index++)
        {
            var gameId = orderedIds[index];
            var comparisonCount = counts[gameId];
            result.Add(new RatingState
            {
                GameId = gameId,
                Rating = ratings[gameId],
                Uncertainty = 1d / Math.Sqrt(comparisonCount + 1d),
                ComparisonCount = comparisonCount,
                Rank = index + 1,
                AlgorithmVersion = Version,
                UpdatedAt = calculatedAt
            });
        }

        return result;
    }

    private static double ExpectedScore(double rating, double opponentRating) =>
        1d / (1d + Math.Pow(10d, (opponentRating - rating) / 400d));
}
