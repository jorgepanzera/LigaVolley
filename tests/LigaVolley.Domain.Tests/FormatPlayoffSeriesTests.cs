using LigaVolley.Domain.Common;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Domain.Tests;

public sealed class FormatPlayoffSeriesTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, -1, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(2, 0, 2)]
    public void RejectsInvalidWinsConfiguration(short winsRequired, short team1InitialWins, short team2InitialWins)
    {
        Assert.Throws<DomainValidationException>(() =>
            new FormatPlayoffSeries("S", "Series", 1, winsRequired, team1InitialWins, team2InitialWins));
    }
}
