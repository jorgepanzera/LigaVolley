using LigaVolley.Domain.Common;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Domain.Competitions;

public enum CompetitionPeriodType { Annual, Opening, Closing, Other }
public enum CompetitionStatus { Draft, Scheduled, InProgress, Finished, Cancelled }
public enum CompetitionPhaseStatus { Pending, InProgress, Finished, Cancelled }
public enum PlayoffSeriesStatus { Pending, Ready, InProgress, Finished, Cancelled }

public sealed class Competition
{
    private Competition() { }

    public Competition(string name, Season season, Division division, CompetitionFormat format,
        CompetitionPeriodType periodType, DateOnly? startDate, DateOnly? endDate)
    {
        Season = season ?? throw new DomainValidationException("Season is required.");
        Division = division ?? throw new DomainValidationException("Division is required.");
        CompetitionFormat = format ?? throw new DomainValidationException("CompetitionFormat is required.");
        Update(name, periodType, startDate, endDate);
        Status = CompetitionStatus.Draft;
        InstantiateStructure(format);
    }

    public int CompetitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SeasonId { get; private set; }
    public Season Season { get; private set; } = null!;
    public int DivisionId { get; private set; }
    public Division Division { get; private set; } = null!;
    public int CompetitionFormatId { get; private set; }
    public CompetitionFormat CompetitionFormat { get; private set; } = null!;
    public CompetitionPeriodType PeriodType { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public CompetitionStatus Status { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public List<CompetitionPhase> Phases { get; private set; } = [];

    public void Update(string name, CompetitionPeriodType periodType, DateOnly? startDate, DateOnly? endDate)
    {
        if (Status != CompetitionStatus.Draft)
            throw new DomainValidationException("Competition metadata can only be changed while it is in Draft status.");
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new DomainValidationException("Name is required.");
        if (normalized.Length > 150) throw new DomainValidationException("Name cannot exceed 150 characters.");
        if (!Enum.IsDefined(periodType)) throw new DomainValidationException("PeriodType is invalid.");
        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
            throw new DomainValidationException("EndDate cannot be earlier than StartDate.");
        Name = normalized; PeriodType = periodType; StartDate = startDate; EndDate = endDate;
    }

    public void ChangeStatus(CompetitionStatus target)
    {
        if (target == CompetitionStatus.Finished)
            throw new DomainValidationException("Competition can only be set to Finished by CompleteCompetition.");
        if (target == Status) return;
        if (target == CompetitionStatus.Cancelled && Status is CompetitionStatus.Draft or CompetitionStatus.Scheduled)
        { Status = target; return; }
        throw new DomainValidationException($"Transition from {Status} to {target} is not available without the fixture/progression use cases.");
    }

    public void Schedule(DateTimeOffset scheduledAt)
    {
        if (Status != CompetitionStatus.Draft)
            throw new DomainValidationException("Only a Draft competition can be scheduled.");
        Status = CompetitionStatus.Scheduled;
        ScheduledAt = scheduledAt;
    }

    // Legacy test/seed setup only; production scheduling is performed by CompetitionSchedulingService.
    public void MarkScheduledAfterInitialFixture() => Schedule(DateTimeOffset.UtcNow);
    public void MarkInProgressAfterMatchStart()
    {
        if (Status == CompetitionStatus.InProgress) return;
        if (Status != CompetitionStatus.Scheduled)
            throw new DomainValidationException("Only a Scheduled competition can start with its first match.");
        Status = CompetitionStatus.InProgress;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (Status == CompetitionStatus.Finished) return;
        if (Status != CompetitionStatus.InProgress)
            throw new DomainValidationException("Only an InProgress competition can be completed.");
        Status = CompetitionStatus.Finished;
        CompletedAt = completedAt;
    }

    private void InstantiateStructure(CompetitionFormat format)
    {
        var phaseMap = format.Phases.ToDictionary(x => x, x => new CompetitionPhase(x));
        Phases.AddRange(format.Phases.OrderBy(x => x.Sequence).Select(x => phaseMap[x]));
        var seriesMap = new Dictionary<FormatPlayoffSeries, CompetitionPlayoffSeries>();
        foreach (var formatPhase in format.Phases)
        {
            var phase = phaseMap[formatPhase];
            phase.Groups.AddRange(formatPhase.Groups.OrderBy(x => x.Sequence).Select(x => new CompetitionPhaseGroup(x)));
            foreach (var source in formatPhase.Series.OrderBy(x => x.Sequence))
            { var series = new CompetitionPlayoffSeries(source); phase.Series.Add(series); seriesMap[source] = series; }
        }
        foreach (var formatSeries in seriesMap.Keys)
            foreach (var source in formatSeries.ParticipantSources)
                seriesMap[formatSeries].ParticipantSources.Add(new CompetitionSeriesParticipantSource(source.TargetSide, source.SourceType, seriesMap[source.SourceSeries]));
    }
}

public sealed class CompetitionPhase
{
    private CompetitionPhase() { }
    internal CompetitionPhase(FormatPhase source) { FormatPhase = source; Code = source.Code; Name = source.Name; PhaseType = source.PhaseType; PhaseRole = source.PhaseRole; Sequence = source.Sequence; Rounds = source.Rounds; FixtureMode = source.FixtureMode; }
    public int CompetitionPhaseId { get; private set; }
    public int CompetitionId { get; private set; }
    public int? FormatPhaseId { get; private set; }
    public FormatPhase? FormatPhase { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PhaseType PhaseType { get; private set; }
    public PhaseRole PhaseRole { get; private set; }
    public short Sequence { get; private set; }
    public short? Rounds { get; private set; }
    public FixtureMode? FixtureMode { get; private set; }
    public CompetitionPhaseStatus Status { get; private set; } = CompetitionPhaseStatus.Pending;
    public List<CompetitionPhaseGroup> Groups { get; private set; } = [];
    public List<CompetitionPlayoffSeries> Series { get; private set; } = [];
    public void MarkInProgress()
    {
        if (Status != CompetitionPhaseStatus.Pending) throw new DomainValidationException("Only a pending phase can start.");
        Status = CompetitionPhaseStatus.InProgress;
    }
    public void Complete()
    {
        if (Status != CompetitionPhaseStatus.InProgress) throw new DomainValidationException("Only an in-progress phase can be completed.");
        Status = CompetitionPhaseStatus.Finished;
    }
    public void FinishPlayoff()
    {
        if (PhaseType != PhaseType.Playoff) throw new DomainValidationException("Only a playoff phase can be finished by series progression.");
        if (Status == CompetitionPhaseStatus.Cancelled) throw new DomainValidationException("A cancelled phase cannot finish automatically.");
        Status = CompetitionPhaseStatus.Finished;
    }
}

public sealed class CompetitionPhaseGroup
{
    private CompetitionPhaseGroup() { }
    internal CompetitionPhaseGroup(FormatGroup source) { FormatGroup = source; Code = source.Code; Name = source.Name; GroupRole = source.GroupRole; Sequence = source.Sequence; Rounds = source.Rounds; FixtureMode = source.FixtureMode; CarryOverMode = source.CarryOverMode; }
    public int PhaseGroupId { get; private set; }
    public int CompetitionPhaseId { get; private set; }
    public int? FormatGroupId { get; private set; }
    public FormatGroup? FormatGroup { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public GroupRole GroupRole { get; private set; }
    public short Sequence { get; private set; }
    public short Rounds { get; private set; }
    public FixtureMode FixtureMode { get; private set; }
    public CarryOverMode CarryOverMode { get; private set; }
    public List<PhaseGroupEntry> Entries { get; private set; } = [];
}

public sealed class PhaseGroupEntry
{
    private PhaseGroupEntry() { }
    public PhaseGroupEntry(CompetitionPhaseGroup group, TeamEntry teamEntry, short? sourcePosition, short? seed)
    {
        if (sourcePosition <= 0) throw new DomainValidationException("SourcePosition must be positive when provided.");
        if (seed <= 0) throw new DomainValidationException("Seed must be positive when provided.");
        PhaseGroup = group ?? throw new DomainValidationException("PhaseGroup is required.");
        TeamEntry = teamEntry ?? throw new DomainValidationException("TeamEntry is required.");
        CompetitionId = teamEntry.CompetitionId; SourcePosition = sourcePosition; Seed = seed;
    }
    public int PhaseGroupEntryId { get; private set; }
    public int CompetitionId { get; private set; }
    public int PhaseGroupId { get; private set; }
    public CompetitionPhaseGroup PhaseGroup { get; private set; } = null!;
    public int TeamEntryId { get; private set; }
    public TeamEntry TeamEntry { get; private set; } = null!;
    public short? SourcePosition { get; private set; }
    public short? Seed { get; private set; }
}

public sealed class CompetitionPlayoffSeries
{
    private CompetitionPlayoffSeries() { }
    internal CompetitionPlayoffSeries(FormatPlayoffSeries source) { FormatSeries = source; Code = source.Code; Name = source.Name; Sequence = source.Sequence; WinsRequired = source.WinsRequired; Team1InitialWins = source.Team1InitialWins; Team2InitialWins = source.Team2InitialWins; }
    public int PlayoffSeriesId { get; private set; }
    public int CompetitionId { get; private set; }
    public int CompetitionPhaseId { get; private set; }
    public int? FormatSeriesId { get; private set; }
    public FormatPlayoffSeries? FormatSeries { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public short Sequence { get; private set; }
    public short WinsRequired { get; private set; }
    public short Team1InitialWins { get; private set; }
    public short Team2InitialWins { get; private set; }
    public int? Team1EntryId { get; private set; }
    public TeamEntry? Team1Entry { get; private set; }
    public int? Team2EntryId { get; private set; }
    public TeamEntry? Team2Entry { get; private set; }
    public int? WinnerTeamEntryId { get; private set; }
    public TeamEntry? WinnerTeamEntry { get; private set; }
    public PlayoffSeriesStatus Status { get; private set; } = PlayoffSeriesStatus.Pending;
    public List<CompetitionSeriesParticipantSource> ParticipantSources { get; private set; } = [];
    public void AssignParticipant(byte side, TeamEntry entry)
    {
        if (side is not 1 and not 2) throw new DomainValidationException("Series participant side must be 1 or 2.");
        var current = side == 1 ? Team1EntryId : Team2EntryId;
        if (current.HasValue && current != entry.TeamEntryId) throw new DomainValidationException("Series participant side is already occupied.");
        var other = side == 1 ? Team2EntryId : Team1EntryId;
        if (other == entry.TeamEntryId) throw new DomainValidationException("A series cannot contain the same team on both sides.");
        if (side == 1) { Team1Entry = entry; Team1EntryId = entry.TeamEntryId; }
        else { Team2Entry = entry; Team2EntryId = entry.TeamEntryId; }
        if (Team1EntryId.HasValue && Team2EntryId.HasValue && Status == PlayoffSeriesStatus.Pending) Status = PlayoffSeriesStatus.Ready;
    }
    public void MarkInProgress()
    {
        if (Status == PlayoffSeriesStatus.InProgress) return;
        if (Status != PlayoffSeriesStatus.Ready) throw new DomainValidationException("Only a ready playoff series can start.");
        Status = PlayoffSeriesStatus.InProgress;
    }
    public void Finish(TeamEntry winner)
    {
        if (Status == PlayoffSeriesStatus.Cancelled) throw new DomainValidationException("A cancelled playoff series cannot finish automatically.");
        if (winner.TeamEntryId != Team1EntryId && winner.TeamEntryId != Team2EntryId)
            throw new DomainValidationException("Series winner must be one of its participants.");
        if (WinnerTeamEntryId.HasValue && WinnerTeamEntryId != winner.TeamEntryId)
            throw new DomainValidationException("Series already has a different winner.");
        WinnerTeamEntry = winner;
        WinnerTeamEntryId = winner.TeamEntryId;
        Status = PlayoffSeriesStatus.Finished;
    }
}

public sealed class CompetitionSeriesParticipantSource
{
    private CompetitionSeriesParticipantSource() { }
    internal CompetitionSeriesParticipantSource(byte side, SeriesParticipantSourceType type, CompetitionPlayoffSeries source) { TargetSide = side; SourceType = type; SourceSeries = source; }
    public int SeriesParticipantSourceId { get; private set; }
    public int TargetPlayoffSeriesId { get; private set; }
    public byte TargetSide { get; private set; }
    public SeriesParticipantSourceType SourceType { get; private set; }
    public int SourcePlayoffSeriesId { get; private set; }
    public CompetitionPlayoffSeries SourceSeries { get; private set; } = null!;
}
