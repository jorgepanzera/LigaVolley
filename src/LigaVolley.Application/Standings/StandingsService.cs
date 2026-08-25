using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.Standings;

namespace LigaVolley.Application.Standings;

public sealed class StandingsService(ICompetitionRepository competitions, IStandingsRepository repository, StandingsCalculator calculator)
{
    public async Task<StandingsDto> GetAsync(int competitionId, int phaseId, int? phaseGroupId, CancellationToken ct)
    {
        var competition = await competitions.GetAsync(competitionId, false, ct)
            ?? throw new ResourceNotFoundException("Competition", competitionId);
        var phase = competition.Phases.SingleOrDefault(x => x.CompetitionPhaseId == phaseId)
            ?? throw new ResourceNotFoundException("CompetitionPhase", phaseId);
        if (phase.PhaseType == PhaseType.Playoff)
            throw new RequestValidationException("standings_not_supported_for_phase", "Standings are not supported for playoff phases.");

        var hasGroups = phase.Groups.Count > 0;
        if (hasGroups && phaseGroupId is null)
            throw new RequestValidationException("standings_group_required", "PhaseGroupId is required for a phase with groups.");
        if (!hasGroups && phaseGroupId is not null)
            throw new RequestValidationException("standings_group_not_allowed", "PhaseGroupId is not allowed for a phase without groups.");

        var group = phaseGroupId is null ? null : phase.Groups.SingleOrDefault(x => x.PhaseGroupId == phaseGroupId);
        if (phaseGroupId is not null && group is null)
        {
            if (await repository.PhaseGroupExistsAsync(phaseGroupId.Value, ct))
                throw new RequestValidationException("standings_group_not_in_phase", "The phase group does not belong to the requested phase.");
            throw new ResourceNotFoundException("PhaseGroup", phaseGroupId.Value);
        }

        var entries = group is null
            ? await repository.ListPhaseParticipantsAsync(competitionId, ct)
            : await repository.ListGroupParticipantsAsync(competitionId, group.PhaseGroupId, ct);
        var matches = await repository.ListScopeMatchesAsync(competitionId, phaseId, group?.PhaseGroupId, ct);
        var finished = matches.Where(x => x.Status == MatchStatus.Finished).ToArray();

        try
        {
            var positions = calculator.Calculate(
                entries.Select(x => new StandingsTeam(x.TeamEntryId, x.TeamId, x.Team.Name)).ToArray(),
                finished.Select(x => new StandingsMatch(x.MatchId, x.HomeTeamEntryId ?? 0, x.AwayTeamEntryId ?? 0, x.HomeSets, x.AwaySets, x.WinnerTeamEntryId,
                    x.Sets.Select(s => new StandingsSet(s.SetNumber, s.HomePoints, s.AwayPoints)).ToArray())).ToArray(),
                competition.CompetitionFormat.ScoringRules.Select(x => new StandingsScoringRule(x.WinnerSets, x.LoserSets, x.WinnerTablePoints, x.LoserTablePoints)).ToArray(),
                competition.CompetitionFormat.TiebreakRules.Select(x => new StandingsTiebreakRule(x.Sequence, x.Criterion, x.SortDirection)).ToArray());
            return new(competitionId, phaseId, phase.Code, phase.Name, group?.PhaseGroupId, group?.Code, group?.Name,
                matches.All(x => x.Status == MatchStatus.Finished), positions.Select(Map).ToArray());
        }
        catch (StandingsCalculationException exception)
        {
            throw new ResourceConflictException(exception.Code, exception.Message);
        }
    }

    private static StandingPositionDto Map(StandingPosition x) => new(x.Position, x.TeamEntryId, x.TeamId, x.TeamName,
        x.Played, x.Won, x.Lost, x.SetsWon, x.SetsLost, x.SetRatio, x.PointsWon, x.PointsLost, x.PointRatio, x.TablePoints, x.IsTied);
}
