using System.Reflection;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;

namespace LigaVolley.Application.Tests;

public sealed class CompetitionSchedulingServiceTests
{
    [Fact]
    public async Task Ready_draft_is_scheduled_and_retry_preserves_timestamp()
    {
        var f = Create(2, 2, [TeamEntryStatus.Active, TeamEntryStatus.Active], fixture: true);
        var preview = await f.Service.PreviewAsync(1, default);
        Assert.True(preview.CanSchedule);
        Assert.Equal(1, preview.InitialMatches);
        Assert.Equal(1, preview.UnscheduledMatches);

        var first = await f.Service.ScheduleAsync(1, default);
        var retry = await f.Service.ScheduleAsync(1, default);

        Assert.Equal(CompetitionStatus.Scheduled, f.Competition.Status);
        Assert.NotNull(f.Competition.ScheduledAt);
        Assert.False(first.AlreadyScheduled);
        Assert.True(retry.AlreadyScheduled);
        Assert.Equal(first.ScheduledAt, retry.ScheduledAt);
    }

    [Fact]
    public async Task Only_active_entries_count_and_missing_fixture_blocks_without_persisting()
    {
        var f = Create(2, 4, [TeamEntryStatus.Active, TeamEntryStatus.Registered, TeamEntryStatus.Withdrawn, TeamEntryStatus.Disqualified], fixture: false);
        var preview = await f.Service.PreviewAsync(1, default);
        Assert.False(preview.CanSchedule);
        Assert.Equal(1, preview.ActiveTeamEntries);
        Assert.Contains(preview.Blockers, x => x.Code == "competition_schedule_team_count_below_minimum");
        Assert.Contains(preview.Blockers, x => x.Code == "competition_schedule_fixture_missing");
        Assert.Equal(CompetitionStatus.Draft, f.Competition.Status);
        Assert.Equal(0, f.Unit.SaveCount);
    }

    [Fact]
    public async Task Participant_mismatch_blocks_post_with_stable_conflict()
    {
        var f = Create(2, 3, [TeamEntryStatus.Active, TeamEntryStatus.Active], fixture: true);
        f.Entries[1].ChangeStatus(TeamEntryStatus.Withdrawn);
        var preview = await f.Service.PreviewAsync(1, default);
        Assert.Contains(preview.Blockers, x => x.Code == "competition_schedule_fixture_participant_mismatch");
        var error = await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.ScheduleAsync(1, default));
        Assert.Equal("competition_cannot_schedule", error.Code);
    }

    private static SchedulingFixture Create(short min, short max, TeamEntryStatus[] statuses, bool fixture)
    {
        var format = new CompetitionFormat("F", "Format", null, min, max);
        var formatPhase = new FormatPhase("REG", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom);
        format.Phases.Add(formatPhase); Set(format, "CompetitionFormatId", 10); Set(formatPhase, "FormatPhaseId", 20);
        var competition = new Competition("League", new Season(2026, "2026", null, null), new Division("A", 1, Gender.Female), format, CompetitionPeriodType.Annual, null, null);
        Set(competition, "CompetitionId", 1); Set(competition, "CompetitionFormatId", 10); Set(competition.Phases[0], "CompetitionPhaseId", 30); Set(competition.Phases[0], "FormatPhaseId", 20);
        var competitionRepo = new FakeCompetitionRepository(); competitionRepo.Seed(1, competition);
        var formatRepo = new FakeCompetitionFormatRepository(); formatRepo.Seed(10, format);
        var entryRepo = new FakeTeamEntryRepository(); var all = new List<TeamEntry>();
        for (var i = 0; i < statuses.Length; i++)
        {
            var team = new Team($"Team {i + 1}", Gender.Female, null); Set(team, "TeamId", i + 1);
            var entry = new TeamEntry(competition, team, null); entry.ChangeStatus(statuses[i]); Set(entry, "TeamEntryId", i + 1);
            entryRepo.Seed(1, i + 1, entry); all.Add(entry);
        }
        var fixtureRepo = new FakeFixtureRepository();
        if (fixture && all.Count >= 2)
        {
            var match=new Match(competition, competition.Phases[0], null, all[0], all[1], 1, 1);
            Set(match,"PhaseId",30);Set(match,"HomeTeamEntryId",1);Set(match,"AwayTeamEntryId",2);fixtureRepo.Matches.Add(match);
        }
        var unit = new FakeUnitOfWork();
        return new(new CompetitionSchedulingService(competitionRepo, formatRepo, entryRepo, fixtureRepo, unit), competition, all, unit);
    }

    private static void Set(object target, string property, object value) => target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
    private sealed record SchedulingFixture(CompetitionSchedulingService Service, Competition Competition, List<TeamEntry> Entries, FakeUnitOfWork Unit);
}
