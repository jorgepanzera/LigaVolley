using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.Tests;

public sealed class CompetitionFormatServiceTests
{
    [Fact]
    public async Task Validate_AcceptsRealEightTeamFormat()
    {
        var result = await Service().ValidateAsync(new(8, 8, EightTeamDefinition()));
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(x => x.Message)));
    }

    [Fact]
    public async Task Validate_AcceptsRealTenTeamFormatWithHalfGroups()
    {
        var result = await Service().ValidateAsync(new(10, 10, TenTeamDefinition()));
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(x => x.Message)));
    }

    [Fact]
    public async Task Validate_RejectsDuplicateSeriesCodesAcrossPhases()
    {
        var definition = EightTeamDefinition();
        var phases = definition.Phases.ToArray();
        phases[2] = phases[2] with { Series = [phases[2].Series[0] with { Code = "SF1" }] };
        var result = await Service().ValidateAsync(new(8, 8, definition with { Phases = phases }));
        Assert.Contains(result.Errors, x => x.Code == "format.duplicate_series_code");
    }

    [Theory]
    [InlineData(FixtureMode.MirroredHomeAway, 1)]
    [InlineData(FixtureMode.MirroredHomeAway, 3)]
    [InlineData(FixtureMode.BalancedRandom, 2)]
    [InlineData(FixtureMode.Playoff, 1)]
    public async Task Validate_RejectsUnsupportedV1RoundRobinCombinations(FixtureMode mode, short rounds)
    {
        var definition = new CompetitionFormatDefinitionDto([new("REG", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, rounds, mode, [], [])], [], [], [], []);
        var result = await Service().ValidateAsync(new(4, 8, definition));
        Assert.Contains(result.Errors, x => x.Code == "format.unsupported_fixture_configuration");
    }

    [Fact]
    public async Task Create_AndCloneProduceIndependentAggregates()
    {
        var repository = new FakeCompetitionFormatRepository(); var unit = new FakeUnitOfWork(); var service = new CompetitionFormatService(repository, unit);
        var created = await service.CreateAsync(new("RR8", "Eight", null, 8, 8, EightTeamDefinition()), default);
        Assert.Equal("RR8", created.Code); Assert.Equal(1, unit.SaveCount);
        repository.Seed(1, repository.Added!);
        var clone = await service.CloneAsync(1, new("RR8_V2", "Eight v2", null), default);
        Assert.Equal("RR8_V2", clone.Code); Assert.NotSame(repository.Added, repository.GetAsync(1, false, default).Result);
    }

    private static CompetitionFormatService Service() => new(new FakeCompetitionFormatRepository(), new FakeUnitOfWork());

    internal static CompetitionFormatDefinitionDto EightTeamDefinition()
    {
        var regular = Phase("REGULAR", PhaseType.RoundRobin, PhaseRole.Regular, 1, 2, FixtureMode.MirroredHomeAway);
        var sf = Phase("SF", PhaseType.Playoff, PhaseRole.Semifinal, 2, null, FixtureMode.Playoff, series: [Series("SF1", 1, 2, 1, 0), Series("SF2", 2, 2, 1, 0)]);
        var third = Phase("THIRD", PhaseType.Playoff, PhaseRole.ThirdPlace, 3, null, FixtureMode.Playoff, series: [Series("THIRD", 1, 1, 0, 0, new(1, SeriesParticipantSourceType.SeriesLoser, "SF1"), new(2, SeriesParticipantSourceType.SeriesLoser, "SF2"))]);
        var final = Phase("FINAL", PhaseType.Playoff, PhaseRole.Final, 3, null, FixtureMode.Playoff, series: [Series("FINAL", 1, 1, 0, 0, new(1, SeriesParticipantSourceType.SeriesWinner, "SF1"), new(2, SeriesParticipantSourceType.SeriesWinner, "SF2"))]);
        return new([regular, sf, third, final], [Q("REGULAR", 1, "SF", "SF1", 1, 1), Q("REGULAR", 4, "SF", "SF1", 2, 2), Q("REGULAR", 2, "SF", "SF2", 1, 3), Q("REGULAR", 3, "SF", "SF2", 2, 4)], Scoring(), Tiebreak(), []);
    }

    internal static CompetitionFormatDefinitionDto TenTeamDefinition()
    {
        var first = Phase("FIRST", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom);
        var second = Phase("SECOND", PhaseType.GroupStage, PhaseRole.Regular, 2, null, null, [new("CHAMP", "Championship", GroupRole.Championship, 1, 1, FixtureMode.BalancedRandom, CarryOverMode.None), new("RELEG", "Relegation", GroupRole.Relegation, 2, 1, FixtureMode.BalancedRandom, CarryOverMode.None)]);
        var sf = Phase("SF", PhaseType.Playoff, PhaseRole.Semifinal, 3, null, FixtureMode.Playoff, series: [Series("SF1", 1, 2, 1, 0), Series("SF2", 2, 2, 1, 0)]);
        var final = Phase("FINAL", PhaseType.Playoff, PhaseRole.Final, 4, null, FixtureMode.Playoff, series: [Series("FINAL", 1, 1, 0, 0, new(1, SeriesParticipantSourceType.SeriesWinner, "SF1"), new(2, SeriesParticipantSourceType.SeriesWinner, "SF2"))]);
        var rules = new List<FormatQualificationRuleInputDto> { new("FIRST", null, QualificationSelectionMode.TopHalf, null, null, QualificationTargetType.Group, "SECOND", "CHAMP", null, null, 1), new("FIRST", null, QualificationSelectionMode.BottomHalf, null, null, QualificationTargetType.Group, "SECOND", "RELEG", null, null, 2), Q("SECOND",1,"SF","SF1",1,3,"CHAMP"), Q("SECOND",4,"SF","SF1",2,4,"CHAMP"), Q("SECOND",2,"SF","SF2",1,5,"CHAMP"), Q("SECOND",3,"SF","SF2",2,6,"CHAMP") };
        return new([first, second, sf, final], rules, Scoring(), Tiebreak(), [new(MovementType.Relegation, MovementSourceType.GroupLastN, "SECOND", "RELEG", null, 1, 2, 1, true)]);
    }

    private static FormatPhaseInputDto Phase(string code, PhaseType type, PhaseRole role, short sequence, short? rounds, FixtureMode? mode, IReadOnlyList<FormatGroupInputDto>? groups = null, IReadOnlyList<FormatPlayoffSeriesInputDto>? series = null) => new(code, code, type, role, sequence, rounds, mode, groups ?? [], series ?? []);
    private static FormatPlayoffSeriesInputDto Series(string code, short sequence, short wins, short w1, short w2, params SeriesParticipantSourceInputDto[] sources) => new(code, code, sequence, wins, w1, w2, sources);
    private static FormatQualificationRuleInputDto Q(string source, short position, string targetPhase, string targetSeries, byte side, short sequence, string? sourceGroup = null) => new(source, sourceGroup, QualificationSelectionMode.PositionRange, position, position, QualificationTargetType.Series, targetPhase, null, targetSeries, side, sequence);
    private static FormatScoringRuleInputDto[] Scoring() => [new(3,0,3,0), new(3,1,3,0), new(3,2,2,1)];
    private static FormatTiebreakRuleInputDto[] Tiebreak() => [new(1,TiebreakCriterion.TablePoints,SortDirection.Desc), new(2,TiebreakCriterion.MatchWins,SortDirection.Desc)];
}
