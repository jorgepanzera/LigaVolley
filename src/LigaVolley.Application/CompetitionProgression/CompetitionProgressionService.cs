using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.PlayoffProgression;
using LigaVolley.Application.Standings;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;

namespace LigaVolley.Application.CompetitionProgression;

public sealed class CompetitionProgressionService(ICompetitionRepository competitions,
    ICompetitionProgressionRepository progression, IDivisionRepository divisions,
    StandingsService standings, IUnitOfWork unit)
{
    public async Task<CompetitionProgressionDto> GetProgressionAsync(int competitionId, CancellationToken ct)
    {
        var competition = await Required(competitionId, false, ct);
        var matches = await progression.ListMatchesAsync(competitionId, ct);
        ValidatePersistedState(competition, matches);
        return ToProgression(competition, matches);
    }

    public async Task<CompetitionCompletionPreviewDto> PreviewCompletionAsync(int competitionId, CancellationToken ct)
    {
        var competition = await Required(competitionId, false, ct);
        var evaluation = await Evaluate(competition, ct);
        return new(competition.CompetitionId, competition.Name, competition.Status,
            competition.Status == CompetitionStatus.Finished, evaluation.Blockers.Count == 0,
            evaluation.Blockers, evaluation.Movements);
    }

    public Task<CompetitionCompletionResultDto> CompleteAsync(int competitionId, CancellationToken ct) =>
        progression.ExecuteExclusiveAsync<CompetitionCompletionResultDto>(competitionId, async innerCt =>
        {
            var competition = await Required(competitionId, true, innerCt);
            var alreadyCompleted = competition.Status == CompetitionStatus.Finished;
            var evaluation = await Evaluate(competition, innerCt);
            if (evaluation.Blockers.Count > 0)
                throw new ResourceConflictException("competition_cannot_complete", "The competition cannot be completed because sporting blockers remain.")
                {
                    Extensions = new Dictionary<string, object?> { ["blockers"] = evaluation.Blockers }
                };
            if (!alreadyCompleted)
            {
                competition.Complete(DateTimeOffset.UtcNow);
                await unit.SaveChangesAsync(innerCt);
            }
            return new(competition.CompetitionId, competition.Status, alreadyCompleted, competition.CompletedAt, evaluation.Movements);
        }, ct);

    private async Task<Evaluation> Evaluate(Competition competition, CancellationToken ct)
    {
        var matches = await progression.ListMatchesAsync(competition.CompetitionId, ct);
        ValidatePersistedState(competition, matches);
        var blockers = StateBlockers(competition, matches);
        if (blockers.Count > 0)
            return new(blockers, []);
        var movementEvaluation = await EvaluateMovements(competition, ct);
        blockers.AddRange(movementEvaluation.Blockers);
        return blockers.Count == 0 ? new([], movementEvaluation.Results) : new(blockers, []);
    }

    private static List<CompetitionCompletionBlockerDto> StateBlockers(Competition competition, IReadOnlyList<Match> matches)
    {
        var blockers = new List<CompetitionCompletionBlockerDto>();
        if (competition.Status is CompetitionStatus.Draft or CompetitionStatus.Scheduled)
            blockers.Add(new("competition_not_in_progress", "Competition must be InProgress before completion."));
        else if (competition.Status == CompetitionStatus.Cancelled)
            blockers.Add(new("competition_cancelled", "A cancelled Competition cannot be completed."));
        if (competition.Status == CompetitionStatus.Finished)
            return blockers;

        foreach (var phase in competition.Phases.OrderBy(x => x.Sequence))
        {
            if (phase.Status == CompetitionPhaseStatus.Pending)
                blockers.Add(new("phase_pending", "A phase is still pending.", phase.CompetitionPhaseId));
            else if (phase.Status == CompetitionPhaseStatus.InProgress)
                blockers.Add(new("phase_in_progress", "A phase is still in progress.", phase.CompetitionPhaseId));
            foreach (var series in phase.Series)
            {
                var code = series.Status switch
                {
                    PlayoffSeriesStatus.Pending => "playoff_series_pending",
                    PlayoffSeriesStatus.Ready => "playoff_series_ready",
                    PlayoffSeriesStatus.InProgress => "playoff_series_in_progress",
                    PlayoffSeriesStatus.Cancelled => "playoff_series_cancelled_unresolved",
                    _ => null
                };
                if (code is not null)
                    blockers.Add(new(code, $"Playoff series {series.Code} is not sportingly resolved.", phase.CompetitionPhaseId, series.PlayoffSeriesId));
            }
        }
        blockers.AddRange(matches.Where(x => x.Status == MatchStatus.Cancelled)
            .Select(x => new CompetitionCompletionBlockerDto("match_cancelled_unresolved", "A cancelled match has no sporting resolution in v1.", x.PhaseId, x.SeriesId, x.MatchId)));
        return blockers;
    }

    private async Task<MovementEvaluation> EvaluateMovements(Competition competition, CancellationToken ct)
    {
        var results = new List<MovementResultDto>();
        var blockers = new List<CompetitionCompletionBlockerDto>();
        var allDivisions = await divisions.ListAsync(competition.Division.Gender, null, ct);
        var standingsCache = new Dictionary<(int PhaseId, int? GroupId), StandingsDto>();

        foreach (var rule in competition.CompetitionFormat.MovementRules.OrderBy(x => x.FormatMovementRuleId))
        {
            var phase = competition.Phases.SingleOrDefault(x => x.FormatPhaseId == rule.SourceFormatPhaseId)
                ?? throw InvalidMovement("Movement source phase does not exist in the Competition.");
            var source = ResolveSource(competition, phase, rule);
            IReadOnlyList<MovementCandidate> candidates;
            if (rule.SourceType == MovementSourceType.SeriesResult)
                candidates = SelectSeries(source.Series!, rule);
            else
            {
                var groupId = source.Group?.PhaseGroupId;
                var key = (phase.CompetitionPhaseId, groupId);
                if (!standingsCache.TryGetValue(key, out var table))
                {
                    table = await standings.GetAsync(competition.CompetitionId, phase.CompetitionPhaseId, groupId, ct);
                    standingsCache[key] = table;
                }
                candidates = SelectStandings(table.Positions, rule, blockers);
            }

            foreach (var candidate in candidates)
            {
                var targetOrder = competition.Division.LevelOrder + rule.TargetLevelDelta;
                var target = allDivisions.SingleOrDefault(x => x.LevelOrder == targetOrder && x.Gender == competition.Division.Gender);
                if (target is null && !rule.AppliesIfTargetExists)
                {
                    blockers.Add(new("movement_target_division_missing", "The exact target Division required by the movement rule does not exist.",
                        phase.CompetitionPhaseId, source.Series?.PlayoffSeriesId, null, rule.FormatMovementRuleId));
                    continue;
                }
                results.Add(new(rule.FormatMovementRuleId, rule.MovementType, ToSource(rule.SourceType, phase, source.Group, source.Series),
                    candidate.TeamEntryId, candidate.TeamId, candidate.TeamName, candidate.SourcePosition, candidate.StandingPosition,
                    competition.DivisionId, competition.Division.Name, competition.Division.LevelOrder,
                    target is null ? MovementResultStatus.NotApplicable : MovementResultStatus.Applied,
                    target?.DivisionId, target?.Name, target?.LevelOrder, rule.TargetLevelDelta,
                    target is null ? MovementNotAppliedReason.TargetDivisionNotFound : null));
            }
        }
        return new(results, blockers);
    }

    private static MovementScope ResolveSource(Competition competition, CompetitionPhase phase, FormatMovementRule rule)
    {
        CompetitionPhaseGroup? group = null;
        CompetitionPlayoffSeries? series = null;
        if (rule.SourceType is MovementSourceType.GroupPosition or MovementSourceType.GroupLastN)
        {
            group = phase.Groups.SingleOrDefault(x => x.FormatGroupId == rule.SourceFormatGroupId)
                ?? throw InvalidMovement("Movement source group is invalid.");
            if (rule.SourceSeries is not null) throw InvalidMovement("Group movement cannot reference a series.");
        }
        else if (rule.SourceType == MovementSourceType.SeriesResult)
        {
            series = phase.Series.SingleOrDefault(x => x.FormatSeriesId == rule.SourceFormatSeriesId)
                ?? throw InvalidMovement("Movement source series is invalid.");
            if (rule.SourceGroup is not null) throw InvalidMovement("Series movement cannot reference a group.");
        }
        else if (rule.SourceGroup is not null || rule.SourceSeries is not null)
            throw InvalidMovement("Phase movement has incompatible source references.");
        return new(group, series);
    }

    private static IReadOnlyList<MovementCandidate> SelectSeries(CompetitionPlayoffSeries series, FormatMovementRule rule)
    {
        if (series.Status != PlayoffSeriesStatus.Finished || !series.WinnerTeamEntryId.HasValue ||
            !series.Team1EntryId.HasValue || !series.Team2EntryId.HasValue || rule.FromPosition < 1 || rule.ToPosition > 2 || rule.FromPosition > rule.ToPosition)
            throw InvalidMovement("SERIES_RESULT movement configuration or persisted result is invalid.");
        var loser = series.WinnerTeamEntryId == series.Team1EntryId ? series.Team2Entry! : series.Team1Entry!;
        var ordered = new[] { series.WinnerTeamEntry!, loser };
        return Enumerable.Range(rule.FromPosition - 1, rule.ToPosition - rule.FromPosition + 1)
            .Select(index => new MovementCandidate(ordered[index].TeamEntryId, ordered[index].TeamId, ordered[index].Team.Name, index + 1, null)).ToArray();
    }

    private static IReadOnlyList<MovementCandidate> SelectStandings(IReadOnlyList<StandingPositionDto> positions,
        FormatMovementRule rule, ICollection<CompetitionCompletionBlockerDto> blockers)
    {
        int start;
        int count;
        var last = rule.SourceType is MovementSourceType.PhaseLastN or MovementSourceType.GroupLastN;
        if (rule.FromPosition < 1 || rule.ToPosition < rule.FromPosition)
            throw InvalidMovement("Movement position range is invalid.");
        if (last)
        {
            start = positions.Count - rule.ToPosition;
            count = rule.ToPosition - rule.FromPosition + 1;
        }
        else
        {
            start = rule.FromPosition - 1;
            count = rule.ToPosition - rule.FromPosition + 1;
        }
        if (start < 0 || start + count > positions.Count)
        {
            blockers.Add(new("movement_source_position_unavailable", "A position required by the movement rule does not exist.",
                MovementRuleId: rule.FormatMovementRuleId));
            return [];
        }
        if (BoundaryTie(positions, start) || BoundaryTie(positions, start + count))
        {
            blockers.Add(new("movement_boundary_tie_unresolved", "An unresolved standings tie crosses a movement boundary.",
                MovementRuleId: rule.FormatMovementRuleId));
            return [];
        }
        var selected = positions.Skip(start).Take(count);
        if (last) selected = selected.Reverse();
        return selected.Select((x, index) => new MovementCandidate(x.TeamEntryId, x.TeamId, x.TeamName,
            last ? rule.FromPosition + index : x.Position, x.Position)).ToArray();
    }

    private static bool BoundaryTie(IReadOnlyList<StandingPositionDto> positions, int boundary) =>
        boundary > 0 && boundary < positions.Count && positions[boundary - 1].Position == positions[boundary].Position;

    private static void ValidatePersistedState(Competition competition, IReadOnlyList<Match> matches)
    {
        var phaseIds = competition.Phases.Select(x => x.CompetitionPhaseId).ToHashSet();
        if (matches.Any(x => !phaseIds.Contains(x.PhaseId)))
            throw Conflict("competition_completion_inconsistent_match", "A match references an invalid Competition phase.");
        foreach (var match in matches.Where(x => x.Status == MatchStatus.Finished))
            if (!match.WinnerTeamEntryId.HasValue || match.WinnerTeamEntryId != match.HomeTeamEntryId && match.WinnerTeamEntryId != match.AwayTeamEntryId)
                throw Conflict("competition_completion_inconsistent_match", "A FINISHED match has an invalid winner.");
        foreach (var phase in competition.Phases)
        {
            var phaseMatches = matches.Where(x => x.PhaseId == phase.CompetitionPhaseId).ToArray();
            if (phase.Status == CompetitionPhaseStatus.Finished && phaseMatches.Any(x => x.Status != MatchStatus.Finished))
                throw Conflict("competition_completion_inconsistent_phase", "A FINISHED phase contains unresolved matches.");
            if (phase.Status == CompetitionPhaseStatus.Cancelled)
                throw Conflict("competition_completion_inconsistent_phase", "Cancelled phase completion semantics are not defined.");
            foreach (var series in phase.Series)
            {
                var seriesMatches = phaseMatches.Where(x => x.SeriesId == series.PlayoffSeriesId).ToArray();
                if (series.Status == PlayoffSeriesStatus.Finished)
                {
                    if (!series.Team1EntryId.HasValue || !series.Team2EntryId.HasValue || !series.WinnerTeamEntryId.HasValue)
                        throw Conflict("competition_completion_inconsistent_series", "A FINISHED series lacks participants or winner.");
                    PlayoffSeriesWins wins;
                    try
                    {
                        wins = PlayoffSeriesResultCalculator.Calculate(series.WinsRequired, series.Team1InitialWins, series.Team2InitialWins,
                            series.Team1EntryId.Value, series.Team2EntryId.Value,
                            seriesMatches.Where(x => x.Status == MatchStatus.Finished).Select(x => x.WinnerTeamEntryId!.Value));
                    }
                    catch (ResourceConflictException error)
                    {
                        throw Conflict("competition_completion_inconsistent_series", error.Message);
                    }
                    var expected = wins.WinnerSide == 1 ? series.Team1EntryId : wins.WinnerSide == 2 ? series.Team2EntryId : null;
                    if (expected != series.WinnerTeamEntryId)
                        throw Conflict("competition_completion_inconsistent_series", "A FINISHED series winner contradicts its match results.");
                }
                else if (series.WinnerTeamEntryId.HasValue)
                    throw Conflict("competition_completion_inconsistent_series", "An unfinished series has a persisted winner.");
            }
        }
    }

    private static CompetitionProgressionDto ToProgression(Competition competition, IReadOnlyList<Match> matches) =>
        new(competition.CompetitionId, competition.Name, competition.Status, Count(matches), competition.Phases.OrderBy(x => x.Sequence).Select(phase =>
        {
            var phaseMatches = matches.Where(x => x.PhaseId == phase.CompetitionPhaseId).ToArray();
            return new CompetitionPhaseProgressDto(phase.CompetitionPhaseId, phase.Code, phase.Name, phase.Sequence, phase.PhaseType, phase.Status,
                Count(phaseMatches), phase.Groups.OrderBy(x => x.Sequence).Select(group => new CompetitionGroupProgressDto(group.PhaseGroupId, group.Code, group.Name,
                    Count(phaseMatches.Where(x => x.PhaseGroupId == group.PhaseGroupId)))).ToArray(),
                phase.Series.OrderBy(x => x.Sequence).Select(series =>
                {
                    var seriesMatches = phaseMatches.Where(x => x.SeriesId == series.PlayoffSeriesId).ToArray();
                    var real1 = seriesMatches.Count(x => x.Status == MatchStatus.Finished && x.WinnerTeamEntryId == series.Team1EntryId);
                    var real2 = seriesMatches.Count(x => x.Status == MatchStatus.Finished && x.WinnerTeamEntryId == series.Team2EntryId);
                    return new PlayoffSeriesProgressDto(series.PlayoffSeriesId, series.Code, series.Name, series.Status,
                        Team(series.Team1Entry), Team(series.Team2Entry), series.Team1InitialWins, series.Team2InitialWins,
                        real1, real2, series.Team1InitialWins + real1, series.Team2InitialWins + real2,
                        series.WinsRequired, series.WinnerTeamEntryId, Count(seriesMatches));
                }).ToArray());
        }).ToArray());

    private static MatchProgressDto Count(IEnumerable<Match> source)
    {
        var matches = source.ToArray();
        return new(matches.Length,
            matches.Count(x => x.Status is MatchStatus.Pending or MatchStatus.Scheduled),
            matches.Count(x => x.Status is MatchStatus.InProgress or MatchStatus.Suspended),
            matches.Count(x => x.Status == MatchStatus.Finished),
            matches.Count(x => x.Status == MatchStatus.Cancelled));
    }
    private static TeamEntrySummaryDto? Team(LigaVolley.Domain.TeamEntries.TeamEntry? entry) => entry is null ? null : new(entry.TeamEntryId, entry.TeamId, entry.Team.Name, entry.Status);
    private async Task<Competition> Required(int id, bool tracking, CancellationToken ct) =>
        await competitions.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Competition", id);
    private static MovementSourceDto ToSource(MovementSourceType type, CompetitionPhase phase, CompetitionPhaseGroup? group, CompetitionPlayoffSeries? series) =>
        new(type, phase.CompetitionPhaseId, phase.Code, phase.Name, group?.PhaseGroupId, group?.Code, group?.Name,
            series?.PlayoffSeriesId, series?.Code, series?.Name);
    private static ResourceConflictException InvalidMovement(string message) => Conflict("competition_completion_invalid_movement_configuration", message);
    private static ResourceConflictException Conflict(string code, string message) => new(code, message);
    private sealed record Evaluation(IReadOnlyList<CompetitionCompletionBlockerDto> Blockers, IReadOnlyList<MovementResultDto> Movements);
    private sealed record MovementEvaluation(IReadOnlyList<MovementResultDto> Results, IReadOnlyList<CompetitionCompletionBlockerDto> Blockers);
    private sealed record MovementScope(CompetitionPhaseGroup? Group, CompetitionPlayoffSeries? Series);
    private sealed record MovementCandidate(int TeamEntryId, int TeamId, string TeamName, int SourcePosition, int? StandingPosition);
}
