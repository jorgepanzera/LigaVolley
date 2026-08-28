using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Competitions;

public sealed class CompetitionSchedulingService(
    ICompetitionRepository competitions,
    ICompetitionFormatRepository formats,
    ITeamEntryRepository entries,
    IFixtureRepository fixtures,
    IUnitOfWork unit)
{
    public async Task<CompetitionSchedulePreviewDto> PreviewAsync(int competitionId, CancellationToken ct)
    {
        var competition = await Required(competitionId, false, ct);
        return await EvaluateAsync(competition, ct);
    }

    public async Task<CompetitionScheduleResultDto> ScheduleAsync(int competitionId, CancellationToken ct)
    {
        await using var transaction = await unit.BeginSerializableTransactionAsync(ct);
        var competition = await Required(competitionId, true, ct);
        if (competition.Status == CompetitionStatus.Scheduled && competition.ScheduledAt.HasValue)
        {
            var current = await EvaluateAsync(competition, ct);
            await transaction.CommitAsync(ct);
            return Result(current, competition.ScheduledAt.Value, true);
        }
        if (competition.Status != CompetitionStatus.Draft)
            throw new ResourceConflictException("competition_cannot_schedule", $"Competition in {competition.Status} status cannot be scheduled.");

        var preview = await EvaluateAsync(competition, ct);
        if (!preview.CanSchedule)
            throw new ResourceConflictException("competition_cannot_schedule", "Competition is not ready to be marked as scheduled.")
            {
                Extensions = new Dictionary<string, object?> { ["blockers"] = preview.Blockers }
            };

        var now = DateTimeOffset.UtcNow;
        competition.Schedule(now);
        await unit.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result(preview, now, false);
    }

    private async Task<CompetitionSchedulePreviewDto> EvaluateAsync(Competition competition, CancellationToken ct)
    {
        var allEntries = await entries.ListAsync(competition.CompetitionId, false, ct);
        var active = allEntries.Where(x => x.Status == TeamEntryStatus.Active).OrderBy(x => x.TeamEntryId).ToArray();
        var matches = await fixtures.ListMatchesAsync(competition.CompetitionId, ct);
        var format = await formats.GetAsync(competition.CompetitionFormatId, false, ct)
            ?? throw new ResourceConflictException("competition_schedule_structure_inconsistent", "The referenced CompetitionFormat no longer exists.");
        var blockers = new List<CompetitionScheduleBlockerDto>();

        if (competition.Status != CompetitionStatus.Draft)
            blockers.Add(new("competition_schedule_not_draft", "Competition must be in Draft status."));
        if (active.Length == 0)
            blockers.Add(new("competition_schedule_no_active_entries", "At least one ACTIVE TeamEntry is required."));
        if (active.Length < format.MinTeams)
            blockers.Add(new("competition_schedule_team_count_below_minimum", $"At least {format.MinTeams} ACTIVE teams are required.", active.Select(x => x.TeamEntryId).ToArray()));
        if (active.Length > format.MaxTeams)
            blockers.Add(new("competition_schedule_team_count_above_maximum", $"At most {format.MaxTeams} ACTIVE teams are allowed.", active.Select(x => x.TeamEntryId).ToArray()));

        var structureConsistent = StructureIsConsistent(competition, format);
        if (!structureConsistent)
            blockers.Add(new("competition_schedule_structure_inconsistent", "The instantiated competition structure is inconsistent with its format."));

        var initialPhase = InitialPhase(competition);
        var initialMatches = initialPhase is null ? [] : matches.Where(x => x.PhaseId == initialPhase.CompetitionPhaseId && x.PhaseGroupId == null && x.SeriesId == null).ToArray();
        if (initialPhase is null)
            blockers.Add(new("competition_schedule_structure_inconsistent", "The initial fixture scope is not supported or is ambiguous."));
        else if (initialMatches.Length == 0)
            blockers.Add(new("competition_schedule_fixture_missing", "The initial fixture has not been generated."));
        else
        {
            var expectedCount = active.Length >= 2
                ? RoundRobinFixtureGenerator.Generate(active.Select(x => x.TeamEntryId).ToArray(), 1, initialPhase.FixtureMode == FixtureMode.MirroredHomeAway).Count
                : 0;
            if (active.Length >= 2 && initialMatches.Length != expectedCount)
                blockers.Add(new("competition_schedule_fixture_incomplete", $"Initial fixture contains {initialMatches.Length} matches; {expectedCount} are required.", MatchIds: initialMatches.Select(x => x.MatchId).ToArray()));
            var activeIds = active.Select(x => x.TeamEntryId).ToHashSet();
            var fixtureIds = initialMatches.SelectMany(x => new[] { x.HomeTeamEntryId, x.AwayTeamEntryId }).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
            if (!fixtureIds.SetEquals(activeIds) || initialMatches.Any(x => !x.HomeTeamEntryId.HasValue || !x.AwayTeamEntryId.HasValue))
                blockers.Add(new("competition_schedule_fixture_participant_mismatch", "Initial fixture participants do not exactly match the ACTIVE TeamEntries.", active.Select(x => x.TeamEntryId).ToArray(), initialMatches.Select(x => x.MatchId).ToArray()));
        }

        var started = matches.Where(x => x.Status is MatchStatus.InProgress or MatchStatus.Finished).Select(x => x.MatchId).ToArray();
        if (started.Length > 0)
            blockers.Add(new("competition_schedule_match_already_started", "A Draft competition cannot contain started or finished matches.", MatchIds: started));

        var scheduled = initialMatches.Count(x => x.MatchDate.HasValue && x.VenueId.HasValue);
        return new(competition.CompetitionId, competition.Status, blockers.Count == 0, active.Length, format.MinTeams, format.MaxTeams,
            initialMatches.Length, scheduled, initialMatches.Length - scheduled, blockers);
    }

    private static bool StructureIsConsistent(Competition competition, CompetitionFormat format)
    {
        if (competition.Phases.Count != format.Phases.Count) return false;
        return competition.Phases.All(phase => format.Phases.Any(source =>
            source.FormatPhaseId == phase.FormatPhaseId && source.Code == phase.Code && source.Sequence == phase.Sequence &&
            source.PhaseType == phase.PhaseType && source.Groups.Count == phase.Groups.Count && source.Series.Count == phase.Series.Count));
    }

    private static CompetitionPhase? InitialPhase(Competition competition)
    {
        if (competition.Phases.Count == 0) return null;
        var sequence = competition.Phases.Min(x => x.Sequence);
        var candidates = competition.Phases.Where(x => x.Sequence == sequence && x.Groups.Count == 0 && x.PhaseType == PhaseType.RoundRobin &&
            (x.FixtureMode, x.Rounds) is (FixtureMode.MirroredHomeAway, 2) or (FixtureMode.BalancedRandom, 1)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private async Task<Competition> Required(int id, bool tracking, CancellationToken ct)
        => await competitions.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Competition", id);

    private static CompetitionScheduleResultDto Result(CompetitionSchedulePreviewDto x, DateTimeOffset at, bool already)
        => new(x.CompetitionId, CompetitionStatus.Scheduled, at, already, x.ActiveTeamEntries, x.InitialMatches, x.ScheduledMatches, x.UnscheduledMatches);
}
