using GameCompare.Core.Models;
using GameCompare.Core.Services;

namespace GameCompare.Core.Tests;

public sealed class BalancedPairSelectionStrategyTests
{
    private readonly BalancedPairSelectionStrategy strategy = new();

    [Fact]
    public void SelectNext_WithFewerThanTwoGames_ReturnsNull()
    {
        Assert.Null(strategy.SelectNext(Array.Empty<GameListItem>(), Array.Empty<ComparisonEvent>()));
        Assert.Null(strategy.SelectNext(new[] { Item("Only", 0) }, Array.Empty<ComparisonEvent>()));
    }

    [Fact]
    public void SelectNext_AlwaysReturnsTwoDifferentGames()
    {
        var games = new[]
        {
            Item("Least compared", 0),
            Item("Experienced", 5),
            Item("Also experienced", 5)
        };

        var pair = strategy.SelectNext(games, Array.Empty<ComparisonEvent>());

        Assert.NotNull(pair);
        Assert.NotEqual(pair.Left.Game.Id, pair.Right.Game.Id);
        Assert.Contains("Least compared", new[] { pair.Left.Game.Name, pair.Right.Game.Name });
    }

    private static GameListItem Item(string name, int comparisonCount)
    {
        var game = new GameRecord
        {
            Id = Guid.NewGuid(),
            PlayniteId = Guid.NewGuid(),
            Name = name,
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        return new GameListItem(game, null, 1500, comparisonCount, null);
    }
}
