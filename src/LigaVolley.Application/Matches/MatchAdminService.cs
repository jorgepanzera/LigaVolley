using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.Venues;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.Venues;

namespace LigaVolley.Application.Matches;

public sealed class MatchAdminService(IFixtureRepository fixtures, IVenueRepository venues, IUnitOfWork unit)
{
    public async Task<MatchAdminDto> GetAsync(int matchId, CancellationToken ct)
    {
        var match = await fixtures.GetMatchAsync(matchId, false, ct)
            ?? throw new ResourceNotFoundException("Match", matchId);
        return Map(match);
    }

    public async Task<MatchAdminDto> ScheduleAsync(int matchId, ScheduleMatchRequest request, CancellationToken ct)
    {
        var match = await fixtures.GetMatchAsync(matchId, true, ct)
            ?? throw new ResourceNotFoundException("Match", matchId);

        if (match.Status is not MatchStatus.Pending and not MatchStatus.Scheduled)
            throw new ResourceConflictException("match_scheduling_not_allowed", "Only a pending or scheduled match can have its scheduling modified.");

        Venue? selectedVenue = null;
        if (request.VenueId is int venueId)
        {
            selectedVenue = await venues.GetAsync(venueId, false, ct)
                ?? throw new ResourceNotFoundException("Venue", venueId);
            if (!selectedVenue.Active)
                throw new ResourceConflictException("venue_inactive", "An inactive venue cannot be assigned to a match.");
        }

        match.Schedule(request.MatchDate?.UtcDateTime, request.VenueId);
        await unit.SaveChangesAsync(ct);

        return Map(match, selectedVenue, useVenueOverride: true);
    }

    private static MatchAdminDto Map(Match match, Venue? venueOverride = null, bool useVenueOverride = false) => new(
        match.MatchId,
        match.CompetitionId,
        match.Competition.Name,
        match.PhaseId,
        match.Phase.Code,
        match.PhaseGroupId,
        match.PhaseGroup?.Code,
        match.RoundNumber,
        match.MatchNumber,
        MapTeam(match.HomeTeamEntry),
        MapTeam(match.AwayTeamEntry),
        match.MatchDate is DateTime date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)) : null,
        (useVenueOverride ? venueOverride : match.Venue) is not { } venue ? null : new VenueSummaryDto(venue.VenueId, venue.Name, venue.Address, venue.Active),
        match.Status);

    private static FixtureTeamEntryDto? MapTeam(Domain.TeamEntries.TeamEntry? entry) => entry is null ? null
        : new(entry.TeamEntryId, entry.TeamId, entry.Team.Name, entry.Status);
}
