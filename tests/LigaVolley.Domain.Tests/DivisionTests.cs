using LigaVolley.Domain.Common;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Domain.Tests;

public sealed class DivisionTests
{
    [Fact]
    public void Create_WithValidValues_SetsState()
    {
        var division = new Division("  B Femenina  ", 2, Gender.Female);

        Assert.Equal("B Femenina", division.Name);
        Assert.Equal((short)2, division.LevelOrder);
        Assert.Equal(Gender.Female, division.Gender);
        Assert.True(division.Active);
    }

    [Fact]
    public void Create_WithNonPositiveLevel_Throws()
        => Assert.Throws<DomainValidationException>(() => new Division("B", 0, Gender.Female));

    [Fact]
    public void Create_WithInvalidGender_Throws()
        => Assert.Throws<DomainValidationException>(() => new Division("B", 1, (Gender)99));

    [Fact]
    public void Update_ChangesValues()
    {
        var division = new Division("B", 2, Gender.Female);
        division.Update("A", 1, Gender.Male);

        Assert.Equal("A", division.Name);
        Assert.Equal(Gender.Male, division.Gender);
    }
}
