using System.Reflection;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;

namespace LigaVolley.Domain.Tests;

public sealed class MatchTests
{
    [Fact]
    public void Schedule_AssignsAndClearsSchedulingWhileMatchHasNotStarted()
    {
        var match = CreateMatch();
        var date = new DateTime(2026, 9, 12, 22, 30, 0, DateTimeKind.Utc);

        match.Schedule(date, 8);

        Assert.Equal(date, match.MatchDate);
        Assert.Equal(8, match.VenueId);
        Assert.Equal(MatchStatus.Scheduled, match.Status);

        match.Schedule(null, null);

        Assert.Null(match.MatchDate);
        Assert.Null(match.VenueId);
        Assert.Equal(MatchStatus.Pending, match.Status);
    }

    [Theory]
    [InlineData(MatchStatus.InProgress)]
    [InlineData(MatchStatus.Finished)]
    [InlineData(MatchStatus.Suspended)]
    [InlineData(MatchStatus.Cancelled)]
    public void Schedule_RejectsStatusesThatAreNoLongerAdministrativelySchedulable(MatchStatus status)
    {
        var match = CreateMatch();
        Set(match, nameof(Match.Status), status);

        Assert.Throws<DomainValidationException>(() => match.Schedule(DateTime.UtcNow, 1));
    }

    private static Match CreateMatch()
    {
        var format = new CompetitionFormat("RR", "Round robin", null, 2, 2);
        format.Phases.Add(new FormatPhase("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom));
        var competition = new Competition("League", new Season(2026, "2026", null, null), new Division("A", 1, Gender.Female), format, CompetitionPeriodType.Annual, null, null);
        return new Match(competition, competition.Phases.Single(), null,
            new TeamEntry(competition, new Team("A", Gender.Female, null), null),
            new TeamEntry(competition, new Team("B", Gender.Female, null), null), 1, 1);
    }

    private static void Set<T>(object target, string property, T value) => target.GetType()
        .GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
        .SetValue(target, value);
}
