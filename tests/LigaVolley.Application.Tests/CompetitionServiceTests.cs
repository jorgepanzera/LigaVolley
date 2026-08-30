using LigaVolley.Application.Competitions;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;

namespace LigaVolley.Application.Tests;

public sealed class CompetitionServiceTests
{
    [Fact]
    public async Task Create_FromFormat_InstantiatesTheCompleteStructure()
    {
        var fixture = await CreateFixture();
        var result = await fixture.Service.CreateAsync(Request(new(CompetitionStructureSourceType.Format, 0, null)), default);

        Assert.Equal(CompetitionStatus.Draft, result.Status);
        Assert.Same(fixture.Format, fixture.Competitions.Added!.CompetitionFormat);
        Assert.Equal(fixture.Format.Phases.Count, fixture.Competitions.Added.Phases.Count);
        Assert.Equal(fixture.Format.Phases.Sum(x => x.Series.Count), fixture.Competitions.Added.Phases.Sum(x => x.Series.Count));
        Assert.Equal(4, fixture.Competitions.Added.Phases.SelectMany(x => x.Series).SelectMany(x => x.ParticipantSources).Count());
        Assert.Equal(1, fixture.Unit.SaveCount);
    }

    [Fact]
    public async Task Create_FromCompetition_ReusesFormatButCreatesIndependentStructure()
    {
        var fixture = await CreateFixture();
        var model = new Competition("Model", fixture.Season, fixture.Division, fixture.Format, CompetitionPeriodType.Annual, null, null);
        fixture.Competitions.Seed(99, model);

        await fixture.Service.CreateAsync(Request(new(CompetitionStructureSourceType.Competition, null, 99)), default);
        var created = fixture.Competitions.Added!;

        Assert.Same(model.CompetitionFormat, created.CompetitionFormat);
        Assert.NotSame(model.Phases[0], created.Phases[0]);
        Assert.NotSame(model.Phases.SelectMany(x => x.Series).First(), created.Phases.SelectMany(x => x.Series).First());
        Assert.Equal(model.Phases.Select(x => x.Code), created.Phases.Select(x => x.Code));
    }

    [Fact]
    public async Task Create_FromFormatAndCompetition_RejectInactiveFormat()
    {
        var fixture=await CreateFixture();fixture.Format.SetActive(false);
        var direct=await Assert.ThrowsAsync<LigaVolley.Application.Common.ResourceConflictException>(()=>fixture.Service.CreateAsync(Request(new(CompetitionStructureSourceType.Format,0,null)),default));Assert.Equal("competition_format_inactive",direct.Code);
        var model=new Competition("Model",fixture.Season,fixture.Division,fixture.Format,CompetitionPeriodType.Annual,null,null);fixture.Competitions.Seed(99,model);
        var based=await Assert.ThrowsAsync<LigaVolley.Application.Common.ResourceConflictException>(()=>fixture.Service.CreateAsync(Request(new(CompetitionStructureSourceType.Competition,null,99)),default));Assert.Equal("competition_format_inactive",based.Code);
    }

    [Theory]
    [InlineData(CompetitionStructureSourceType.Format, null, null)]
    [InlineData(CompetitionStructureSourceType.Format, 1, 1)]
    [InlineData(CompetitionStructureSourceType.Competition, null, null)]
    [InlineData(CompetitionStructureSourceType.Competition, 1, 1)]
    public async Task Create_RejectsInvalidStructureSourceXor(CompetitionStructureSourceType type, int? formatId, int? sourceId)
    {
        var fixture = await CreateFixture();
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.CreateAsync(Request(new(type, formatId, sourceId)), default));
    }

    private static CreateCompetitionRequest Request(CompetitionStructureSourceDto source)
        => new("Apertura", 1, 1, CompetitionPeriodType.Opening, new DateOnly(2026, 3, 1), new DateOnly(2026, 6, 30), source);

    private static async Task<Fixture> CreateFixture()
    {
        var formatRepo = new FakeCompetitionFormatRepository();
        var formatService = new CompetitionFormatService(formatRepo, new FakeUnitOfWork());
        await formatService.CreateAsync(new("TEST8", "Test 8", null, 8, 8, CompetitionFormatServiceTests.EightTeamDefinition()), default);
        var format = formatRepo.Added!; format.SetActive(true); formatRepo.Seed(0, format);
        var season = new Season(2026, "2026", null, null); var seasons = new FakeSeasonRepository(); seasons.Seed(1, season);
        var division = new Division("A Female", 1, Gender.Female); var divisions = new FakeDivisionRepository(); divisions.Seed(1, division);
        var competitions = new FakeCompetitionRepository(); var unit = new FakeUnitOfWork();
        return new(new CompetitionService(competitions, seasons, divisions, formatRepo, unit), competitions, unit, season, division, format);
    }

    private sealed record Fixture(CompetitionService Service, FakeCompetitionRepository Competitions, FakeUnitOfWork Unit, Season Season, Division Division, LigaVolley.Domain.CompetitionFormats.CompetitionFormat Format);
}
