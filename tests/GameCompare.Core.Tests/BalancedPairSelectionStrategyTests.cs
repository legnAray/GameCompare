using GameCompare.Core.Models;
using GameCompare.Core.Services;

namespace GameCompare.Core.Tests;

public sealed class BalancedPairSelectionStrategyTests
{
    [Fact]
    public void SelectNext_WithFewerThanTwoGames_ReturnsNull()
    {
        var strategy = StrategyFor(0.1, 0);
        Assert.Null(strategy.SelectNext(Array.Empty<GameListItem>(), Array.Empty<ComparisonEvent>()));
        Assert.Null(strategy.SelectNext(new[] { Item("Only", 0) }, Array.Empty<ComparisonEvent>()));
    }

    [Fact]
    public void SelectNext_InCoverageMode_PrioritizesGamesWithLeastExposure()
    {
        var games = new[]
        {
            Item("New A", 0),
            Item("New B", 0),
            Item("Experienced C", 12),
            Item("Experienced D", 12)
        };

        var strategy = StrategyFor(0.70, 0);
        var pair = strategy.SelectNext(games, Array.Empty<ComparisonEvent>());

        Assert.NotNull(pair);
        Assert.Equal(new[] { "New A", "New B" }, Names(pair!));
    }

    [Fact]
    public void SelectNext_InRefinementMode_PrioritizesCloseRatings()
    {
        var games = new[]
        {
            Item("Low", 12, 1200),
            Item("Close A", 12, 1495),
            Item("Close B", 12, 1505),
            Item("High", 12, 1800)
        };

        var strategy = StrategyFor(0.10, 0);
        var pair = strategy.SelectNext(games, Array.Empty<ComparisonEvent>());

        Assert.NotNull(pair);
        Assert.Equal(new[] { "Close A", "Close B" }, Names(pair!));
    }

    [Fact]
    public void SelectNext_InCalibrationMode_PrioritizesCrossRangePair()
    {
        var games = new[]
        {
            Item("Low", 12, 1200),
            Item("Middle", 12, 1500),
            Item("High", 12, 1800)
        };

        var strategy = StrategyFor(0.90, 0);
        var pair = strategy.SelectNext(games, Array.Empty<ComparisonEvent>());

        Assert.NotNull(pair);
        Assert.Equal(new[] { "High", "Low" }, Names(pair!));
    }

    [Fact]
    public void SelectNext_AvoidsRecentlyComparedPairWhenAlternativesExist()
    {
        var first = Item("First", 3);
        var second = Item("Second", 3);
        var third = Item("Third", 3);
        var recent = Compared(first, second, 1);

        var strategy = StrategyFor(0.10, 0);
        var pair = strategy.SelectNext(new[] { first, second, third }, new[] { recent });

        Assert.NotNull(pair);
        Assert.NotEqual(new[] { "First", "Second" }, Names(pair!));
    }

    [Fact]
    public void SelectNext_RestsGamesShownInLastTwoRoundsWhenPossible()
    {
        var games = new[]
        {
            Item("Recent A", 3),
            Item("Recent B", 3),
            Item("Recent C", 3),
            Item("Recent D", 3),
            Item("Rested E", 3),
            Item("Rested F", 3)
        };
        var recent = new[]
        {
            Compared(games[0], games[1], 2),
            Compared(games[2], games[3], 1)
        };

        var strategy = StrategyFor(0.10, 0);
        var pair = strategy.SelectNext(games, recent);

        Assert.NotNull(pair);
        Assert.Equal(new[] { "Rested E", "Rested F" }, Names(pair!));
    }

    [Fact]
    public void SelectNext_WithFullLibrary_EvaluatesAllCandidatePairs()
    {
        var games = Enumerable.Range(0, 118)
            .Select(index => Item(
                $"Game {index:000}",
                index % 9,
                1250d + index * 5d))
            .ToArray();

        var strategy = StrategyFor(0.10, 0.25);
        var pair = strategy.SelectNext(games, Array.Empty<ComparisonEvent>());

        Assert.NotNull(pair);
        Assert.NotEqual(pair!.Left.Game.Id, pair.Right.Game.Id);
        Assert.Contains(pair.Left, games);
        Assert.Contains(pair.Right, games);
    }

    private static BalancedPairSelectionStrategy StrategyFor(params double[] randomValues) =>
        new(new FixedRandom(randomValues));

    private static string[] Names(GamePair pair) => new[]
    {
        pair.Left.Game.Name,
        pair.Right.Game.Name
    }.OrderBy(name => name, StringComparer.Ordinal).ToArray();

    private static ComparisonEvent Compared(
        GameListItem left,
        GameListItem right,
        int minutesAgo) => new()
        {
            LeftGameId = left.Game.Id,
            RightGameId = right.Game.Id,
            Outcome = ComparisonOutcome.Left,
            ComparedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo)
        };

    private static GameListItem Item(string name, int comparisonCount, double rating = 1500)
    {
        var game = new GameRecord
        {
            Id = Guid.NewGuid(),
            PlayniteId = Guid.NewGuid(),
            Name = name,
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        return new GameListItem(
            game,
            null,
            rating,
            comparisonCount,
            1d / Math.Sqrt(comparisonCount + 1d));
    }

    private sealed class FixedRandom(IEnumerable<double> values) : Random
    {
        private readonly Queue<double> values = new(values);

        public override double NextDouble() => values.Count > 0 ? values.Dequeue() : 0d;

        public override int Next(int maxValue) => 0;
    }
}
