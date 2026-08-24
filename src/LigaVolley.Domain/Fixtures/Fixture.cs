using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Venues;

namespace LigaVolley.Domain.Fixtures;

public enum MatchStatus { Pending, Scheduled, InProgress, Finished, Suspended, Cancelled }

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

    public void Schedule(DateTime? matchDate, int? venueId)
    {
        if (Status is not MatchStatus.Pending and not MatchStatus.Scheduled)
            throw new DomainValidationException("Only a pending or scheduled match can have its scheduling modified.");

        MatchDate = matchDate;
        VenueId = venueId;
        Status = matchDate.HasValue || venueId.HasValue ? MatchStatus.Scheduled : MatchStatus.Pending;
    }
}
