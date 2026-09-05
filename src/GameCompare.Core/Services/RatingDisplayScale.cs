namespace GameCompare.Core.Services;

public static class RatingDisplayScale
{
    public const int MinimumScore = 60;
    public const int MidpointScore = 80;
    public const int MaximumScore = 100;

    private const double EloMidpoint = 1500d;
    private const double EloSpread = 100d;

    public static int ToDisplayScore(double eloRating)
    {
        if (double.IsNaN(eloRating))
        {
            throw new ArgumentOutOfRangeException(nameof(eloRating));
        }

        var normalized = 1d / (1d + Math.Exp(-(eloRating - EloMidpoint) / EloSpread));
        var score = MinimumScore + (MaximumScore - MinimumScore) * normalized;

        return (int)Math.Round(score, MidpointRounding.AwayFromZero);
    }
}
