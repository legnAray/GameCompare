using GameCompare.Core.Abstractions;
using GameCompare.Core.Models;

namespace GameCompare.Core.Services;

public sealed class BalancedPairSelectionStrategy : IPairSelectionStrategy
{
    private const int RecentPairWindow = 50;
    private const int RecentGameWindow = 2;
    private const double RefinementProbability = 0.60;
    private const double CoverageProbability = 0.25;
    private const double RatingScale = 400d;

    private readonly Random random;

    public BalancedPairSelectionStrategy()
        : this(Random.Shared)
    {
    }

    public BalancedPairSelectionStrategy(Random random)
    {
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public GamePair? SelectNext(
        IReadOnlyList<GameListItem> games,
        IReadOnlyList<ComparisonEvent> recentEvents)
    {
        if (games.Count < 2)
        {
            return null;
        }

        var orderedRecentEvents = recentEvents
            .OrderByDescending(item => item.ComparedAt)
            .ThenByDescending(item => item.Id)
            .ToArray();
        var recentPairs = orderedRecentEvents
            .Take(RecentPairWindow)
            .Select(item => PairKey.Create(item.LeftGameId, item.RightGameId))
            .ToHashSet();
        var recentlyShownGameIds = orderedRecentEvents
            .Take(RecentGameWindow)
            .SelectMany(item => new[] { item.LeftGameId, item.RightGameId })
            .ToHashSet();

        var candidates = CreateCandidates(games);
        var nonRecentCandidates = candidates
            .Where(candidate => !recentPairs.Contains(candidate.Key))
            .ToList();
        if (nonRecentCandidates.Count > 0)
        {
            candidates = nonRecentCandidates;
        }

        var restedCandidates = candidates
            .Where(candidate =>
                !recentlyShownGameIds.Contains(candidate.First.Game.Id) &&
                !recentlyShownGameIds.Contains(candidate.Second.Game.Id))
            .ToList();
        if (restedCandidates.Count > 0)
        {
            candidates = restedCandidates;
        }

        var mode = SelectMode();
        var scoredCandidates = candidates
            .Select(candidate => new ScoredCandidate(candidate, Score(candidate, mode)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.First.Game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Candidate.Second.Game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var shortlistCount = Math.Clamp(
            (int)Math.Ceiling(scoredCandidates.Length * 0.05d),
            3,
            24);
        var selected = ChooseWeighted(scoredCandidates.Take(shortlistCount).ToArray());

        return random.Next(2) == 0
            ? new GamePair(selected.First, selected.Second)
            : new GamePair(selected.Second, selected.First);
    }

    private static List<PairCandidate> CreateCandidates(IReadOnlyList<GameListItem> games)
    {
        var candidates = new List<PairCandidate>(games.Count * (games.Count - 1) / 2);
        for (var firstIndex = 0; firstIndex < games.Count - 1; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < games.Count; secondIndex++)
            {
                candidates.Add(PairCandidate.Create(games[firstIndex], games[secondIndex]));
            }
        }

        return candidates;
    }

    private SelectionMode SelectMode()
    {
        var roll = random.NextDouble();
        if (roll < RefinementProbability)
        {
            return SelectionMode.Refinement;
        }

        return roll < RefinementProbability + CoverageProbability
            ? SelectionMode.Coverage
            : SelectionMode.Calibration;
    }

    private static double Score(PairCandidate candidate, SelectionMode mode) => mode switch
    {
        SelectionMode.Refinement =>
            0.65d * candidate.Informativeness +
            0.25d * candidate.Uncertainty +
            0.10d * candidate.Coverage,
        SelectionMode.Coverage =>
            0.65d * candidate.Coverage +
            0.20d * candidate.Uncertainty +
            0.15d * candidate.Informativeness,
        SelectionMode.Calibration =>
            0.55d * candidate.Distance +
            0.25d * candidate.Uncertainty +
            0.20d * candidate.Coverage,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private PairCandidate ChooseWeighted(IReadOnlyList<ScoredCandidate> candidates)
    {
        var weights = candidates
            .Select(candidate => Math.Pow(Math.Max(candidate.Score, 0.001d), 3d))
            .ToArray();
        var target = random.NextDouble() * weights.Sum();
        for (var index = 0; index < candidates.Count; index++)
        {
            target -= weights[index];
            if (target <= 0)
            {
                return candidates[index].Candidate;
            }
        }

        return candidates[^1].Candidate;
    }

    private enum SelectionMode
    {
        Refinement,
        Coverage,
        Calibration
    }

    private sealed record ScoredCandidate(PairCandidate Candidate, double Score);

    private sealed record PairCandidate(
        GameListItem First,
        GameListItem Second,
        PairKey Key,
        double Informativeness,
        double Uncertainty,
        double Coverage,
        double Distance)
    {
        public static PairCandidate Create(GameListItem first, GameListItem second)
        {
            var firstRating = first.Rating ?? 1500d;
            var secondRating = second.Rating ?? 1500d;
            var expected = 1d / (1d + Math.Pow(10d, (secondRating - firstRating) / RatingScale));
            var informativeness = 4d * expected * (1d - expected);
            var uncertainty = (ResolveUncertainty(first) + ResolveUncertainty(second)) / 2d;
            var coverage = 1d / Math.Sqrt(
                (first.ComparisonCount + 1d) * (second.ComparisonCount + 1d));
            var distance = Math.Min(1d, Math.Abs(firstRating - secondRating) / RatingScale);

            return new PairCandidate(
                first,
                second,
                PairKey.Create(first.Game.Id, second.Game.Id),
                informativeness,
                uncertainty,
                coverage,
                distance);
        }

        private static double ResolveUncertainty(GameListItem game) => Math.Clamp(
            game.Uncertainty ?? 1d / Math.Sqrt(game.ComparisonCount + 1d),
            0d,
            1d);
    }

    private readonly record struct PairKey(Guid First, Guid Second)
    {
        public static PairKey Create(Guid left, Guid right) =>
            left.CompareTo(right) <= 0 ? new PairKey(left, right) : new PairKey(right, left);
    }
}
