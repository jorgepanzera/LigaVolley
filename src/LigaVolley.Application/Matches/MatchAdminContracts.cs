using LigaVolley.Application.Fixtures;
using LigaVolley.Application.Venues;
using LigaVolley.Domain.Fixtures;

namespace LigaVolley.Application.Matches;

public sealed record ScheduleMatchRequest(DateTimeOffset? MatchDate, int? VenueId);

public sealed record MatchAdminDto(
    int MatchId,
    int CompetitionId,
    string CompetitionName,
    int PhaseId,
    string PhaseCode,
    int? PhaseGroupId,
    string? PhaseGroupCode,
    short RoundNumber,
    short MatchNumber,
    FixtureTeamEntryDto? HomeTeam,
    FixtureTeamEntryDto? AwayTeam,
    DateTimeOffset? MatchDate,
    VenueSummaryDto? Venue,
    MatchStatus Status);
