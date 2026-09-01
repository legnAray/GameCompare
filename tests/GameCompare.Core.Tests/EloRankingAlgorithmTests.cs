using GameCompare.Core.Models;

namespace GameCompare.Core.Tests;

public sealed class EloRankingAlgorithmTests
{
    private readonly EloRankingAlgorithm algorithm = new();

    [Fact]
    public void Calculate_WithNoComparisons_LeavesGamesUnranked()
    {
        var games = new[]
        {
            Game("Zeta"),
            Game("Alpha")
        };

        var result = algorithm.Calculate(games, Array.Empty<ComparisonEvent>(), DateTimeOffset.UtcNow);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_WhenLeftWins_MovesWinnerAboveLoser()
    {
        var winner = Game("Winner");
        var loser = Game("Loser");
        var events = new[]
        {
            new ComparisonEvent
            {
                LeftGameId = winner.Id,
                RightGameId = loser.Id,
                Outcome = ComparisonOutcome.Left,
                ComparedAt = DateTimeOffset.UtcNow
            }
        };

        var result = algorithm.Calculate(new[] { winner, loser }, events, DateTimeOffset.UtcNow);
        var winnerState = result.Single(state => state.GameId == winner.Id);
        var loserState = result.Single(state => state.GameId == loser.Id);

        Assert.Equal(1, winnerState.Rank);
        Assert.True(winnerState.Rating > 1500);
        Assert.True(loserState.Rating < 1500);
        Assert.Equal(1, winnerState.ComparisonCount);
        Assert.Equal(1, loserState.ComparisonCount);
    }

    [Fact]
    public void Calculate_SkipsAndInvalidEvents_DoNotChangeRatings()
    {
        var first = Game("First");
        var second = Game("Second");
        var events = new[]
        {
            new ComparisonEvent
            {
                LeftGameId = first.Id,
                RightGameId = second.Id,
                Outcome = ComparisonOutcome.Skip
            },
            new ComparisonEvent
            {
                LeftGameId = first.Id,
                RightGameId = Guid.NewGuid(),
                Outcome = ComparisonOutcome.Left
            }
        };

        var result = algorithm.Calculate(new[] { first, second }, events, DateTimeOffset.UtcNow);

        Assert.All(result, state =>
        {
            Assert.Equal(1500, state.Rating);
            Assert.Equal(0, state.ComparisonCount);
        });
    }

    private static GameRecord Game(string name) => new()
    {
        Id = Guid.NewGuid(),
        PlayniteId = Guid.NewGuid(),
        Name = name,
        LastSyncedAt = DateTimeOffset.UtcNow
    };
}
