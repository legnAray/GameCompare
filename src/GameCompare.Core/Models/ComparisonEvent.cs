using GameCompare.Core;

namespace GameCompare.Core.Models;

public sealed class ComparisonEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LeftGameId { get; set; }

    public Guid RightGameId { get; set; }

    public ComparisonOutcome Outcome { get; set; }

    public DateTimeOffset ComparedAt { get; set; } = DateTimeOffset.UtcNow;

    public string AlgorithmVersion { get; set; } = EloRankingAlgorithm.Version;

    public Guid? SessionId { get; set; }
}
