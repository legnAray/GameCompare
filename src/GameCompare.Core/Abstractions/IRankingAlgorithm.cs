using GameCompare.Core.Models;

namespace GameCompare.Core.Abstractions;

public interface IRankingAlgorithm
{
    string AlgorithmVersion { get; }

    IReadOnlyList<RatingState> Calculate(
        IReadOnlyCollection<GameRecord> games,
        IReadOnlyCollection<ComparisonEvent> events,
        DateTimeOffset calculatedAt);
}
