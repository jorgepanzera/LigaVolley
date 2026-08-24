using System.Reflection;
using LigaVolley.Application.Common;
using LigaVolley.Application.Matches;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;
using LigaVolley.Domain.Venues;

namespace LigaVolley.Application.Tests;

public sealed class MatchAdminServiceTests
{
    [Fact]
    public async Task Get_ReturnsAdminProjection()
    {
        var fixture = Setup();

        var result = await fixture.Service.GetAsync(51, default);

        Assert.Equal(51, result.MatchId);
        Assert.Equal("League", result.CompetitionName);
        Assert.Equal("REGULAR", result.PhaseCode);
        Assert.Equal("Home", result.HomeTeam!.TeamName);
        Assert.Equal("Away", result.AwayTeam!.TeamName);
    }

    [Fact]
    public async Task Schedule_StoresUtcDateAndActiveVenue()
    {
        var fixture = Setup();
        var requested = new DateTimeOffset(2026, 9, 12, 19, 30, 0, TimeSpan.FromHours(-3));

        var result = await fixture.Service.ScheduleAsync(51, new(requested, 8), default);

        Assert.Equal(requested.UtcDateTime, fixture.Match.MatchDate);
        Assert.Equal(8, fixture.Match.VenueId);
        Assert.Equal(MatchStatus.Scheduled, result.Status);
        Assert.Equal(8, result.Venue!.VenueId);
        Assert.Equal(1, fixture.Unit.SaveCount);
    }

    [Fact]
    public async Task Schedule_WithNullValuesUnprogramsMatch()
    {
        var fixture = Setup();
        await fixture.Service.ScheduleAsync(51, new(DateTimeOffset.UtcNow, 8), default);

        var result = await fixture.Service.ScheduleAsync(51, new(null, null), default);

        Assert.Equal(MatchStatus.Pending, result.Status);
        Assert.Null(result.MatchDate);
        Assert.Null(result.Venue);
    }

    [Fact]
    public async Task MissingMatchOrVenue_ReturnsNotFound()
    {
        var fixture = Setup();
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => fixture.Service.GetAsync(999, default));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => fixture.Service.ScheduleAsync(51, new(null, 999), default));
    }

    [Fact]
    public async Task InactiveVenueAndStartedMatch_ReturnStableConflicts()
    {
        var fixture = Setup(inactiveVenue: true);
        var inactive = await Assert.ThrowsAsync<ResourceConflictException>(() => fixture.Service.ScheduleAsync(51, new(null, 8), default));
        Assert.Equal("venue_inactive", inactive.Code);

        Set(fixture.Match, nameof(Match.Status), MatchStatus.InProgress);
        var state = await Assert.ThrowsAsync<ResourceConflictException>(() => fixture.Service.ScheduleAsync(51, new(null, null), default));
        Assert.Equal("match_scheduling_not_allowed", state.Code);
    }

    private static TestFixture Setup(bool inactiveVenue = false)
    {
        var format = new CompetitionFormat("RR", "Round robin", null, 2, 2);
        format.Phases.Add(new FormatPhase("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom));
        var competition = new Competition("League", new Season(2026, "2026", null, null), new Division("A", 1, Gender.Female), format, CompetitionPeriodType.Annual, null, null);
        var home = new TeamEntry(competition, new Team("Home", Gender.Female, null), null);
        var away = new TeamEntry(competition, new Team("Away", Gender.Female, null), null);
        Set(home, nameof(TeamEntry.TeamEntryId), 11); Set(away, nameof(TeamEntry.TeamEntryId), 12);
        var match = new Match(competition, competition.Phases.Single(), null, home, away, 1, 1);
        Set(match, nameof(Match.MatchId), 51);
        var fixtures = new FakeFixtureRepository(); fixtures.Matches.Add(match);
        var venues = new FakeVenueRepository(); var venue = new Venue("Central", "Street 1"); Set(venue, nameof(Venue.VenueId), 8); if (inactiveVenue) venue.SetActive(false); venues.Seed(8, venue);
        var unit = new FakeUnitOfWork();
        return new(new MatchAdminService(fixtures, venues, unit), match, unit);
    }

    private static void Set<T>(object target, string property, T value) => target.GetType()
        .GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
        .SetValue(target, value);

    private sealed record TestFixture(MatchAdminService Service, Match Match, FakeUnitOfWork Unit);
}
