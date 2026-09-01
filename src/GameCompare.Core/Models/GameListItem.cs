namespace GameCompare.Core.Models;

public sealed record GameListItem(
    GameRecord Game,
    int? Rank,
    double? Rating,
    int ComparisonCount,
    double? Uncertainty);

public sealed record GamePair(GameListItem Left, GameListItem Right);
