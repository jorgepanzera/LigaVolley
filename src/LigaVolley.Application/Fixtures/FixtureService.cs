using System.Security.Cryptography;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Application.Clubs;

namespace LigaVolley.Application.Fixtures;

public sealed class FixtureService(ICompetitionRepository competitions, ITeamEntryRepository entries, IFixtureRepository fixtures, IUnitOfWork unit)
{
    public async Task<GenerateFixtureResponse> GenerateInitialAsync(int competitionId, GenerateFixtureRequest request, CancellationToken ct)
    {
        var competition = await RequiredCompetition(competitionId, true, ct);
        if (competition.Status != CompetitionStatus.Draft)
            throw new ResourceConflictException("fixture_competition_not_draft", "Initial fixture can only be generated for a Draft competition.");

        var validEntries = (await entries.ListAsync(competitionId, true, ct)).Where(x => x.Status == TeamEntryStatus.Active).OrderBy(x => x.TeamEntryId).ToArray();
        var format = competition.CompetitionFormat;
        if (validEntries.Length < format.MinTeams || validEntries.Length > format.MaxTeams)
            throw new ResourceConflictException("fixture_team_count_out_of_range", $"Fixture requires between {format.MinTeams} and {format.MaxTeams} valid teams; found {validEntries.Length}.");

        var phase = ResolveInitialScope(competition);
        if (await fixtures.GenerationExistsAsync(competitionId, phase.CompetitionPhaseId, null, ct))
            throw new ResourceConflictException("initial_fixture_already_generated", "Initial fixture has already been generated.");

        var randomSeed = request.RandomSeed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
        var mirrored = phase.FixtureMode == FixtureMode.MirroredHomeAway;
        var pairings = RoundRobinFixtureGenerator.Generate(validEntries.Select(x => x.TeamEntryId).ToArray(), randomSeed, mirrored);
        var entryById = validEntries.ToDictionary(x => x.TeamEntryId);
        var matches = pairings.Select(x => new Match(competition, phase, null, entryById[x.HomeParticipantId], entryById[x.AwayParticipantId], x.RoundNumber, x.MatchNumber)).ToArray();
        var generation = new FixtureGeneration(competition, phase, null, randomSeed, DateTime.UtcNow);
        fixtures.AddGeneration(generation); fixtures.AddMatches(matches);
        await unit.SaveChangesAsync(ct);
        return new(competitionId, matches.Length, randomSeed, [new(phase.CompetitionPhaseId, phase.Code, matches.Length)]);
    }

    public async Task<CompetitionFixtureDto> GetAsync(int competitionId, CancellationToken ct)
    {
        var competition = await RequiredCompetition(competitionId, false, ct);
        var generations = await fixtures.ListGenerationsAsync(competitionId, ct);
        var matches = await fixtures.ListMatchesAsync(competitionId, ct);
        return ToDto(competition, generations, matches);
    }

    private static CompetitionPhase ResolveInitialScope(Competition competition)
    {
        if (competition.Phases.Count == 0) throw Unsupported(0);
        var firstSequence = competition.Phases.Min(x => x.Sequence);
        var candidates = competition.Phases.Where(x => x.Sequence == firstSequence && x.Groups.Count == 0 && x.PhaseType == PhaseType.RoundRobin && ValidConfiguration(x.FixtureMode, x.Rounds)).ToArray();
        if (candidates.Length != 1) throw Unsupported(candidates.Length);
        return candidates[0];
    }
    private static bool ValidConfiguration(FixtureMode? mode, short? rounds) => (mode, rounds) is (FixtureMode.MirroredHomeAway, 2) or (FixtureMode.BalancedRandom, 1);
    private static ResourceConflictException Unsupported(int count) => new("unsupported_initial_fixture_scope", $"V1 requires exactly one generable initial fixture scope; found {count}.");
    private async Task<Competition> RequiredCompetition(int id, bool tracking, CancellationToken ct) => await competitions.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Competition", id);

    private static CompetitionFixtureDto ToDto(Competition competition, IReadOnlyList<FixtureGeneration> generations, IReadOnlyList<Match> matches)
    {
        FixtureGenerationDto Metadata(FixtureGeneration x) => new(x.FixtureGenerationId, x.RandomSeed, x.GeneratedAt);
        FixtureMatchDto MatchDto(Match x) => new(x.MatchId, x.RoundNumber, x.MatchNumber,
            new(x.HomeTeamEntryId!.Value,x.HomeTeamEntry!.TeamId,x.HomeTeamEntry.Team.Name,x.HomeTeamEntry.Status,x.HomeTeamEntry.Team.Club is null?null:ClubService.LogoUrl(x.HomeTeamEntry.Team.Club)),
            new(x.AwayTeamEntryId!.Value,x.AwayTeamEntry!.TeamId,x.AwayTeamEntry.Team.Name,x.AwayTeamEntry.Status,x.AwayTeamEntry.Team.Club is null?null:ClubService.LogoUrl(x.AwayTeamEntry.Team.Club)),x.MatchDate,x.VenueId,x.Status);
        var phases = competition.Phases.OrderBy(x => x.Sequence).Select(phase =>
        {
            var phaseGeneration = generations.SingleOrDefault(x => x.PhaseId == phase.CompetitionPhaseId && x.PhaseGroupId == null);
            var phaseMatches = matches.Where(x => x.PhaseId == phase.CompetitionPhaseId && x.PhaseGroupId == null).OrderBy(x => x.MatchNumber).Select(MatchDto).ToArray();
            var groups = phase.Groups.OrderBy(x => x.Sequence).Select(group => { var generation = generations.SingleOrDefault(x => x.PhaseGroupId == group.PhaseGroupId); var groupMatches = matches.Where(x => x.PhaseGroupId == group.PhaseGroupId).OrderBy(x => x.MatchNumber).Select(MatchDto).ToArray(); return new FixtureGroupDto(group.PhaseGroupId, group.Code, group.Name, group.FixtureMode, group.Rounds, generation is not null, generation is null ? null : Metadata(generation), groupMatches); }).ToArray();
            var series = phase.Series.OrderBy(x => x.Sequence).Select(series => new FixtureSeriesDto(series.PlayoffSeriesId, series.Code, series.Name, false, matches.Where(x => x.SeriesId == series.PlayoffSeriesId).OrderBy(x => x.MatchNumber).Select(MatchDto).ToArray())).ToArray();
            return new FixturePhaseDto(phase.CompetitionPhaseId, phase.Code, phase.Name, phase.PhaseType, phase.FixtureMode, phase.Rounds, phaseGeneration is not null, phaseGeneration is null ? null : Metadata(phaseGeneration), phaseMatches, groups, series);
        }).ToArray();
        return new(competition.CompetitionId, phases);
    }
}
