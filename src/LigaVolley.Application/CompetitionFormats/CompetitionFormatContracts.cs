using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.CompetitionFormats;

public sealed record CreateCompetitionFormatRequest(string Code, string Name, string? Description, short MinTeams, short MaxTeams, CompetitionFormatDefinitionDto Definition);
public sealed record UpdateCompetitionFormatRequest(string Code, string Name, string? Description, short MinTeams, short MaxTeams, CompetitionFormatDefinitionDto Definition);
public sealed record CloneCompetitionFormatRequest(string Code, string Name, string? Description);
public sealed record ValidateCompetitionFormatRequest(short MinTeams, short MaxTeams, CompetitionFormatDefinitionDto Definition, string? Code = null, string? Name = null);
public sealed record CompetitionFormatDefinitionDto(IReadOnlyList<FormatPhaseInputDto> Phases, IReadOnlyList<FormatQualificationRuleInputDto> QualificationRules, IReadOnlyList<FormatScoringRuleInputDto> ScoringRules, IReadOnlyList<FormatTiebreakRuleInputDto> TiebreakRules, IReadOnlyList<FormatMovementRuleInputDto> MovementRules);
public sealed record FormatPhaseInputDto(string Code, string Name, PhaseType PhaseType, PhaseRole PhaseRole, short Sequence, short? Rounds, FixtureMode? FixtureMode, IReadOnlyList<FormatGroupInputDto> Groups, IReadOnlyList<FormatPlayoffSeriesInputDto> Series);
public sealed record FormatGroupInputDto(string Code, string Name, GroupRole GroupRole, short Sequence, short Rounds, FixtureMode FixtureMode, CarryOverMode CarryOverMode);
public sealed record FormatPlayoffSeriesInputDto(string Code, string Name, short Sequence, short WinsRequired, short Team1InitialWins, short Team2InitialWins, IReadOnlyList<SeriesParticipantSourceInputDto> ParticipantSources);
public sealed record SeriesParticipantSourceInputDto(byte TargetSide, SeriesParticipantSourceType SourceType, string SourceSeriesCode);
public sealed record FormatQualificationRuleInputDto(string SourcePhaseCode, string? SourceGroupCode, QualificationSelectionMode SelectionMode, short? FromPosition, short? ToPosition, QualificationTargetType TargetType, string TargetPhaseCode, string? TargetGroupCode, string? TargetSeriesCode, byte? TargetSide, short Sequence);
public sealed record FormatScoringRuleInputDto(byte WinnerSets, byte LoserSets, short WinnerTablePoints, short LoserTablePoints);
public sealed record FormatTiebreakRuleInputDto(short Sequence, TiebreakCriterion Criterion, SortDirection SortDirection);
public sealed record FormatMovementRuleInputDto(MovementType MovementType, MovementSourceType SourceType, string? SourcePhaseCode, string? SourceGroupCode, string? SourceSeriesCode, short FromPosition, short ToPosition, short TargetLevelDelta, bool AppliesIfTargetExists);

public enum ValidationSeverity { Error, Warning }
public sealed record CompetitionFormatValidationErrorDto(string Code, string Path, string Message, ValidationSeverity Severity = ValidationSeverity.Error);
public sealed record CompetitionFormatTeamCountValidationDto(short TeamCount, bool IsValid, int ErrorCount, IReadOnlyList<CompetitionFormatValidationErrorDto> Issues);
public sealed record CompetitionFormatValidationDto(bool IsValid, IReadOnlyList<CompetitionFormatValidationErrorDto> Errors, IReadOnlyList<CompetitionFormatValidationErrorDto> Warnings, IReadOnlyList<CompetitionFormatTeamCountValidationDto> TeamCounts);
public sealed record CompetitionFormatSummaryDto(int CompetitionFormatId, string Code, string Name, short MinTeams, short MaxTeams, bool Active, bool Used = false, bool IsStructurallyLocked = false, int UsedByDraftCompetitionCount = 0, int UsedByOperationalCompetitionCount = 0);
public sealed record CompetitionFormatDto(int CompetitionFormatId, string Code, string Name, string? Description, short MinTeams, short MaxTeams, bool Active, CompetitionFormatDefinitionDto Definition, bool Used = false, bool IsStructurallyLocked = false, int UsedByDraftCompetitionCount = 0, int UsedByOperationalCompetitionCount = 0);
