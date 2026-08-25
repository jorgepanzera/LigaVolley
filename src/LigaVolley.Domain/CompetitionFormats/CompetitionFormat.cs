using LigaVolley.Domain.Common;

namespace LigaVolley.Domain.CompetitionFormats;

public enum PhaseType { RoundRobin, GroupStage, Playoff }
public enum PhaseRole { Regular, Championship, Relegation, Semifinal, ThirdPlace, Final }
public enum FixtureMode { MirroredHomeAway, BalancedRandom, Playoff }
public enum GroupRole { Championship, Relegation, Other }
public enum CarryOverMode { None, All, QualifiedOnly }
public enum QualificationSelectionMode { PositionRange, TopHalf, BottomHalf }
public enum QualificationTargetType { Group, Series }
public enum SeriesParticipantSourceType { SeriesWinner, SeriesLoser }
public enum TiebreakCriterion { TablePoints, MatchWins, SetRatio, PointRatio, HeadToHead }
public enum SortDirection { Asc, Desc }
public enum MovementType { Promotion, Relegation }
public enum MovementSourceType { PhasePosition, GroupPosition, SeriesResult, PhaseLastN, GroupLastN }

public sealed class CompetitionFormat
{
    private CompetitionFormat() { }

    public CompetitionFormat(string code, string name, string? description, short minTeams, short maxTeams)
    {
        UpdateMetadata(code, name, description, minTeams, maxTeams);
        Active = true;
    }

    public int CompetitionFormatId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public short MinTeams { get; private set; }
    public short MaxTeams { get; private set; }
    public bool Active { get; private set; }
    public List<FormatPhase> Phases { get; private set; } = [];
    public List<FormatQualificationRule> QualificationRules { get; private set; } = [];
    public List<FormatScoringRule> ScoringRules { get; private set; } = [];
    public List<FormatTiebreakRule> TiebreakRules { get; private set; } = [];
    public List<FormatMovementRule> MovementRules { get; private set; } = [];

    public void UpdateMetadata(string code, string name, string? description, short minTeams, short maxTeams)
    {
        Code = Required(code, 30, nameof(Code));
        Name = Required(name, 150, nameof(Name));
        Description = Optional(description, 500, nameof(Description));
        if (minTeams <= 1 || maxTeams < minTeams)
            throw new DomainValidationException("MinTeams must be greater than one and MaxTeams cannot be lower than MinTeams.");
        MinTeams = minTeams;
        MaxTeams = maxTeams;
    }

    public void SetActive(bool active) => Active = active;
    public void ReplaceWith(CompetitionFormat replacement)
    {
        UpdateMetadata(replacement.Code, replacement.Name, replacement.Description, replacement.MinTeams, replacement.MaxTeams);
        Phases = replacement.Phases;
        QualificationRules = replacement.QualificationRules;
        ScoringRules = replacement.ScoringRules;
        TiebreakRules = replacement.TiebreakRules;
        MovementRules = replacement.MovementRules;
    }

    private static string Required(string value, int max, string field)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length == 0) throw new DomainValidationException($"{field} is required.");
        if (result.Length > max) throw new DomainValidationException($"{field} cannot exceed {max} characters.");
        return result;
    }

    private static string? Optional(string? value, int max, string field)
    {
        var result = value?.Trim();
        if (result?.Length > max) throw new DomainValidationException($"{field} cannot exceed {max} characters.");
        return string.IsNullOrEmpty(result) ? null : result;
    }
}

public sealed class FormatPhase
{
    private FormatPhase() { }
    public FormatPhase(string code, string name, PhaseType type, PhaseRole role, short sequence, short? rounds, FixtureMode? fixtureMode)
    { Code = code.Trim(); Name = name.Trim(); PhaseType = type; PhaseRole = role; Sequence = sequence; Rounds = rounds; FixtureMode = fixtureMode; Active = true; }
    public int FormatPhaseId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PhaseType PhaseType { get; private set; }
    public PhaseRole PhaseRole { get; private set; }
    public short Sequence { get; private set; }
    public short? Rounds { get; private set; }
    public FixtureMode? FixtureMode { get; private set; }
    internal bool Active { get; private set; }
    public List<FormatGroup> Groups { get; private set; } = [];
    public List<FormatPlayoffSeries> Series { get; private set; } = [];
}

public sealed class FormatGroup
{
    private FormatGroup() { }
    public FormatGroup(string code, string name, GroupRole role, short sequence, short rounds, FixtureMode fixtureMode, CarryOverMode carryOverMode)
    { Code = code.Trim(); Name = name.Trim(); GroupRole = role; Sequence = sequence; Rounds = rounds; FixtureMode = fixtureMode; CarryOverMode = carryOverMode; Active = true; }
    public int FormatGroupId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public int FormatPhaseId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public GroupRole GroupRole { get; private set; }
    public short Sequence { get; private set; }
    public short Rounds { get; private set; }
    public FixtureMode FixtureMode { get; private set; }
    public CarryOverMode CarryOverMode { get; private set; }
    internal bool Active { get; private set; }
}

public sealed class FormatPlayoffSeries
{
    private FormatPlayoffSeries() { }
    public FormatPlayoffSeries(string code, string name, short sequence, short winsRequired, short team1InitialWins, short team2InitialWins)
    {
        if (sequence <= 0 || winsRequired <= 0 || team1InitialWins < 0 || team2InitialWins < 0 ||
            team1InitialWins >= winsRequired || team2InitialWins >= winsRequired)
            throw new DomainValidationException("Playoff series sequence and wins configuration is invalid.");
        Code = code.Trim(); Name = name.Trim(); Sequence = sequence; WinsRequired = winsRequired;
        Team1InitialWins = team1InitialWins; Team2InitialWins = team2InitialWins; Active = true;
    }
    public int FormatSeriesId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public int FormatPhaseId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public short Sequence { get; private set; }
    public short WinsRequired { get; private set; }
    public short Team1InitialWins { get; private set; }
    public short Team2InitialWins { get; private set; }
    internal bool Active { get; private set; }
    public List<FormatSeriesParticipantSource> ParticipantSources { get; private set; } = [];
}

public sealed class FormatSeriesParticipantSource
{
    private FormatSeriesParticipantSource() { }
    public FormatSeriesParticipantSource(byte targetSide, SeriesParticipantSourceType sourceType, FormatPlayoffSeries sourceSeries)
    { TargetSide = targetSide; SourceType = sourceType; SourceSeries = sourceSeries; }
    public int FormatSeriesParticipantSourceId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public int TargetFormatSeriesId { get; private set; }
    public byte TargetSide { get; private set; }
    public SeriesParticipantSourceType SourceType { get; private set; }
    public int SourceFormatSeriesId { get; private set; }
    public FormatPlayoffSeries SourceSeries { get; private set; } = null!;
}

public sealed class FormatQualificationRule
{
    private FormatQualificationRule() { }
    public FormatQualificationRule(FormatPhase sourcePhase, FormatGroup? sourceGroup, QualificationSelectionMode selectionMode, short? fromPosition, short? toPosition, QualificationTargetType targetType, FormatPhase targetPhase, FormatGroup? targetGroup, FormatPlayoffSeries? targetSeries, byte? targetSide, short sequence)
    { SourcePhase = sourcePhase; SourceGroup = sourceGroup; SelectionMode = selectionMode; FromPosition = fromPosition; ToPosition = toPosition; TargetType = targetType; TargetPhase = targetPhase; TargetGroup = targetGroup; TargetSeries = targetSeries; TargetSide = targetSide; Sequence = sequence; }
    public int QualificationRuleId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public int SourceFormatPhaseId { get; private set; }
    public int? SourceFormatGroupId { get; private set; }
    public QualificationSelectionMode SelectionMode { get; private set; }
    public short? FromPosition { get; private set; }
    public short? ToPosition { get; private set; }
    public QualificationTargetType TargetType { get; private set; }
    public int? TargetFormatPhaseId { get; private set; }
    public int? TargetFormatGroupId { get; private set; }
    public int? TargetFormatSeriesId { get; private set; }
    public byte? TargetSide { get; private set; }
    public short Sequence { get; private set; }
    public FormatPhase SourcePhase { get; private set; } = null!;
    public FormatGroup? SourceGroup { get; private set; }
    public FormatPhase TargetPhase { get; private set; } = null!;
    public FormatGroup? TargetGroup { get; private set; }
    public FormatPlayoffSeries? TargetSeries { get; private set; }
}

public sealed class FormatScoringRule(byte winnerSets, byte loserSets, short winnerTablePoints, short loserTablePoints)
{
    public int FormatScoringRuleId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public byte WinnerSets { get; private set; } = winnerSets;
    public byte LoserSets { get; private set; } = loserSets;
    public short WinnerTablePoints { get; private set; } = winnerTablePoints;
    public short LoserTablePoints { get; private set; } = loserTablePoints;
}

public sealed class FormatTiebreakRule(short sequence, TiebreakCriterion criterion, SortDirection sortDirection)
{
    public int FormatTiebreakRuleId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public short Sequence { get; private set; } = sequence;
    public TiebreakCriterion Criterion { get; private set; } = criterion;
    public SortDirection SortDirection { get; private set; } = sortDirection;
}

public sealed class FormatMovementRule
{
    private FormatMovementRule() { }
    public FormatMovementRule(MovementType movementType, MovementSourceType sourceType, FormatPhase sourcePhase, FormatGroup? sourceGroup, FormatPlayoffSeries? sourceSeries, short fromPosition, short toPosition, short targetLevelDelta, bool appliesIfTargetExists)
    { MovementType = movementType; SourceType = sourceType; SourcePhase = sourcePhase; SourceGroup = sourceGroup; SourceSeries = sourceSeries; FromPosition = fromPosition; ToPosition = toPosition; TargetLevelDelta = targetLevelDelta; AppliesIfTargetExists = appliesIfTargetExists; }
    public int FormatMovementRuleId { get; private set; }
    public int CompetitionFormatId { get; private set; }
    public MovementType MovementType { get; private set; }
    public MovementSourceType SourceType { get; private set; }
    public int? SourceFormatPhaseId { get; private set; }
    public int? SourceFormatGroupId { get; private set; }
    public int? SourceFormatSeriesId { get; private set; }
    public short FromPosition { get; private set; }
    public short ToPosition { get; private set; }
    public short TargetLevelDelta { get; private set; }
    public bool AppliesIfTargetExists { get; private set; }
    public FormatPhase SourcePhase { get; private set; } = null!;
    public FormatGroup? SourceGroup { get; private set; }
    public FormatPlayoffSeries? SourceSeries { get; private set; }
}
