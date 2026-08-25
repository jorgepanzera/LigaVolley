using LigaVolley.Application.Standings;
using LigaVolley.Domain.Competitions;

namespace LigaVolley.Application.PhaseCompletion;

public sealed record PhaseCompletionBlockerDto(string Code,string Message,IReadOnlyList<int>? MatchIds,IReadOnlyList<int>? TeamEntryIds,int? QualificationRuleId);
public sealed record QualificationPreviewDto(int QualificationRuleId,int TeamEntryId,string TeamName,int SourcePosition,string TargetPhaseCode,string? TargetGroupCode,string? TargetSeriesCode,byte? TargetSide);
public sealed record QualifiedTeamDto(int QualificationRuleId,int TeamEntryId,string TeamName,int SourcePosition,string TargetPhaseCode,string? TargetGroupCode,string? TargetSeriesCode,byte? TargetSide);
public sealed record GeneratedFixtureSummaryDto(int PhaseId,int? PhaseGroupId,int? SeriesId,int MatchesCreated);
public sealed record ResolvedSeriesDto(int SeriesId,string SeriesCode,int? Team1EntryId,int? Team2EntryId,PlayoffSeriesStatus Status);
public sealed record PhaseCompletionPreviewDto(int CompetitionId,int PhaseId,string PhaseCode,string PhaseName,bool CanComplete,IReadOnlyList<PhaseCompletionBlockerDto> Blockers,IReadOnlyList<StandingsDto> Standings,IReadOnlyList<QualificationPreviewDto> Qualifications,IReadOnlyList<GeneratedFixtureSummaryDto> GeneratedFixtures,IReadOnlyList<ResolvedSeriesDto> ResolvedSeries);
public sealed record PhaseCompletionResultDto(int CompetitionId,int PhaseId,CompetitionPhaseStatus Status,bool AlreadyCompleted,IReadOnlyList<StandingsDto> Standings,IReadOnlyList<QualifiedTeamDto> Qualifications,IReadOnlyList<GeneratedFixtureSummaryDto> GeneratedFixtures,IReadOnlyList<ResolvedSeriesDto> ResolvedSeries);
