using GameCompare.Core;

namespace GameCompare.Core.Models;

public sealed class RatingState
{
    public Guid GameId { get; set; }

    public double Rating { get; set; }

    public double Uncertainty { get; set; }

    public int ComparisonCount { get; set; }

    public int Rank { get; set; }

    public string AlgorithmVersion { get; set; } = EloRankingAlgorithm.Version;

    public DateTimeOffset UpdatedAt { get; set; }
}
