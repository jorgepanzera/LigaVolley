using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Venues;

namespace LigaVolley.Domain.Fixtures;

public enum MatchStatus { Pending, Scheduled, InProgress, Finished, Suspended, Cancelled }
public enum MatchSetStatus { Ready, InProgress, Finished }

public sealed class FixtureGeneration
{
    private FixtureGeneration() { }
    public FixtureGeneration(Competition competition, CompetitionPhase phase, CompetitionPhaseGroup? phaseGroup, int randomSeed, DateTime generatedAt)
    { Competition = competition; Phase = phase; PhaseGroup = phaseGroup; RandomSeed = randomSeed; GeneratedAt = generatedAt; }
    public int FixtureGenerationId { get; private set; }
    public int CompetitionId { get; private set; }
    public Competition Competition { get; private set; } = null!;
    public int PhaseId { get; private set; }
    public CompetitionPhase Phase { get; private set; } = null!;
    public int? PhaseGroupId { get; private set; }
    public CompetitionPhaseGroup? PhaseGroup { get; private set; }
    public int RandomSeed { get; private set; }
    public DateTime GeneratedAt { get; private set; }
}

public sealed class Match
{
    private Match() { }
    public Match(Competition competition, CompetitionPhase phase, CompetitionPhaseGroup? group,
        TeamEntry home, TeamEntry away, short roundNumber, short matchNumber)
    {
        if (ReferenceEquals(home, away)) throw new DomainValidationException("Home and away teams must be different.");
        if (roundNumber <= 0 || matchNumber <= 0) throw new DomainValidationException("RoundNumber and MatchNumber must be positive.");
        Competition = competition; Phase = phase; PhaseGroup = group; HomeTeamEntry = home; AwayTeamEntry = away;
        RoundNumber = roundNumber; MatchNumber = matchNumber; Status = MatchStatus.Pending;
    }
    public Match(Competition competition, CompetitionPhase phase, CompetitionPlayoffSeries series,
        TeamEntry home, TeamEntry away, short matchNumber)
        : this(competition, phase, null, home, away, 1, matchNumber)
    { Series = series ?? throw new DomainValidationException("Series is required."); }
    public int MatchId { get; private set; }
    public int CompetitionId { get; private set; }
    public Competition Competition { get; private set; } = null!;
    public int PhaseId { get; private set; }
    public CompetitionPhase Phase { get; private set; } = null!;
    public int? PhaseGroupId { get; private set; }
    public CompetitionPhaseGroup? PhaseGroup { get; private set; }
    public int? SeriesId { get; private set; }
    public CompetitionPlayoffSeries? Series { get; private set; }
    public int? HomeTeamEntryId { get; private set; }
    public TeamEntry? HomeTeamEntry { get; private set; }
    public int? AwayTeamEntryId { get; private set; }
    public TeamEntry? AwayTeamEntry { get; private set; }
    public DateTime? MatchDate { get; private set; }
    public int? VenueId { get; private set; }
    public Venue? Venue { get; private set; }
    public short RoundNumber { get; private set; }
    public short MatchNumber { get; private set; }
    public MatchStatus Status { get; private set; }
    public byte? HomeSets { get; private set; }
    public byte? AwaySets { get; private set; }
    public int? WinnerTeamEntryId { get; private set; }
    public List<MatchSet> Sets { get; private set; } = [];

    public void Schedule(DateTime? matchDate, int? venueId)
    {
        if (Status is not MatchStatus.Pending and not MatchStatus.Scheduled)
            throw new DomainValidationException("Only a pending or scheduled match can have its scheduling modified.");

        MatchDate = matchDate;
        VenueId = venueId;
        Status = matchDate.HasValue || venueId.HasValue ? MatchStatus.Scheduled : MatchStatus.Pending;
    }

    public void Finish(byte homeSets, byte awaySets, TeamEntry winner, IEnumerable<MatchSet> sets)
    {
        if (!ReferenceEquals(winner, HomeTeamEntry) && !ReferenceEquals(winner, AwayTeamEntry))
            throw new DomainValidationException("Winner must be one of the match participants.");
        HomeSets = homeSets; AwaySets = awaySets; WinnerTeamEntryId = winner.TeamEntryId;
        Sets = sets?.ToList() ?? throw new DomainValidationException("Match sets are required.");
        Status = MatchStatus.Finished;
    }

    public void Start()
    {
        if (Status != MatchStatus.Scheduled)
            throw new DomainValidationException("Only a Scheduled match can start.");
        Status = MatchStatus.InProgress;
    }
}

public sealed class MatchSet
{
    private MatchSet() { }
    public MatchSet(byte setNumber, short homePoints, short awayPoints)
    {
        if (setNumber is < 1 or > 5) throw new DomainValidationException("SetNumber must be between 1 and 5.");
        if (homePoints < 0 || awayPoints < 0) throw new DomainValidationException("Set points cannot be negative.");
        SetNumber = setNumber; HomePoints = homePoints; AwayPoints = awayPoints;
        Status = MatchSetStatus.Finished;
        WinnerSide = homePoints > awayPoints ? MatchSheets.MatchSide.Home : MatchSheets.MatchSide.Away;
    }
    public MatchSet(MatchSheets.MatchSheet matchSheet, byte setNumber)
    {
        if (setNumber is < 1 or > 5) throw new DomainValidationException("SetNumber must be between 1 and 5.");
        MatchSheet=matchSheet??throw new DomainValidationException("MatchSheet is required.");MatchId=matchSheet.MatchId; MatchSheetId=matchSheet.MatchSheetId; SetNumber = setNumber;
        SetUuid = Guid.NewGuid(); Status = MatchSetStatus.Ready;
    }
    public int MatchSetId { get; private set; }
    public int MatchId { get; private set; }
    public int? MatchSheetId { get; private set; }
    public MatchSheets.MatchSheet? MatchSheet { get; private set; }
    public Guid SetUuid { get; private set; }
    public byte SetNumber { get; private set; }
    public MatchSetStatus Status { get; private set; }
    public short HomePoints { get; private set; }
    public short AwayPoints { get; private set; }
    public MatchSheets.MatchSide? WinnerSide { get; private set; }
    public MatchSheets.MatchSide? InitialServingSide { get; private set; }
    public MatchSheets.MatchSide? CurrentServingSide { get; private set; }
    public byte HomeRotationOffset { get; private set; }
    public byte AwayRotationOffset { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public List<MatchSheets.MatchLineup> Lineups { get; private set; } = [];
    public List<MatchSheets.MatchSubstitution> Substitutions { get; private set; } = [];
    public List<MatchSheets.MatchLiberoReplacement> LiberoReplacements { get; private set; } = [];
    public List<MatchSheets.MatchSetLiberoPlan> LiberoPlans { get; private set; } = [];
    public List<MatchSheets.MatchTimeout> Timeouts { get; private set; } = [];

    public void Start(MatchSheets.MatchSide servingSide, DateTimeOffset now)
    {
        if (Status != MatchSetStatus.Ready) throw new DomainValidationException("Only a Ready set can start.");
        Status = MatchSetStatus.InProgress; InitialServingSide = CurrentServingSide = servingSide;
        HomeRotationOffset = AwayRotationOffset = 0; StartedAt = now;
    }
    public void ApplyPoint(MatchSheets.MatchSide side, DateTimeOffset now)
    {
        if (Status != MatchSetStatus.InProgress) throw new DomainValidationException("Point requires an InProgress set.");
        if (side == MatchSheets.MatchSide.Home) HomePoints++; else AwayPoints++;
        if (CurrentServingSide != side)
        {
            if (side == MatchSheets.MatchSide.Home) HomeRotationOffset = (byte)((HomeRotationOffset + 1) % 6);
            else AwayRotationOffset = (byte)((AwayRotationOffset + 1) % 6);
        }
        CurrentServingSide = side;
        var target = SetNumber == 5 ? 15 : 25;
        if ((side == MatchSheets.MatchSide.Home ? HomePoints : AwayPoints) >= target && Math.Abs(HomePoints - AwayPoints) >= 2)
        { Status = MatchSetStatus.Finished; WinnerSide = side; FinishedAt = now; }
    }
    public void Rebuild(short homePoints, short awayPoints, MatchSheets.MatchSide servingSide, byte homeOffset, byte awayOffset, DateTimeOffset? finishedAt)
    {
        HomePoints = homePoints; AwayPoints = awayPoints; CurrentServingSide = servingSide;
        HomeRotationOffset = homeOffset; AwayRotationOffset = awayOffset;
        var target = SetNumber == 5 ? 15 : 25;
        var finished = (homePoints >= target || awayPoints >= target) && Math.Abs(homePoints - awayPoints) >= 2;
        Status = finished ? MatchSetStatus.Finished : MatchSetStatus.InProgress;
        WinnerSide = finished ? (homePoints > awayPoints ? MatchSheets.MatchSide.Home : MatchSheets.MatchSide.Away) : null;
        FinishedAt = finished ? finishedAt : null;
    }
}
