using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Infrastructure.Persistence;
using LigaVolley.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LigaVolley.IntegrationTests;

public sealed class CompetitionTestDataResetTests
{
    [Fact]
    public async Task ResetIsRejectedBeforeDatabaseAccessOutsideDevelopment()
    {
        var options = new DbContextOptionsBuilder<LigaVolleyDbContext>().UseSqlServer("Server=invalid;Database=invalid;User Id=invalid;Password=invalid;TrustServerCertificate=True").Options;
        await using var db = new LigaVolleyDbContext(options);
        var resetter = new CompetitionTestDataResetter(db, NullLogger<CompetitionTestDataResetter>.Instance);
        var exception = await Assert.ThrowsAsync<CompetitionTestDataResetException>(() => resetter.ResetAsync(false));
        Assert.Contains("only in Development", exception.Message);
    }

    [Theory]
    [InlineData(1, 6, 8)]
    [InlineData(2, 9, 16)]
    public void CanonicalDefinitionsValidateEverySupportedTeamCount(int id, short minTeams, short maxTeams)
    {
        var canonical = CanonicalCompetitionFormats.Get(id);
        var validation = CompetitionFormatDefinitionFactory.Validate(canonical.MinTeams, canonical.MaxTeams, canonical.Definition);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors.Select(x => $"{x.Code}: {x.Message}")));
        Assert.Equal(Enumerable.Range(minTeams, maxTeams - minTeams + 1).Select(x => (short)x), validation.TeamCounts.Select(x => x.TeamCount));
        Assert.All(validation.TeamCounts, x => Assert.True(x.IsValid));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CanonicalPlayoffsScoringAndTiebreaksAreExact(int id)
    {
        var canonical = CanonicalCompetitionFormats.Get(id);
        var definition = canonical.Definition;
        var series = definition.Phases.SelectMany(x => x.Series).ToDictionary(x => x.Code);
        Assert.Equal((2, 1, 0), (series["SF1"].WinsRequired, series["SF1"].Team1InitialWins, series["SF1"].Team2InitialWins));
        Assert.Equal((2, 1, 0), (series["SF2"].WinsRequired, series["SF2"].Team1InitialWins, series["SF2"].Team2InitialWins));
        Assert.Equal((1, 0, 0), (series["THIRD_PLACE"].WinsRequired, series["THIRD_PLACE"].Team1InitialWins, series["THIRD_PLACE"].Team2InitialWins));
        Assert.Equal((1, 0, 0), (series["FINAL"].WinsRequired, series["FINAL"].Team1InitialWins, series["FINAL"].Team2InitialWins));
        Assert.Equal(4, series.Values.SelectMany(x => x.ParticipantSources).Count());
        Assert.Equal(new[] { (3, 0, 2, 1), (3, 1, 2, 1), (3, 2, 2, 1) }, definition.ScoringRules.Select(x => ((int)x.WinnerSets, (int)x.LoserSets, (int)x.WinnerTablePoints, (int)x.LoserTablePoints)));
        Assert.Equal(new[] { TiebreakCriterion.TablePoints, TiebreakCriterion.MatchWins, TiebreakCriterion.SetRatio, TiebreakCriterion.PointRatio, TiebreakCriterion.HeadToHead }, definition.TiebreakRules.OrderBy(x => x.Sequence).Select(x => x.Criterion));
        Assert.All(definition.TiebreakRules, x => Assert.Equal(SortDirection.Desc, x.SortDirection));
    }

    [Fact]
    public void CanonicalMovementsAndSplitAreExact()
    {
        var first = CanonicalCompetitionFormats.Get(1).Definition;
        var second = CanonicalCompetitionFormats.Get(2).Definition;
        AssertMovement(first, MovementType.Promotion, MovementSourceType.SeriesResult, "PLAYOFF", null, "FINAL", -1);
        AssertMovement(first, MovementType.Relegation, MovementSourceType.PhaseLastN, "REGULAR", null, null, 1);
        AssertMovement(second, MovementType.Promotion, MovementSourceType.SeriesResult, "PLAYOFF", null, "FINAL", -1);
        AssertMovement(second, MovementType.Relegation, MovementSourceType.GroupLastN, "SECOND_STAGE", "RELEGATION", null, 1);
        var groups = second.Phases.Single(x => x.Code == "SECOND_STAGE").Groups;
        Assert.Equal(new[] { "CHAMPIONSHIP", "RELEGATION" }, groups.OrderBy(x => x.Sequence).Select(x => x.Code));
        Assert.All(groups, x => Assert.Equal(CarryOverMode.None, x.CarryOverMode));
        Assert.Contains(second.QualificationRules, x => x.SelectionMode == QualificationSelectionMode.TopHalf && x.TargetGroupCode == "CHAMPIONSHIP");
        Assert.Contains(second.QualificationRules, x => x.SelectionMode == QualificationSelectionMode.BottomHalf && x.TargetGroupCode == "RELEGATION");
    }

    private static void AssertMovement(CompetitionFormatDefinitionDto definition, MovementType type, MovementSourceType sourceType,
        string phase, string? group, string? series, short delta)
    {
        var rule = Assert.Single(definition.MovementRules.Where(x => x.MovementType == type));
        Assert.Equal((sourceType, phase, group, series, (short)1, (short)2, delta, true),
            (rule.SourceType, rule.SourcePhaseCode, rule.SourceGroupCode, rule.SourceSeriesCode, rule.FromPosition, rule.ToPosition, rule.TargetLevelDelta, rule.AppliesIfTargetExists));
    }
}
