using LigaVolley.Domain.Common;
using LigaVolley.Domain.Seasons;

namespace LigaVolley.Domain.Tests;

public sealed class SeasonTests
{
    [Fact]
    public void Create_WithValidValues_SetsState()
    {
        var season = new Season(2026, "  Season 2026  ", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal((short)2026, season.Year);
        Assert.Equal("Season 2026", season.Name);
        Assert.True(season.Active);
    }

    [Fact]
    public void Create_WithEndBeforeStart_Throws()
        => Assert.Throws<DomainValidationException>(() =>
            new Season(2026, "Season", new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Throws(string name)
        => Assert.Throws<DomainValidationException>(() => new Season(2026, name, null, null));

    [Fact]
    public void SetActive_ChangesState()
    {
        var season = new Season(2026, "Season", null, null);
        season.SetActive(false);
        Assert.False(season.Active);
    }
}
