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
        Assert.Equal(EloRankingAlgorithm.Version, winnerState.AlgorithmVersion);
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

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_UnexpectedResultHasLargerImpactThanExpectedResult()
    {
        var strong = Game("Strong");
        var weak = Game("Weak");
        var establishedHistory = Enumerable.Range(0, 6)
            .Select(index => Compared(strong, weak, ComparisonOutcome.Left, index))
            .ToArray();
        var before = algorithm.Calculate(
            new[] { strong, weak },
            establishedHistory,
            DateTimeOffset.UtcNow);
        var strongBefore = before.Single(state => state.GameId == strong.Id).Rating;
        var weakBefore = before.Single(state => state.GameId == weak.Id).Rating;

        var expectedResult = algorithm.Calculate(
            new[] { strong, weak },
            establishedHistory.Append(Compared(strong, weak, ComparisonOutcome.Left, 6)).ToArray(),
            DateTimeOffset.UtcNow);
        var upsetResult = algorithm.Calculate(
            new[] { strong, weak },
            establishedHistory.Append(Compared(strong, weak, ComparisonOutcome.Right, 6)).ToArray(),
            DateTimeOffset.UtcNow);
        var expectedWinnerGain = expectedResult.Single(state => state.GameId == strong.Id).Rating - strongBefore;
        var upsetWinnerGain = upsetResult.Single(state => state.GameId == weak.Id).Rating - weakBefore;

        Assert.True(upsetWinnerGain > expectedWinnerGain);
    }

    [Fact]
    public void Calculate_AdaptiveKReducesMovementAfterManyComparisons()
    {
        var first = Game("First");
        var second = Game("Second");
        var firstChoice = Compared(first, second, ComparisonOutcome.Left, 0);
        var initial = algorithm.Calculate(
            new[] { first, second },
            new[] { firstChoice },
            DateTimeOffset.UtcNow);
        var initialMovement = initial.Single(state => state.GameId == first.Id).Rating - 1500d;

        var matureHistory = Enumerable.Range(0, 20)
            .Select(index => Compared(
                first,
                second,
                index % 2 == 0 ? ComparisonOutcome.Left : ComparisonOutcome.Right,
                index))
            .ToArray();
        var matureBefore = algorithm.Calculate(
            new[] { first, second },
            matureHistory,
            DateTimeOffset.UtcNow);
        var matureAfter = algorithm.Calculate(
            new[] { first, second },
            matureHistory.Append(Compared(first, second, ComparisonOutcome.Left, 20)).ToArray(),
            DateTimeOffset.UtcNow);
        var matureMovement = Math.Abs(
            matureAfter.Single(state => state.GameId == first.Id).Rating -
            matureBefore.Single(state => state.GameId == first.Id).Rating);

        Assert.True(matureMovement < initialMovement);
    }

    [Fact]
    public void Calculate_NewGameMovesFasterThanExperiencedOpponent()
    {
        var experienced = Game("Experienced");
        var anchor = Game("Anchor");
        var newGame = Game("New");
        var establishedHistory = Enumerable.Range(0, 12)
            .Select(index => Compared(
                experienced,
                anchor,
                index % 2 == 0 ? ComparisonOutcome.Left : ComparisonOutcome.Right,
                index))
            .ToArray();
        var before = algorithm.Calculate(
            new[] { experienced, anchor, newGame },
            establishedHistory,
            DateTimeOffset.UtcNow);
        var experiencedBefore = before.Single(state => state.GameId == experienced.Id).Rating;

        var after = algorithm.Calculate(
            new[] { experienced, anchor, newGame },
            establishedHistory.Append(Compared(newGame, experienced, ComparisonOutcome.Left, 12)).ToArray(),
            DateTimeOffset.UtcNow);
        var newGameMovement = Math.Abs(
            after.Single(state => state.GameId == newGame.Id).Rating - 1500d);
        var experiencedMovement = Math.Abs(
            after.Single(state => state.GameId == experienced.Id).Rating - experiencedBefore);

        Assert.True(newGameMovement > experiencedMovement);
    }

    private static ComparisonEvent Compared(
        GameRecord left,
        GameRecord right,
        ComparisonOutcome outcome,
        int order) => new()
        {
            LeftGameId = left.Id,
            RightGameId = right.Id,
            Outcome = outcome,
            ComparedAt = DateTimeOffset.UnixEpoch.AddMinutes(order)
        };

    private static GameRecord Game(string name) => new()
    {
        Id = Guid.NewGuid(),
        PlayniteId = Guid.NewGuid(),
        Name = name,
        LastSyncedAt = DateTimeOffset.UtcNow
    };
}
