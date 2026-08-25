using LigaVolley.Application.Common;
using LigaVolley.Application.PlayoffProgression;

namespace LigaVolley.Application.Tests;

public sealed class PlayoffSeriesResultCalculatorTests
{
    [Theory]
    [InlineData(new[] { 10 }, 2, 0, 1)]
    [InlineData(new[] { 20 }, 1, 1, null)]
    [InlineData(new[] { 20, 10 }, 2, 1, 1)]
    [InlineData(new[] { 20, 20 }, 1, 2, 2)]
    public void AddsInitialAndRealWinsAndDeterminesWinner(int[] winners, int expectedTeam1, int expectedTeam2, int? expectedWinner)
    {
        var result = PlayoffSeriesResultCalculator.Calculate(2, 1, 0, 10, 20, winners);

        Assert.Equal(expectedTeam1, result.Team1Wins);
        Assert.Equal(expectedTeam2, result.Team2Wins);
        Assert.Equal(expectedWinner, result.WinnerSide.HasValue ? result.WinnerSide.Value : null);
    }

    [Fact]
    public void WinsRequiredOneFinishesWithFirstWinner()
    {
        var result = PlayoffSeriesResultCalculator.Calculate(1, 0, 0, 10, 20, [20]);
        Assert.Equal((byte)2, result.WinnerSide);
    }

    [Fact]
    public void RejectsWinnerOutsideSeries()
    {
        var error = Assert.Throws<ResourceConflictException>(() =>
            PlayoffSeriesResultCalculator.Calculate(2, 0, 0, 10, 20, [30]));
        Assert.Equal("playoff_series_match_invalid", error.Code);
    }
}
