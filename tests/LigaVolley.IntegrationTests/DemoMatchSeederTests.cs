using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Application.PublicQueries;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchOfficials;
using LigaVolley.Infrastructure.Persistence;
using LigaVolley.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class DemoMatchSeederTests(LigaVolleyApiFactory factory) : IClassFixture<LigaVolleyApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

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
