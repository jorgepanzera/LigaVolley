using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Infrastructure.Persistence.Seed;

public sealed record CanonicalCompetitionFormatDefinition(
    int Id,
    string Code,
    string Name,
    string Description,
    short MinTeams,
    short MaxTeams,
    CompetitionFormatDefinitionDto Definition);

public static class CanonicalCompetitionFormats
{
    public static IReadOnlyList<CanonicalCompetitionFormatDefinition> All { get; } = [RoundRobin(), SplitStage()];

    public static CanonicalCompetitionFormatDefinition Get(int id) => All.Single(x => x.Id == id);

    private static CanonicalCompetitionFormatDefinition RoundRobin()
    {
        var regular = new FormatPhaseInputDto("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 2, FixtureMode.MirroredHomeAway, [], []);
        var playoffs = Playoffs(2);
        return new(1, "ROUND_ROBIN", "Round Robin 6-8", "Two round-robin wheels followed by advantaged semifinals, third place and final.", 6, 8,
            new([regular, playoffs], SemifinalQualifications("REGULAR", null), Scoring(), Tiebreaks(),
            [
                new(MovementType.Promotion, MovementSourceType.SeriesResult, "PLAYOFF", null, "FINAL", 1, 2, -1, true),
                new(MovementType.Relegation, MovementSourceType.PhaseLastN, "REGULAR", null, null, 1, 2, 1, true)
            ]));
    }

    private static CanonicalCompetitionFormatDefinition SplitStage()
    {
        var regular = new FormatPhaseInputDto("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom, [], []);
        var championship = new FormatGroupInputDto("CHAMPIONSHIP", "Championship", GroupRole.Championship, 1, 1, FixtureMode.BalancedRandom, CarryOverMode.None);
        var relegation = new FormatGroupInputDto("RELEGATION", "Relegation", GroupRole.Relegation, 2, 1, FixtureMode.BalancedRandom, CarryOverMode.None);
        var second = new FormatPhaseInputDto("SECOND_STAGE", "Second Stage", PhaseType.GroupStage, PhaseRole.Championship, 2, null, null, [championship, relegation], []);
        var qualification = new List<FormatQualificationRuleInputDto>
        {
            new("REGULAR", null, QualificationSelectionMode.TopHalf, null, null, QualificationTargetType.Group, "SECOND_STAGE", "CHAMPIONSHIP", null, null, 1),
            new("REGULAR", null, QualificationSelectionMode.BottomHalf, null, null, QualificationTargetType.Group, "SECOND_STAGE", "RELEGATION", null, null, 2)
        };
        qualification.AddRange(SemifinalQualifications("SECOND_STAGE", "CHAMPIONSHIP", 3));
        return new(2, "SPLIT_STAGE", "Split Stage 9-16", "One regular wheel, Championship/Relegation split and advantaged playoffs.", 9, 16,
            new([regular, second, Playoffs(3)], qualification, Scoring(), Tiebreaks(),
            [
                new(MovementType.Promotion, MovementSourceType.SeriesResult, "PLAYOFF", null, "FINAL", 1, 2, -1, true),
                new(MovementType.Relegation, MovementSourceType.GroupLastN, "SECOND_STAGE", "RELEGATION", null, 1, 2, 1, true)
            ]));
    }

    private static FormatPhaseInputDto Playoffs(short sequence)
    {
        var sf1 = new FormatPlayoffSeriesInputDto("SF1", "Semifinal 1", 1, 2, 1, 0, []);
        var sf2 = new FormatPlayoffSeriesInputDto("SF2", "Semifinal 2", 2, 2, 1, 0, []);
        var third = new FormatPlayoffSeriesInputDto("THIRD_PLACE", "Third Place", 3, 1, 0, 0,
            [new(1, SeriesParticipantSourceType.SeriesLoser, "SF1"), new(2, SeriesParticipantSourceType.SeriesLoser, "SF2")]);
        var final = new FormatPlayoffSeriesInputDto("FINAL", "Final", 4, 1, 0, 0,
            [new(1, SeriesParticipantSourceType.SeriesWinner, "SF1"), new(2, SeriesParticipantSourceType.SeriesWinner, "SF2")]);
        return new("PLAYOFF", "Playoffs", PhaseType.Playoff, PhaseRole.Semifinal, sequence, null, FixtureMode.Playoff, [], [sf1, sf2, third, final]);
    }

    private static IReadOnlyList<FormatQualificationRuleInputDto> SemifinalQualifications(string sourcePhase, string? sourceGroup, short firstSequence = 1) =>
    [
        new(sourcePhase, sourceGroup, QualificationSelectionMode.PositionRange, 1, 1, QualificationTargetType.Series, "PLAYOFF", null, "SF1", 1, firstSequence),
        new(sourcePhase, sourceGroup, QualificationSelectionMode.PositionRange, 4, 4, QualificationTargetType.Series, "PLAYOFF", null, "SF1", 2, (short)(firstSequence + 1)),
        new(sourcePhase, sourceGroup, QualificationSelectionMode.PositionRange, 2, 2, QualificationTargetType.Series, "PLAYOFF", null, "SF2", 1, (short)(firstSequence + 2)),
        new(sourcePhase, sourceGroup, QualificationSelectionMode.PositionRange, 3, 3, QualificationTargetType.Series, "PLAYOFF", null, "SF2", 2, (short)(firstSequence + 3))
    ];

    private static IReadOnlyList<FormatScoringRuleInputDto> Scoring() =>
    [new(3, 0, 2, 1), new(3, 1, 2, 1), new(3, 2, 2, 1)];

    private static IReadOnlyList<FormatTiebreakRuleInputDto> Tiebreaks() =>
    [
        new(1, TiebreakCriterion.TablePoints, SortDirection.Desc),
        new(2, TiebreakCriterion.MatchWins, SortDirection.Desc),
        new(3, TiebreakCriterion.SetRatio, SortDirection.Desc),
        new(4, TiebreakCriterion.PointRatio, SortDirection.Desc),
        new(5, TiebreakCriterion.HeadToHead, SortDirection.Desc)
    ];
}
