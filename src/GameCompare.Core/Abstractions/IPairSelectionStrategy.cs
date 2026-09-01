using GameCompare.Core.Models;

namespace GameCompare.Core.Abstractions;

public interface IPairSelectionStrategy
{
    GamePair? SelectNext(
        IReadOnlyList<GameListItem> games,
        IReadOnlyList<ComparisonEvent> recentEvents);
}
