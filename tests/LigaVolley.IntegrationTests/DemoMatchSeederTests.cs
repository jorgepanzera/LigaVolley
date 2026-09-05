using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Application.PublicQueries;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchOfficials;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Infrastructure.Persistence;
using LigaVolley.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class DemoMatchSeederTests(LigaVolleyApiFactory factory) : IClassFixture<LigaVolleyApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reseed_removes_previous_sheet_and_result_and_allows_a_new_opening(bool finished)
    {
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoMatchSeeder>();
        var demo = await seeder.SeedAsync();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var match = await db.Matches.SingleAsync(x => x.MatchId == demo.MatchId);
        async Task<OpenMatchTeamRequest> Team(int entryId)
        {
            var roster = await db.CompetitionRosters.Include(x => x.Players).SingleAsync(x => x.TeamEntryId == entryId);
            return new(roster.Players.Where(x => x.Role != PlayerRole.Libero).Take(6)
                .Select((x, i) => new OpenMatchPlayerRequest(x.CompetitionRosterPlayerId, (short)(i + 1), i == 0)).ToArray(), [], []);
        }
        var request = new OpenMatchSheetRequest(Guid.NewGuid(), "demo-reset-test",
            await Team(match.HomeTeamEntryId!.Value), await Team(match.AwayTeamEntryId!.Value));
        var route = $"/api/scorer/matches/{demo.MatchId}";
        (await factory.Client.PostAsJsonAsync($"{route}/open", request, Json)).EnsureSuccessStatusCode();
        var oldSheet = await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"{route}/sheet", Json);
        var oldSheetId = await db.MatchSheets.Where(x => x.MatchId == demo.MatchId).Select(x => x.MatchSheetId).SingleAsync();
        (await factory.Client.PostAsync($"{route}/sets/prepare", null)).EnsureSuccessStatusCode();
        foreach (var side in new[] { "HOME", "AWAY" })
        {
            var players = (side == "HOME" ? oldSheet!.Home : oldSheet!.Away).Players.Select(x => x.MatchPlayerId).ToArray();
            (await factory.Client.PutAsJsonAsync($"{route}/sets/1/lineups/{side}",
                new SetLineupRequest(players[0], players[1], players[2], players[3], players[4], players[5]), Json)).EnsureSuccessStatusCode();
        }
        (await factory.Client.PostAsJsonAsync($"{route}/sets/1/start", new StartSetRequest(MatchSide.Home), Json)).EnsureSuccessStatusCode();
        (await factory.Client.PostAsJsonAsync($"{route}/sets/1/points", new AddPointRequest(Guid.NewGuid(), MatchSide.Home), Json)).EnsureSuccessStatusCode();
        (await factory.Client.PostAsJsonAsync($"{route}/sets/1/points/correct-last", new CorrectLastPointRequest(Guid.NewGuid()), Json)).EnsureSuccessStatusCode();
        (await factory.Client.PostAsJsonAsync($"{route}/sets/1/timeouts", new AddTimeoutRequest(Guid.NewGuid(), MatchSide.Home), Json)).EnsureSuccessStatusCode();
        if (finished)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.[MATCH] SET status='FINISHED', home_sets=3, away_sets=0, winner_team_entry_id={match.HomeTeamEntryId} WHERE match_id={match.MatchId}");

        Assert.Equal(demo, await seeder.SeedAsync());
        Assert.Equal(demo, await seeder.SeedAsync());
        await db.Entry(match).ReloadAsync();
        Assert.Equal(MatchStatus.Scheduled, match.Status);
        Assert.Null(match.HomeSets);
        Assert.Null(match.AwaySets);
        Assert.Null(match.WinnerTeamEntryId);
        Assert.False(await db.MatchSheets.AnyAsync(x => x.MatchId == demo.MatchId));
        Assert.False(await db.MatchSets.AnyAsync(x => x.MatchId == demo.MatchId));
        Assert.False(await db.MatchEvents.AnyAsync(x => x.MatchSheetId == oldSheetId));
        var context = await factory.Client.GetFromJsonAsync<OpenMatchContextDto>($"{route}/open-context", Json);
        Assert.Null(context!.ExistingMatchSheet);
        (await factory.Client.PostAsJsonAsync($"{route}/open", request with { ClientRequestId = Guid.NewGuid() }, Json)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Seed_is_idempotent_and_leaves_valid_scorer_and_public_context()
    {
        DemoMatchSeedResult first;
        DemoMatchSeedResult second;
        using (var scope = factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoMatchSeeder>();
            first = await seeder.SeedAsync();
            second = await seeder.SeedAsync();
        }

        Assert.Equal(first, second);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var match = await db.Matches.Include(x => x.Competition).Include(x => x.Venue).SingleAsync(x => x.MatchId == first.MatchId);
            Assert.Equal(MatchStatus.Scheduled, match.Status);
            Assert.Null(await db.MatchSheets.SingleOrDefaultAsync(x => x.MatchId == first.MatchId));
            Assert.NotNull(match.MatchDate);
            Assert.NotNull(match.Venue);
            var officials = await db.MatchOfficials.Where(x => x.MatchId == first.MatchId).OrderBy(x => x.Role).ToArrayAsync();
            Assert.Equal(3, officials.Length);
            Assert.Equal(
                new HashSet<MatchOfficialRole> { MatchOfficialRole.FirstReferee, MatchOfficialRole.SecondReferee, MatchOfficialRole.Scorer },
                officials.Select(x => x.Role).ToHashSet());
            foreach (var entryId in new[] { match.HomeTeamEntryId!.Value, match.AwayTeamEntryId!.Value })
            {
                var roster = await db.CompetitionRosters.Include(x => x.Players).Include(x => x.Staff).SingleAsync(x => x.TeamEntryId == entryId);
                Assert.Equal(CompetitionRosterStatus.Active, roster.Status);
                Assert.Equal(8, roster.Players.Count);
                Assert.Single(roster.Players.Where(x => x.Role == PlayerRole.Libero));
                Assert.Single(roster.Staff);
            }
        }

        var contextResponse = await factory.Client.GetAsync($"/api/scorer/matches/{first.MatchId}/open-context");
        Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);
        var context = await contextResponse.Content.ReadFromJsonAsync<OpenMatchContextDto>(Json);
        Assert.NotNull(context);
        Assert.Equal(8, context.Home.Players.Count);
        Assert.Equal(8, context.Away.Players.Count);
        Assert.Equal(3, context.MatchOfficials.Count);
        Assert.Null(context.ExistingMatchSheet);

        var competitionResponse = await factory.Client.GetAsync($"/api/public/competitions/{first.CompetitionId}");
        var matchResponse = await factory.Client.GetAsync($"/api/public/matches/{first.MatchId}");
        Assert.Equal(HttpStatusCode.OK, competitionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, matchResponse.StatusCode);
        var publicMatch = await matchResponse.Content.ReadFromJsonAsync<PublicMatchDto>(Json);
        Assert.Equal(MatchStatus.Scheduled, publicMatch!.Status);
        Assert.False(publicMatch.LiveAvailable);
    }
}
