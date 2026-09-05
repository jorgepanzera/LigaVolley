using System.Net.Http.Json;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed partial class MatchSheetOpeningEndpointsTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(1, 42)]
    [InlineData(2, 99)]
    public async Task Declared_roster_liberos_survive_open_sheet_set_plan_and_takeover(int count, short jersey)
    {
        var data = await Seed(count);
        var root = $"/api/scorer/matches/{data.MatchId}";
        var context = (await factory.Client.GetFromJsonAsync<OpenMatchContextDto>($"{root}/open-context", Json))!;
        OpenMatchTeamRequest Selection(OpenMatchTeamContextDto side) => new(
            side.Players.Select((p, i) => new OpenMatchPlayerRequest(p.CompetitionRosterPlayerId,
                (short)(p.Role == PlayerRole.Libero ? jersey - (i - 6) : 10 + i), i == 0)).ToArray(),
            side.Players.Where(p => p.Role == PlayerRole.Libero).Select(p => p.CompetitionRosterPlayerId).ToArray(), []);
        Assert.Equal(count, context.Home.Players.Count(p => p.Role == PlayerRole.Libero));
        Assert.Equal(count, context.Away.Players.Count(p => p.Role == PlayerRole.Libero));
        var request = new OpenMatchSheetRequest(Guid.NewGuid(), "libero-flow", Selection(context.Home), Selection(context.Away));
        var opening = await factory.Client.PostAsJsonAsync($"{root}/open", request, Json);
        opening.EnsureSuccessStatusCode();
        var opened = (await opening.Content.ReadFromJsonAsync<OpenMatchSheetResponse>(Json))!.MatchSheet;
        foreach (var team in new[] { opened.Home, opened.Away })
        {
            Assert.Equal(count, team.Liberos.Count);
            Assert.All(team.Liberos, l => Assert.Equal(PlayerRole.Libero, team.Players.Single(p => p.MatchPlayerId == l.MatchPlayerId).Role));
            Assert.DoesNotContain(team.Liberos, l => team.Players.Single(p => p.MatchPlayerId == l.MatchPlayerId).Role == PlayerRole.Setter);
            if (count > 0) Assert.Equal(jersey, team.Players.Single(p => p.MatchPlayerId == team.Liberos[0].MatchPlayerId).JerseyNumber);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            Assert.Equal(2 * count, await db.Set<MatchLibero>().CountAsync(l => l.MatchTeam.MatchSheet.MatchId == data.MatchId));
        }
        (await factory.Client.PostAsJsonAsync($"{root}/open", request, Json)).EnsureSuccessStatusCode();
        var loaded = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"{root}/sheet", Json))!;
        Assert.Equal(opened.Home.Liberos, loaded.Home.Liberos);
        Assert.Equal(opened.Away.Liberos, loaded.Away.Liberos);
        (await factory.Client.PostAsync($"{root}/sets/prepare", null)).EnsureSuccessStatusCode();
        foreach (var team in new[] { loaded.Home, loaded.Away })
        {
            var regular = team.Players.Where(p => p.Role != PlayerRole.Libero).Select(p => p.MatchPlayerId).ToArray();
            var lineup = new SetLineupRequest(regular[0], regular[1], regular[2], regular[3], regular[4], regular[5],
                team.Liberos.FirstOrDefault()?.MatchPlayerId, count == 0 ? [] : [0]);
            (await factory.Client.PutAsJsonAsync($"{root}/sets/1/lineups/{team.Side}", lineup, Json)).EnsureSuccessStatusCode();
        }
        loaded = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"{root}/sheet", Json))!;
        var plans = Assert.Single(loaded.OperationalState.Sets).LiberoPlans!;
        Assert.Equal(loaded.Home.Liberos.FirstOrDefault()?.MatchPlayerId, plans["HOME"].LiberoMatchPlayerId);
        Assert.Equal(loaded.Away.Liberos.FirstOrDefault()?.MatchPlayerId, plans["AWAY"].LiberoMatchPlayerId);
        Assert.Equal(count > 0, plans["HOME"].Enabled);
        var takeoverResponse = await factory.Client.PostAsJsonAsync($"{root}/take-over",
            new TakeOverMatchSheetRequest(loaded.Sheet.SheetUuid, loaded.Session.SessionUuid, "libero-new-device", Guid.NewGuid()), Json);
        takeoverResponse.EnsureSuccessStatusCode();
        var taken = (await takeoverResponse.Content.ReadFromJsonAsync<TakeOverMatchSheetResponse>(Json))!.Snapshot;
        Assert.Equal(loaded.Home.Liberos, taken.Home.Liberos);
        Assert.Equal(loaded.Away.Liberos, taken.Away.Liberos);
        foreach (var side in new[] { "HOME", "AWAY" })
        {
            var plan = Assert.Single(taken.OperationalState.Sets).LiberoPlans![side];
            Assert.Equal(plans[side].LiberoMatchPlayerId, plan.LiberoMatchPlayerId);
            Assert.Equal(plans[side].LogicalPositions, plan.LogicalPositions);
        }
    }

    [Fact]
    public async Task An_open_sheet_without_declarations_does_not_infer_them_later_from_roster_roles()
    {
        var data = await Seed(1);
        var root = $"/api/scorer/matches/{data.MatchId}";
        var opening = await factory.Client.PostAsJsonAsync($"{root}/open", Request(data), Json);
        opening.EnsureSuccessStatusCode();
        var sheet = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"{root}/sheet", Json))!;
        Assert.Contains(sheet.Home.Players, p => p.Role == PlayerRole.Libero);
        Assert.Empty(sheet.Home.Liberos);
        Assert.Empty(sheet.Away.Liberos);
        var takeover = await factory.Client.PostAsJsonAsync($"{root}/take-over",
            new TakeOverMatchSheetRequest(sheet.Sheet.SheetUuid, sheet.Session.SessionUuid, "another-device", Guid.NewGuid()), Json);
        takeover.EnsureSuccessStatusCode();
        var taken = (await takeover.Content.ReadFromJsonAsync<TakeOverMatchSheetResponse>(Json))!.Snapshot;
        Assert.Empty(taken.Home.Liberos);
        Assert.Empty(taken.Away.Liberos);
    }
}
