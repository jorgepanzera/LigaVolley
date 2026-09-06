using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed partial class MatchEngineEndpointsTests
{
    [Fact]
    public async Task OpenApi_documents_frozen_rules_specific_confirmations_and_atomic_request()
    {
        using var doc = JsonDocument.Parse(await factory.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("RulesSnapshotDto").GetProperty("properties").GetProperty("maxSubstitutionsPerSet").GetProperty("nullable").GetBoolean());
        Assert.Equal(1, schemas.GetProperty("SubstitutionRequest").GetProperty("properties").GetProperty("replacements").GetProperty("minItems").GetInt32());
        Assert.True(schemas.GetProperty("AddTimeoutRequest").GetProperty("properties").TryGetProperty("confirmedRuleWarnings", out _));
        Assert.True(schemas.GetProperty("AddPointRequest").GetProperty("properties").TryGetProperty("observedServerMatchPlayerId", out _));
        Assert.True(schemas.GetProperty("ProblemDetails").GetProperty("properties").TryGetProperty("warnings", out _));
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/scorer/matches/{matchId}/sets/{setNumber}/substitution-requests", out _));
        Assert.True(paths.TryGetProperty("/api/admin/competitions/{id}/match-rules", out _));
    }
    [Fact]
    public async Task Second_libero_and_atomic_multiple_decisions_survive_sync_snapshot_and_retry()
    {
        var x = await Open(twoLiberos: true, maxSubstitutions: 1);
        var sheet = await PlayingRulesMatch(x.MatchId);
        Assert.Equal(2, sheet.Home.Liberos.Count);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), x.Home[6], x.Home[4]));
        var replacementId = Guid.NewGuid();
        var request = Sync(sheet, [(replacementId, 1L, ScorerSyncEventType.LiberoEnter,
            new { setNumber=1, liberoMatchPlayerId=x.Home[7], replacedMatchPlayerId=x.Home[6], confirmedRuleWarnings=new[]{"libero_replacement_without_completed_rally"} })]);
        var switched = await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync", request);
        Assert.Contains(switched.Snapshot.OperationalState!.Sets[0].LiberoReplacements, p => p.Active && p.LiberoMatchPlayerId == x.Home[7]);
        var duplicate = await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync", request);
        Assert.Equal(ScorerSyncResultStatus.AlreadyAccepted, duplicate.Results[0].Status);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/libero/exit", new LiberoExitRequest(Guid.NewGuid(), x.Home[7], ["libero_replacement_without_completed_rally"]));
        var id = Guid.NewGuid();
        var multiple = Sync(sheet, [(id, 2L, ScorerSyncEventType.SubstitutionRequest,
            new { setNumber=1, side="HOME", replacements=new[]{new {playerOutMatchPlayerId=x.Home[0],playerInMatchPlayerId=x.Home[6]},new {playerOutMatchPlayerId=x.Home[1],playerInMatchPlayerId=x.Home[7]}}, confirmedRuleWarnings=Array.Empty<string>() })]);
        var accepted = await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync", multiple);
        Assert.Equal(2, accepted.Snapshot.OperationalState!.Sets[0].Substitutions.Count);
        Assert.Equal(2, accepted.LastAcceptedSequence);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        Assert.Equal(1, await db.MatchEvents.CountAsync(e => e.EventUuid == id));
        Assert.Equal(2, await db.Set<MatchSubstitution>().CountAsync(s => s.RequestEventUuid == id));
    }
    private async Task<MatchSheetSnapshotDto> PlayingRulesMatch(int id)
    {
        var sheet = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"/api/scorer/matches/{id}/sheet", Json))!;
        await Prepare(id);
        await Lineup(id, 1, MatchSide.Home, sheet.Home.Players.OrderBy(x => x.JerseyNumber).Take(6).Select(x => x.MatchPlayerId).ToArray());
        await Lineup(id, 1, MatchSide.Away, sheet.Away.Players.OrderBy(x => x.JerseyNumber).Take(6).Select(x => x.MatchPlayerId).ToArray());
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{id}/sets/1/start", new StartSetRequest(MatchSide.Home));
        return sheet;
    }

    [Fact]
    public async Task Rules_freeze_overrides_and_direct_timeout_confirmation_is_idempotent()
    {
        var x = await Open(maxSubstitutions: 8, maxTimeouts: 1);
        var sheet = await PlayingRulesMatch(x.MatchId);
        Assert.Equal(8, sheet.RulesSnapshot!.MaxSubstitutionsPerSet);
        Assert.Equal(1, sheet.RulesSnapshot.MaxTimeoutsPerSet);
        Assert.Equal(1, sheet.RulesSnapshot.RulesSnapshotVersion);
        var updated = await Post<CompetitionDto>($"/api/admin/competitions/{sheet.Competition.CompetitionId}/match-rules",
            new UpdateCompetitionMatchRulesRequest(null, 4), HttpMethod.Put);
        Assert.Equal(6, updated.MatchRules!.EffectiveMaxSubstitutionsPerSet);
        Assert.Equal(4, updated.MatchRules.EffectiveMaxTimeoutsPerSet);
        var refreshed = (await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"/api/scorer/matches/{x.MatchId}/sheet", Json))!;
        Assert.Equal(sheet.RulesSnapshot, refreshed.RulesSnapshot);
        var url = $"/api/scorer/matches/{x.MatchId}/sets/1/timeouts";
        await Post<MatchEngineCommandResult>(url, new AddTimeoutRequest(Guid.NewGuid(), MatchSide.Home));
        var uuid = Guid.NewGuid();
        var rejected = await factory.Client.PostAsJsonAsync(url, new AddTimeoutRequest(uuid, MatchSide.Home), Json);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("rule_confirmation_required", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("timeout_limit_exceeded", problem.RootElement.GetProperty("warnings")[0].GetProperty("code").GetString());
        var request = new AddTimeoutRequest(uuid, MatchSide.Home, ["timeout_limit_exceeded"]);
        var accepted = await Post<MatchEngineCommandResult>(url, request);
        Assert.Equal(2, accepted.State.HomeTimeouts);
        Assert.True((await Post<MatchEngineCommandResult>(url, request)).AlreadyApplied);
        var conflicting = await factory.Client.PostAsJsonAsync(url, request with { Side = MatchSide.Away }, Json);
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var stored = await db.MatchEvents.SingleAsync(e => e.EventUuid == uuid);
        Assert.Contains("timeout_limit_exceeded", stored.CommandPayload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sync_accepts_sporting_disagreement_retries_and_takeover(bool confirmed)
    {
        var x = await Open(maxTimeouts: 1);
        var sheet = await PlayingRulesMatch(x.MatchId);
        var commands = new[] {
            (Guid.NewGuid(), 1L, ScorerSyncEventType.Timeout, (object)new { setNumber=1, side="HOME", confirmedRuleWarnings=Array.Empty<string>() }),
            (Guid.NewGuid(), 2L, ScorerSyncEventType.Timeout, (object)new { setNumber=1, side="HOME", confirmedRuleWarnings=confirmed ? new[] { "timeout_limit_exceeded", "unexpected_server" } : Array.Empty<string>() })
        };
        var request = Sync(sheet, commands);
        var accepted = await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync", request);
        Assert.Equal(2, accepted.Snapshot.CurrentState.HomeTimeouts);
        var retry = await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync", request);
        Assert.All(retry.Results, result => Assert.Equal(ScorerSyncResultStatus.AlreadyAccepted, result.Status));
        var takeover = await Post<TakeOverMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/take-over",
            new TakeOverMatchSheetRequest(sheet.Sheet.SheetUuid, sheet.Session.SessionUuid, "rules-takeover", Guid.NewGuid()));
        Assert.Equal(accepted.Snapshot.RulesSnapshot, takeover.Snapshot.RulesSnapshot);
        Assert.Equal(2, takeover.Snapshot.CurrentState.HomeTimeouts);
        var next = Sync(takeover.Snapshot, [(Guid.NewGuid(), 1L, ScorerSyncEventType.Point, new {setNumber=1, winningSide="AWAY"})]);
        Assert.Equal(1, (await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync", next)).Snapshot.CurrentState.AwayPoints);
        var hard = Sync(takeover.Snapshot, [(Guid.NewGuid(), 2L, ScorerSyncEventType.SubstitutionRequest,
            new {setNumber=1, side="HOME", replacements=new[]{new {playerOutMatchPlayerId=x.Home[0],playerInMatchPlayerId=x.Away[6]}}, confirmedRuleWarnings=new[]{"substitution_limit_exceeded"}})]);
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sync", hard, Json)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        Assert.Equal(1, await db.Set<MatchSheetSession>().CountAsync(s => s.MatchSheet.MatchId == x.MatchId && s.Status == MatchSheetSessionStatus.Active));
        Assert.Equal(1, await db.Set<MatchSheetSession>().CountAsync(s => s.MatchSheet.MatchId == x.MatchId && s.Status == MatchSheetSessionStatus.Abandoned));
        Assert.Equal(2, await db.Set<MatchTimeout>().CountAsync(t => t.MatchSet.MatchSheet!.MatchId == x.MatchId));
    }

    [Fact]
    public async Task Multiple_substitution_is_one_atomic_event_with_multiple_confirmations()
    {
        var x = await Open(maxSubstitutions: 1);
        await PlayingRulesMatch(x.MatchId);
        var url = $"/api/scorer/matches/{x.MatchId}/sets/1/substitution-requests";
        var request = new SubstitutionRequest(Guid.NewGuid(), MatchSide.Home,
            [new(x.Home[0], x.Home[6]), new(x.Home[1], x.Home[7])]);
        var warning = await factory.Client.PostAsJsonAsync(url, request, Json);
        Assert.Equal(HttpStatusCode.Conflict, warning.StatusCode);
        using var problem = JsonDocument.Parse(await warning.Content.ReadAsStringAsync());
        var codes = problem.RootElement.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString()!).ToArray();
        Assert.Contains("substitution_limit_exceeded", codes);
        Assert.Contains("libero_used_as_regular_substitute", codes);
        var invalid = request with { Replacements = [new(x.Home[0], x.Home[6]), new(x.Home[1], x.Away[7])], ConfirmedRuleWarnings = codes };
        Assert.Equal(HttpStatusCode.Conflict, (await factory.Client.PostAsJsonAsync(url, invalid, Json)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            Assert.Equal(0, await db.Set<MatchSubstitution>().CountAsync(s => s.MatchSet.MatchSheet!.MatchId == x.MatchId));
        }
        var accepted = await Post<MatchEngineCommandResult>(url, request with { ConfirmedRuleWarnings = codes });
        Assert.Contains(accepted.State.HomeCourtState, p => p.EffectiveMatchPlayerId == x.Home[6]);
        Assert.Contains(accepted.State.HomeCourtState, p => p.EffectiveMatchPlayerId == x.Home[7]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            Assert.Equal(1, await db.MatchEvents.CountAsync(e => e.EventUuid == request.EventUuid));
            Assert.Equal(2, await db.Set<MatchSubstitution>().CountAsync(s => s.RequestEventUuid == request.EventUuid));
        }
    }

    [Fact]
    public async Task Libero_keeps_covering_substituted_regular_and_observed_server_preserves_rotation()
    {
        var x = await Open();
        await PlayingRulesMatch(x.MatchId);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), x.Home[7], x.Home[4]));
        var changed = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/substitutions", new AddSubstitutionRequest(Guid.NewGuid(), x.Home[4], x.Home[6]));
        Assert.Contains(changed.State.HomeCourtState, p => p.LogicalLineupPosition == LineupPosition.P5 && p.EffectiveMatchPlayerId == x.Home[7]);
        await Point(x.MatchId, 1, MatchSide.Away);
        var corrected = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/points/correct-last", new CorrectLastPointRequest(Guid.NewGuid()));
        Assert.Contains(corrected.State.HomeCourtState, p => p.LogicalLineupPosition == LineupPosition.P5 && p.EffectiveMatchPlayerId == x.Home[7]);
        var restored = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/libero/exit", new LiberoExitRequest(Guid.NewGuid(), x.Home[7], ["libero_replacement_without_completed_rally"]));
        Assert.Contains(restored.State.HomeCourtState, p => p.LogicalLineupPosition == LineupPosition.P5 && p.EffectiveMatchPlayerId == x.Home[6]);
        var point = new AddPointRequest(Guid.NewGuid(), MatchSide.Home, x.Home[7], ["unexpected_server", "libero_service_not_allowed"]);
        var result = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/points", point);
        Assert.Equal(0, result.State.HomeRotationOffset);
        Assert.Equal(x.Home[0], result.State.ServerMatchPlayerId);
        using var live = JsonDocument.Parse(await factory.Client.GetStringAsync($"/api/public/matches/{x.MatchId}/live"));
        Assert.Equal(1, live.RootElement.GetProperty("sets")[0].GetProperty("homePoints").GetInt32());
        Assert.Equal(1, live.RootElement.GetProperty("servingPlayer").GetProperty("jerseyNumber").GetInt32());
    }
}
