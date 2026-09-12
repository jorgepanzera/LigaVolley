using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.ParticipantComposition;

public sealed class ParticipantCompositionService(
    ICompetitionRepository competitions,
    ITeamEntryRepository entries,
    ITeamRepository teams,
    ICompetitionProgressionRepository progression,
    IUnitOfWork unit)
{
    public async Task<IReadOnlyList<ParticipantSuggestionSourceDto>> ListSourcesAsync(int competitionId, CancellationToken ct)
    {
        var competition = await Required(competitionId, false, ct);
        return Sources(competition);
    }

    public async Task<IReadOnlyList<ParticipantSuggestionSourceDto>> ReplaceSourcesAsync(int competitionId, ReplaceParticipantSuggestionSourcesRequest request, CancellationToken ct)
    {
        var target = await Required(competitionId, true, ct);
        if (target.Status != CompetitionStatus.Draft)
            throw new ResourceConflictException("competition_not_draft", "Participant suggestion sources can only be changed while the competition is in Draft status.");
        var ids = request.SourceCompetitionIds?.Distinct().ToArray() ?? throw new RequestValidationException("source_competition_ids_required", "SourceCompetitionIds is required.");
        if (ids.Any(x => x <= 0)) throw new RequestValidationException("source_competition_id_invalid", "Source competition ids must be positive.");
        if (ids.Contains(competitionId)) throw new RequestValidationException("participant_suggestion_source_self", "A competition cannot be its own participant suggestion source.");
        var sources = new List<Competition>();
        foreach (var id in ids)
        {
            var source = await Required(id, true, ct);
            if (source.Status != CompetitionStatus.Finished)
                throw new ResourceConflictException("participant_suggestion_source_not_finished", "A participant suggestion source must be Finished.");
            if (source.Division.Gender != target.Division.Gender)
                throw new ResourceConflictException("participant_suggestion_source_gender_mismatch", "A participant suggestion source must have the same gender as the target competition.");
            sources.Add(source);
        }
        target.ReplaceParticipantSuggestionSources(sources);
        await unit.SaveChangesAsync(ct);
        return Sources(target);
    }

    public async Task<ParticipantCompositionDto> GetAsync(int competitionId, CancellationToken ct)
    {
        var target = await Required(competitionId, false, ct);
        var configured = target.ParticipantSuggestionSources.OrderBy(x => x.SourceCompetition.Name).ToArray();
        var current = await entries.ListAsync(competitionId, false, ct);
        var currentIds = current.Select(x => x.TeamId).ToHashSet();
        var candidates = new Dictionary<ParticipantSuggestionKind, Dictionary<int, Candidate>>
        {
            [ParticipantSuggestionKind.Remaining] = [], [ParticipantSuggestionKind.PromotedIn] = [], [ParticipantSuggestionKind.RelegatedIn] = []
        };
        foreach (var configuredSource in configured)
        {
            var source = configuredSource.SourceCompetition;
            var sourceEntries = await entries.ListAsync(source.CompetitionId, false, ct);
            var movements = await progression.ListMovementsAsync(source.CompetitionId, ct);
            if (source.DivisionId == target.DivisionId)
            {
                var departed = movements.Where(x => x.TargetDivisionId != target.DivisionId).Select(x => x.TeamId).ToHashSet();
                foreach (var entry in sourceEntries.Where(x => !departed.Contains(x.TeamId)))
                    Add(candidates[ParticipantSuggestionKind.Remaining], entry.TeamId, entry.Team.Name, new(source.CompetitionId, source.Name, ParticipantSuggestionKind.Remaining, null, null));
            }
            foreach (var movement in movements.Where(x => x.TargetDivisionId == target.DivisionId && x.MovementType is MovementType.Promotion or MovementType.Relegation))
            {
                var kind = movement.MovementType == MovementType.Promotion ? ParticipantSuggestionKind.PromotedIn : ParticipantSuggestionKind.RelegatedIn;
                Add(candidates[kind], movement.TeamId, movement.TeamEntry.Team.Name, new(source.CompetitionId, source.Name, kind, movement.MovementRuleId, movement.SourcePosition));
            }
        }
        var promoted = candidates[ParticipantSuggestionKind.PromotedIn];
        var relegated = candidates[ParticipantSuggestionKind.RelegatedIn];
        var remaining = candidates[ParticipantSuggestionKind.Remaining];
        var allSuggested = promoted.Keys.Concat(relegated.Keys).Concat(remaining.Keys).ToHashSet();
        var promotedRows = Rows(promoted, currentIds);
        var relegatedRows = Rows(relegated.Where(x => !promoted.ContainsKey(x.Key)).ToDictionary(), currentIds);
        var remainingRows = Rows(remaining.Where(x => !promoted.ContainsKey(x.Key) && !relegated.ContainsKey(x.Key)).ToDictionary(), currentIds);
        var available = await teams.ListAsync(null, null, target.Division.Gender, true, 1, 10000, ct);
        var other = available.Items.Where(x => !currentIds.Contains(x.TeamId) && !allSuggested.Contains(x.TeamId))
            .Select(x => new ParticipantCompositionTeamDto(x.TeamId, x.Name, false, [])).ToArray();
        var warnings = new List<ParticipantCompositionWarningDto>();
        warnings.AddRange(remaining.Values.Where(x => !currentIds.Contains(x.TeamId)).Select(x => Warning("SPORTING_SUGGESTION_REMAINING_TEAM_MISSING", x)));
        warnings.AddRange(promoted.Values.Where(x => !currentIds.Contains(x.TeamId)).Select(x => Warning("SPORTING_SUGGESTION_PROMOTED_TEAM_MISSING", x)));
        warnings.AddRange(relegated.Values.Where(x => currentIds.Contains(x.TeamId)).Select(x => Warning("SPORTING_SUGGESTION_RELEGATED_TEAM_RETAINED", x)));
        warnings.AddRange(current.Where(x => !allSuggested.Contains(x.TeamId)).Select(x => new ParticipantCompositionWarningDto("SPORTING_SUGGESTION_UNEXPECTED_TEAM_ADDED", x.TeamId, x.Team.Name, [])));
        return new(target.CompetitionId, target.Status, Sources(target), remainingRows, promotedRows, relegatedRows, other, warnings.OrderBy(x => x.Code).ThenBy(x => x.TeamName).ToArray());
    }

    private async Task<Competition> Required(int id, bool tracking, CancellationToken ct) => await competitions.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Competition", id);
    private static IReadOnlyList<ParticipantSuggestionSourceDto> Sources(Competition competition) => competition.ParticipantSuggestionSources.OrderBy(x => x.SourceCompetition.Name).Select(x => new ParticipantSuggestionSourceDto(x.SourceCompetitionId, x.SourceCompetition.Name, x.SourceCompetition.Season.Year, x.SourceCompetition.Season.Name, x.SourceCompetition.DivisionId, x.SourceCompetition.Division.Name)).ToArray();
    private static void Add(IDictionary<int, Candidate> target, int teamId, string teamName, ParticipantSuggestionProvenanceDto provenance)
    { if (!target.TryGetValue(teamId, out var candidate)) target[teamId] = candidate = new(teamId, teamName); candidate.Provenance.Add(provenance); }
    private static IReadOnlyList<ParticipantCompositionTeamDto> Rows(IReadOnlyDictionary<int, Candidate> rows, IReadOnlySet<int> current) => rows.Values.OrderBy(x => x.TeamName).Select(x => new ParticipantCompositionTeamDto(x.TeamId, x.TeamName, current.Contains(x.TeamId), x.Provenance)).ToArray();
    private static ParticipantCompositionWarningDto Warning(string code, Candidate x) => new(code, x.TeamId, x.TeamName, x.Provenance);
    private sealed class Candidate(int teamId, string teamName) { public int TeamId { get; } = teamId; public string TeamName { get; } = teamName; public List<ParticipantSuggestionProvenanceDto> Provenance { get; } = []; }
}
