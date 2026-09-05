using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Application.PublicQueries;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed partial class MatchEngineEndpointsTests
{
    [Fact]
    public async Task Public_server_matches_canonical_server_through_rotation_substitution_correction_and_close()
    {
        var x = await Open();
        var url = $"/api/public/matches/{x.MatchId}/live";
        var unavailable = await factory.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        using (var problem = JsonDocument.Parse(await unavailable.Content.ReadAsStringAsync()))
            Assert.Equal("public_live_match_not_available", problem.RootElement.GetProperty("code").GetString());

        await Prepare(x.MatchId);
        // Receiving P1 is occupied by the libero; the public server must still follow the regular canonical derivation.
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/lineups/Home",
            new SetLineupRequest(x.Home[0], x.Home[1], x.Home[2], x.Home[3], x.Home[4], x.Home[5], x.Home[7], [0]), HttpMethod.Put);
        await Lineup(x.MatchId, 1, MatchSide.Away, x.Away.Take(6).ToArray());
        var started = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/start", new StartSetRequest(MatchSide.Away));
        var first = await AssertPublicServer(x.MatchId, started.State);
        Assert.Contains(first.HomeCourt!.Positions, p => p.Player.IsLibero);

        var rotated = await Point(x.MatchId, 1, MatchSide.Home);
        await AssertPublicServer(x.MatchId, rotated.State);
        var substitute = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/substitutions",
            new AddSubstitutionRequest(Guid.NewGuid(), rotated.State.ServerMatchPlayerId!.Value, x.Home[6]));
        var substituted = await AssertPublicServer(x.MatchId, substitute.State);
        Assert.Equal((short)7, substituted.ServingPlayer!.JerseyNumber);
        var pointUuid = Guid.NewGuid();
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/points", new AddPointRequest(pointUuid, MatchSide.Home));
        var beforeRetry = await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>(url, Json);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/points", new AddPointRequest(pointUuid, MatchSide.Home));
        var afterRetry = await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>(url, Json);
        Assert.Equal(beforeRetry!.LastUpdatedAt, afterRetry!.LastUpdatedAt);
        var corrected = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/points/correct-last", new CorrectLastPointRequest(Guid.NewGuid()));
        await AssertPublicServer(x.MatchId, corrected.State);

        await WinSet(x.MatchId, 1, MatchSide.Home, 24);
        Assert.Null((await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>(url, Json))!.ServingPlayer);
        for (byte set = 2; set <= 3; set++)
        {
            await Prepare(x.MatchId);
            Assert.Null((await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>(url, Json))!.ServingPlayer);
            await Lineup(x.MatchId, set, MatchSide.Home, x.Home.Take(6).ToArray());
            await Lineup(x.MatchId, set, MatchSide.Away, x.Away.Take(6).ToArray());
            await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/{set}/start", new StartSetRequest(MatchSide.Home));
            await WinSet(x.MatchId, set, MatchSide.Home, 25);
        }
        await Post<CloseMatchResult>($"/api/scorer/matches/{x.MatchId}/close", new CloseMatchRequest(Guid.NewGuid()));
        var final = (await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>(url, Json))!;
        Assert.Equal(MatchStatus.Finished, final.Status);
        Assert.Null(final.ServingPlayer);
        Assert.Equal(3, final.Home.SetsWon);
    }

    [Fact]
    public async Task Suspended_public_live_preserves_court_and_nullable_historical_timestamp_without_a_server()
    {
        var x = await Open();
        await Prepare(x.MatchId);
        await Lineup(x.MatchId, 1, MatchSide.Home, x.Home.Take(6).ToArray());
        await Lineup(x.MatchId, 1, MatchSide.Away, x.Away.Take(6).ToArray());
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/start", new StartSetRequest(MatchSide.Home));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var match = await db.Matches.SingleAsync(m => m.MatchId == x.MatchId);
            db.Entry(match).Property(m => m.Status).CurrentValue = MatchStatus.Suspended;
            var sheet = await db.MatchSheets.SingleAsync(s => s.MatchId == x.MatchId);
            db.Entry(sheet).Property(s => s.LastOperationalUpdateAt).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        var live = (await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>($"/api/public/matches/{x.MatchId}/live", Json))!;
        Assert.Equal(MatchStatus.Suspended, live.Status);
        Assert.Null(live.ServingPlayer);
        Assert.Null(live.LastUpdatedAt);
        Assert.Equal(MatchSide.Home, live.ServingSide);
        Assert.Equal(6, live.HomeCourt!.Positions.Count);
    }

    private async Task<PublicLiveMatchDto> AssertPublicServer(int matchId, MatchSetStateDto state)
    {
        var url = $"/api/public/matches/{matchId}/live";
        var live = (await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>(url, Json))!;
        var sheet = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"/api/scorer/matches/{matchId}/sheet", Json))!;
        var team = state.CurrentServingSide == MatchSide.Home ? sheet.Home : sheet.Away;
        var player = team.Players.Single(p => p.MatchPlayerId == state.ServerMatchPlayerId);
        Assert.NotNull(live.ServingPlayer);
        Assert.Equal(player.JerseyNumber, live.ServingPlayer.JerseyNumber);
        Assert.Equal(state.CurrentServingSide, live.ServingSide);
        using var response = JsonDocument.Parse(await factory.Client.GetStringAsync(url));
        Assert.Equal(new[] { "displayName", "jerseyNumber" }, response.RootElement.GetProperty("servingPlayer").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(player.DisplayName, live.ServingPlayer.DisplayName);
        var repeat = (await factory.Client.GetFromJsonAsync<PublicLiveMatchDto>(url, Json))!;
        Assert.Equal(live.LastUpdatedAt, repeat.LastUpdatedAt);
        return live;
    }
}
