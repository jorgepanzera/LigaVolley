using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.CompetitionProgression;

public sealed record TeamEntrySummaryDto(int TeamEntryId, int TeamId, string TeamName, TeamEntryStatus Status);
public sealed record MatchProgressDto(int Total, int Pending, int InProgress, int Finished, int Cancelled);
public sealed record CompetitionGroupProgressDto(int PhaseGroupId, string Code, string Name, MatchProgressDto Matches);
public sealed record PlayoffSeriesProgressDto(int SeriesId, string Code, string Name, PlayoffSeriesStatus Status,
    TeamEntrySummaryDto? Team1, TeamEntrySummaryDto? Team2, short Team1InitialWins, short Team2InitialWins,
    int Team1RealWins, int Team2RealWins, int Team1Wins, int Team2Wins, short WinsRequired,
    int? WinnerTeamEntryId, MatchProgressDto Matches);
public sealed record CompetitionPhaseProgressDto(int PhaseId, string Code, string Name, short Sequence, PhaseType PhaseType,
    CompetitionPhaseStatus Status, MatchProgressDto Matches, IReadOnlyList<CompetitionGroupProgressDto> Groups,
    IReadOnlyList<PlayoffSeriesProgressDto> Series);
public sealed record CompetitionProgressionDto(int CompetitionId, string CompetitionName, CompetitionStatus Status,
    MatchProgressDto Matches, IReadOnlyList<CompetitionPhaseProgressDto> Phases);

public sealed record CompetitionCompletionBlockerDto(string Code, string Message, int? PhaseId = null,
    int? SeriesId = null, int? MatchId = null, int? MovementRuleId = null);
public enum MovementResultStatus { Applied, NotApplicable }
public enum MovementNotAppliedReason { TargetDivisionNotFound }
public sealed record MovementSourceDto(MovementSourceType Type, int PhaseId, string PhaseCode, string PhaseName,
    int? PhaseGroupId, string? GroupCode, string? GroupName, int? SeriesId, string? SeriesCode, string? SeriesName);
public sealed record MovementResultDto(int MovementRuleId, MovementType MovementType, MovementSourceDto Source,
    int TeamEntryId, int TeamId, string TeamName, int SourcePosition, int? StandingPosition,
    int SourceDivisionId, string SourceDivisionName, short SourceDivisionLevelOrder, MovementResultStatus Status,
    int? TargetDivisionId, string? TargetDivisionName, short? TargetDivisionLevelOrder,
    short TargetLevelDelta, MovementNotAppliedReason? NotAppliedReason);
public sealed record CompetitionCompletionPreviewDto(int CompetitionId, string CompetitionName, CompetitionStatus Status,
    bool AlreadyCompleted, bool CanComplete, IReadOnlyList<CompetitionCompletionBlockerDto> Blockers,
    IReadOnlyList<MovementResultDto> Movements);
public sealed record CompetitionCompletionResultDto(int CompetitionId, CompetitionStatus Status, bool AlreadyCompleted,
    DateTimeOffset? CompletedAt, IReadOnlyList<MovementResultDto> Movements);
