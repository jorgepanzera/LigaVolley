using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Application.Matches;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed partial class MatchEngineEndpointsTests
{
    [Theory]
    [InlineData(MatchSide.Home, 4, false)]
    [InlineData(MatchSide.Home, 5, true)]
    [InlineData(MatchSide.Away, 0, false)]
    public async Task Observed_initial_replacement_sync_retry_takeover_and_central_projections(MatchSide side, int position, bool plan)
    {
        var x = await Open();
        await Prepare(x.MatchId);
        foreach (var (team, ids) in new[] { (MatchSide.Home, x.Home), (MatchSide.Away, x.Away) })
            await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/lineups/{team}",
                new SetLineupRequest(ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], plan ? ids[7] : null, plan ? [0,4,5] : []), HttpMethod.Put);
        var root = $"/api/scorer/matches/{x.MatchId}";
        var start = await Post<MatchEngineCommandResult>($"{root}/sets/1/start", new StartSetRequest(MatchSide.Home));
        Assert.All(start.State.HomeCourtState.Concat(start.State.AwayCourtState), p => Assert.False(p.IsLiberoReplacement));
        var sheet = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"{root}/sheet", Json))!;
        Assert.Empty(sheet.OperationalState.Sets[0].LiberoReplacements);
        var idsSide = side == MatchSide.Home ? x.Home : x.Away;
        var uuid = Guid.NewGuid();
        var sync = Sync(sheet, [(uuid, 1L, ScorerSyncEventType.LiberoEnter, new { setNumber=1, side, liberoMatchPlayerId=idsSide[7], replacedMatchPlayerId=idsSide[position], confirmedRuleWarnings=Array.Empty<string>() })]);
        var accepted = await Post<SyncMatchSheetResponse>($"{root}/sync", sync);
        var replacement = Assert.Single(accepted.Snapshot.OperationalState.Sets[0].LiberoReplacements);
        Assert.True(replacement.Active); Assert.False(replacement.Automatic);
        Assert.Equal(position, replacement.Position); Assert.Equal(idsSide[position], replacement.ReplacedMatchPlayerId);
        Assert.Equal(0, accepted.Snapshot.CurrentState.HomePoints + accepted.Snapshot.CurrentState.AwayPoints);
        Assert.Equal(ScorerSyncResultStatus.AlreadyAccepted, (await Post<SyncMatchSheetResponse>($"{root}/sync", sync)).Results[0].Status);
        var admin = (await factory.Client.GetFromJsonAsync<AdminMatchSheetDto>($"/api/admin/matches/{x.MatchId}/match-sheet", Json))!;
        Assert.Contains(admin.Sheet!.OperationalState!.Court!, p => p.MatchPlayerId == idsSide[7] && p.RegularMatchPlayerId == idsSide[position]);
        Assert.False(Assert.Single(admin.Sheet.OperationalState.LiberoReplacements!).Automatic);
        using var live = JsonDocument.Parse(await factory.Client.GetStringAsync($"/api/public/matches/{x.MatchId}/live"));
        var publicCourt = live.RootElement.GetProperty(side == MatchSide.Home ? "homeCourt" : "awayCourt").GetProperty("positions");
        var publicPlayer = publicCourt.EnumerateArray().Single(p => p.GetProperty("position").GetInt32() == position + 1).GetProperty("player");
        Assert.Equal("8", publicPlayer.GetProperty("jerseyNumber").GetString());
        Assert.True(publicPlayer.GetProperty("isLibero").GetBoolean());
        var taken = await Post<TakeOverMatchSheetResponse>($"{root}/take-over", new TakeOverMatchSheetRequest(sheet.Sheet.SheetUuid, sheet.Session.SessionUuid, "observed-takeover", Guid.NewGuid()));
        Assert.Equal(accepted.Snapshot.OperationalState.Sets[0].LiberoReplacements, taken.Snapshot.OperationalState.Sets[0].LiberoReplacements);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var ev = await db.MatchEvents.SingleAsync(e => e.EventUuid == uuid);
        Assert.Equal(1, ev.LocalSequence); Assert.Contains("confirmedRuleWarnings", ev.CommandPayload);
        Assert.Single(await db.Set<MatchLiberoReplacement>().Where(r => r.MatchSet.MatchSheet!.MatchId == x.MatchId).ToListAsync());
    }

    [Fact]
    public async Task Observed_second_libero_stays_on_same_slot_and_two_effective_liberos_are_hard()
    {
        var x = await Open(twoLiberos: true);
        await PlayingRulesMatch(x.MatchId);
        var root = $"/api/scorer/matches/{x.MatchId}/sets/1";
        await Post<MatchEngineCommandResult>($"{root}/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), x.Home[6], x.Home[5]));
        var impossible = await factory.Client.PostAsJsonAsync($"{root}/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), x.Home[7], x.Home[4], ["libero_irregular_second_libero_replacement", "libero_replacement_without_completed_rally"]), Json);
        Assert.Equal(HttpStatusCode.Conflict, impossible.StatusCode);
        Assert.Contains("invalid_libero_replacement", await impossible.Content.ReadAsStringAsync());
        await Point(x.MatchId, 1, MatchSide.Away);
        await Point(x.MatchId, 1, MatchSide.Home);
        var swapped = await Post<MatchEngineCommandResult>($"{root}/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), x.Home[7], x.Home[6]));
        Assert.Equal(x.Home[7], Assert.Single(swapped.State.HomeCourtState.Where(p => p.IsLiberoReplacement)).EffectiveMatchPlayerId);
        await Point(x.MatchId, 1, MatchSide.Home);
        var corrected = await Post<MatchEngineCommandResult>($"{root}/points/correct-last", new CorrectLastPointRequest(Guid.NewGuid()));
        Assert.Equal(x.Home[7], Assert.Single(corrected.State.HomeCourtState.Where(p => p.IsLiberoReplacement)).EffectiveMatchPlayerId);
        var noRally = await factory.Client.PostAsJsonAsync($"{root}/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), x.Home[6], x.Home[7]), Json);
        Assert.Contains("libero_replacement_without_completed_rally", await noRally.Content.ReadAsStringAsync());
        await Post<MatchEngineCommandResult>($"{root}/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), x.Home[6], x.Home[7], ["libero_replacement_without_completed_rally"]));
    }

    [Fact]
    public async Task Observed_protocol_rejects_old_client_without_consuming_sequence()
    {
        var x = await Open(); var sheet = await PlayingRulesMatch(x.MatchId);
        var uuid = Guid.NewGuid();
        var request = new SyncMatchSheetRequest(sheet.Sheet.SheetUuid, sheet.Session.SessionUuid, sheet.Session.DeviceId,
            [new(uuid, 1, ScorerSyncEventType.Point, DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new {setNumber=1, winningSide="HOME"}))]);
        var response = await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sync", request, Json);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("scorer_protocol_incompatible", await response.Content.ReadAsStringAsync());
        var current = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"/api/scorer/matches/{x.MatchId}/sheet", Json))!;
        Assert.Equal(0, current.Session.LastAcceptedSequence); Assert.Equal(0, current.CurrentState.HomePoints);
    }

    [Fact]
    public async Task Historical_automatic_rows_and_unmarked_legacy_sync_are_not_reinterpreted()
    {
        var x = await Open(); await Prepare(x.MatchId);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/lineups/Home",
            new SetLineupRequest(x.Home[0], x.Home[1], x.Home[2], x.Home[3], x.Home[4], x.Home[5], x.Home[7], [4]), HttpMethod.Put);
        await Lineup(x.MatchId, 1, MatchSide.Away, x.Away.Take(6).ToArray());
        // Reproduce a pre-upgrade sheet only in the isolated integration database.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.MATCH_SHEET SET rules_snapshot_version=1, rules_protocol_version=1 WHERE match_id={x.MatchId}");
        }
        var root = $"/api/scorer/matches/{x.MatchId}";
        var sheet = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"{root}/sheet", Json))!;
        var originalPayload = JsonSerializer.SerializeToElement(new { setNumber=1, initialServingSide="HOME" });
        var request = new SyncMatchSheetRequest(sheet.Sheet.SheetUuid, sheet.Session.SessionUuid, sheet.Session.DeviceId,
            [new(Guid.NewGuid(), 1, ScorerSyncEventType.StartSet, DateTimeOffset.UtcNow, originalPayload)]);
        var accepted = await Post<SyncMatchSheetResponse>($"{root}/sync", request);
        var historical = Assert.Single(accepted.Snapshot.OperationalState.Sets[0].LiberoReplacements);
        Assert.True(historical.Automatic); Assert.True(historical.Active);
        await Point(x.MatchId, 1, MatchSide.Home);
        await Post<MatchEngineCommandResult>($"{root}/sets/1/points/correct-last", new CorrectLastPointRequest(Guid.NewGuid()));
        var taken = await Post<TakeOverMatchSheetResponse>($"{root}/take-over", new TakeOverMatchSheetRequest(sheet.Sheet.SheetUuid, sheet.Session.SessionUuid, "historical-takeover", Guid.NewGuid()));
        Assert.Equal(historical, Assert.Single(taken.Snapshot.OperationalState.Sets[0].LiberoReplacements));
        Assert.Equal(ScorerSyncResultStatus.AlreadyAccepted, (await Post<SyncMatchSheetResponse>($"{root}/sync", request)).Results[0].Status);
        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        Assert.Equal(originalPayload.GetRawText(), (await finalDb.MatchEvents.SingleAsync(e => e.EventUuid == request.Events[0].EventUuid)).CommandPayload);
    }

    [Fact]
    public async Task Observed_migration_down_rejects_silent_loss_and_preserves_sheet()
    {
        var x = await Open();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => migrator.MigrateAsync("20260905233721_ScorerRulesAssistant"));
        Assert.Contains("archival/conversion", error.Message);
        Assert.Contains("20260906181534_ObservedLiberoReplacements", await db.Database.GetAppliedMigrationsAsync());
        Assert.True(await db.MatchSheets.AnyAsync(s => s.MatchId == x.MatchId));
    }
}
