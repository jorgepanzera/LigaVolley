using LigaVolley.Domain.Competitions;

namespace LigaVolley.Application.ParticipantComposition;

public sealed record ReplaceParticipantSuggestionSourcesRequest(IReadOnlyList<int> SourceCompetitionIds);
public sealed record ParticipantSuggestionSourceDto(int CompetitionId, string Name, short SeasonYear, string SeasonName, int DivisionId, string DivisionName);
public sealed record ParticipantSuggestionProvenanceDto(int SourceCompetitionId, string SourceCompetitionName, ParticipantSuggestionKind Kind, int? MovementRuleId, int? SourcePosition);
public enum ParticipantSuggestionKind { Remaining, PromotedIn, RelegatedIn }
public sealed record ParticipantCompositionTeamDto(int TeamId, string TeamName, bool IsCurrentParticipant, IReadOnlyList<ParticipantSuggestionProvenanceDto> Provenance);
public sealed record ParticipantCompositionWarningDto(string Code, int TeamId, string TeamName, IReadOnlyList<ParticipantSuggestionProvenanceDto> Provenance);
public sealed record ParticipantCompositionDto(int CompetitionId, CompetitionStatus Status, IReadOnlyList<ParticipantSuggestionSourceDto> Sources,
    IReadOnlyList<ParticipantCompositionTeamDto> Remaining, IReadOnlyList<ParticipantCompositionTeamDto> PromotedIn,
    IReadOnlyList<ParticipantCompositionTeamDto> RelegatedIn, IReadOnlyList<ParticipantCompositionTeamDto> OtherEligible,
    IReadOnlyList<ParticipantCompositionWarningDto> Warnings);
