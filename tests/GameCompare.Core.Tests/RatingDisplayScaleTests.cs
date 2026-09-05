using GameCompare.Core.Services;

namespace GameCompare.Core.Tests;

public sealed class RatingDisplayScaleTests
{
    [Theory]
    [InlineData(1400, 71)]
    [InlineData(1500, 80)]
    [InlineData(1600, 89)]
    public void ToDisplayScore_MapsTypicalEloRatingsToFamiliarScores(
        double eloRating,
        int expectedScore)
    {
        Assert.Equal(expectedScore, RatingDisplayScale.ToDisplayScore(eloRating));
    }

    [Fact]
    public void ToDisplayScore_AlwaysStaysWithinSixtyAndOneHundred()
    {
        Assert.Equal(60, RatingDisplayScale.ToDisplayScore(double.NegativeInfinity));
        Assert.Equal(100, RatingDisplayScale.ToDisplayScore(double.PositiveInfinity));
    }

    [Fact]
    public void ToDisplayScore_PreservesRatingOrder()
    {
        var scores = new[] { 1300d, 1400d, 1500d, 1600d, 1700d }
            .Select(RatingDisplayScale.ToDisplayScore)
            .ToArray();

        Assert.True(scores.SequenceEqual(scores.OrderBy(score => score)));
        Assert.Equal(scores.Length, scores.Distinct().Count());
    }

    [Fact]
    public void ToDisplayScore_RejectsNaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RatingDisplayScale.ToDisplayScore(double.NaN));
    }
}
