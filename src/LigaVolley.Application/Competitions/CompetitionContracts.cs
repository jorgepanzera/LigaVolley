using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Seasons;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Application.Competitions;

public enum CompetitionStructureSourceType { Format, Competition }
public sealed record CompetitionStructureSourceDto(CompetitionStructureSourceType Type, int? CompetitionFormatId, int? SourceCompetitionId);
public sealed record CreateCompetitionRequest(string Name, int SeasonId, int DivisionId, CompetitionPeriodType PeriodType, DateOnly? StartDate, DateOnly? EndDate, CompetitionStructureSourceDto StructureSource);
public sealed record UpdateCompetitionRequest(string Name, CompetitionPeriodType PeriodType, DateOnly? StartDate, DateOnly? EndDate);
public sealed record ChangeCompetitionStatusRequest(CompetitionStatus Status);
public sealed record CompetitionDto(int CompetitionId, string Name, SeasonSummaryDto Season, DivisionSummaryDto Division, CompetitionFormatSummaryDto Format, CompetitionPeriodType PeriodType, DateOnly? StartDate, DateOnly? EndDate, CompetitionStatus Status);
public sealed record CompetitionSummaryDto(int CompetitionId, string Name, short SeasonYear, string DivisionName, Gender Gender, string FormatName, CompetitionPeriodType PeriodType, CompetitionStatus Status);
public sealed record CompetitionStructureDto(int CompetitionId, IReadOnlyList<CompetitionPhaseDto> Phases);
public sealed record CompetitionPhaseDto(int PhaseId, string Code, string Name, PhaseType PhaseType, PhaseRole PhaseRole, short Sequence, short? Rounds, FixtureMode? FixtureMode, CompetitionPhaseStatus Status, IReadOnlyList<CompetitionPhaseGroupDto> Groups, IReadOnlyList<CompetitionPlayoffSeriesDto> Series);
public sealed record CompetitionPhaseGroupDto(int PhaseGroupId, string Code, string Name, GroupRole GroupRole, short Sequence, short Rounds, FixtureMode FixtureMode, CarryOverMode CarryOverMode);
public sealed record CompetitionPlayoffSeriesDto(int PlayoffSeriesId, string Code, string Name, short Sequence, short WinsRequired, short Team1InitialWins, short Team2InitialWins, PlayoffSeriesStatus Status, IReadOnlyList<CompetitionSeriesParticipantSourceDto> ParticipantSources);
public sealed record CompetitionSeriesParticipantSourceDto(int SeriesParticipantSourceId, byte TargetSide, SeriesParticipantSourceType SourceType, int SourcePlayoffSeriesId, string SourceSeriesCode);
