using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchOfficials;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Domain.People;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class MatchEngineEndpointsTests(LigaVolleyApiFactory factory):IClassFixture<LigaVolleyApiFactory>
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};

    [Fact]
    public async Task Full_engine_finishes_three_zero_corrects_tracks_and_closes_idempotently()
    {
        var x=await Open();
        var prepared=await Prepare(x.MatchId);Assert.Equal(MatchSetStatus.Ready,prepared.State.SetStatus);
        await Lineup(x.MatchId,1,MatchSide.Home,x.Home.Take(6).ToArray());await Lineup(x.MatchId,1,MatchSide.Away,x.Away.Take(6).ToArray());
        var started=await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/start",new StartSetRequest(MatchSide.Home));Assert.Equal(x.Home[0],started.State.ServerMatchPlayerId);
        var receive=await Point(x.MatchId,1,MatchSide.Away);Assert.Equal((byte)1,receive.State.AwayRotationOffset);Assert.Equal(x.Away[1],receive.State.ServerMatchPlayerId);
        var corrected=await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/points/correct-last",new CorrectLastPointRequest(Guid.NewGuid()));Assert.Equal((short)0,corrected.State.AwayPoints);Assert.Equal((byte)0,corrected.State.AwayRotationOffset);
        var substitution=await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/substitutions",new AddSubstitutionRequest(Guid.NewGuid(),x.Home[0],x.Home[6]));Assert.Contains(substitution.State.HomeCourtState,p=>p.EffectiveMatchPlayerId==x.Home[6]);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/substitutions",new AddSubstitutionRequest(Guid.NewGuid(),x.Home[6],x.Home[0]));
        var libero=await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/libero/enter",new LiberoEnterRequest(Guid.NewGuid(),x.Home[7],x.Home[0]));Assert.Contains(libero.State.HomeCourtState,p=>p.EffectiveMatchPlayerId==x.Home[7]&&p.IsLiberoReplacement);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/libero/exit",new LiberoExitRequest(Guid.NewGuid(),x.Home[7]));
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/timeouts",new AddTimeoutRequest(Guid.NewGuid(),MatchSide.Home));
        var timeout2=await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/timeouts",new AddTimeoutRequest(Guid.NewGuid(),MatchSide.Home));Assert.Equal((byte)2,timeout2.State.HomeTimeouts);
        var third=await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/timeouts",new AddTimeoutRequest(Guid.NewGuid(),MatchSide.Home),Json);Assert.Equal(HttpStatusCode.Conflict,third.StatusCode);
        await WinSet(x.MatchId,1,MatchSide.Home,25);
        for(byte set=2;set<=3;set++){await Prepare(x.MatchId);await Lineup(x.MatchId,set,MatchSide.Home,x.Home.Take(6).ToArray());await Lineup(x.MatchId,set,MatchSide.Away,x.Away.Take(6).ToArray());await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/{set}/start",new StartSetRequest(set==2?MatchSide.Away:MatchSide.Home));await WinSet(x.MatchId,set,MatchSide.Home,25);}
        var blocked=await factory.Client.PostAsync($"/api/scorer/matches/{x.MatchId}/sets/prepare",null);Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);
        var closeUuid=Guid.NewGuid();var closeResponses=await Task.WhenAll(factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/close",new CloseMatchRequest(closeUuid),Json),factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/close",new CloseMatchRequest(closeUuid),Json));Assert.All(closeResponses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));var closes=await Task.WhenAll(closeResponses.Select(r=>r.Content.ReadFromJsonAsync<CloseMatchResult>(Json)));Assert.Single(closes.Where(c=>!c!.AlreadyClosed));Assert.Single(closes.Where(c=>c!.AlreadyClosed));var closed=closes[0]!;Assert.Equal(MatchSheetStatus.Closed,closed.MatchSheetStatus);Assert.Equal(MatchStatus.Finished,closed.MatchStatus);Assert.Equal((byte)3,closed.HomeSets);
        var after=await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/3/points",new AddPointRequest(Guid.NewGuid(),MatchSide.Home),Json);Assert.Equal(HttpStatusCode.Conflict,after.StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();var persisted=await db.Matches.Include(m=>m.Sets).SingleAsync(m=>m.MatchId==x.MatchId);Assert.Equal(MatchStatus.Finished,persisted.Status);Assert.Equal(3,persisted.Sets.Count);Assert.Equal(1,await db.MatchEvents.CountAsync(e=>e.MatchSheet.MatchId==x.MatchId&&e.EventType==MatchEventType.MatchClosed));Assert.Equal(1,await db.MatchEvents.CountAsync(e=>e.MatchSheet.MatchId==x.MatchId&&e.Status==MatchEventStatus.Cancelled));
    }

    [Fact]
    public async Task Lineup_validation_start_guard_and_prepare_concurrency_are_enforced()
    {
        var x=await Open();var prepares=await Task.WhenAll(factory.Client.PostAsync($"/api/scorer/matches/{x.MatchId}/sets/prepare",null),factory.Client.PostAsync($"/api/scorer/matches/{x.MatchId}/sets/prepare",null));Assert.All(prepares,r=>Assert.Contains(r.StatusCode,new[]{HttpStatusCode.Created,HttpStatusCode.OK}));
        var duplicate=await factory.Client.PutAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/lineups/HOME",new SetLineupRequest(x.Home[0],x.Home[0],x.Home[2],x.Home[3],x.Home[4],x.Home[5]),Json);Assert.Equal(HttpStatusCode.BadRequest,duplicate.StatusCode);
        var libero=await factory.Client.PutAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/lineups/HOME",new SetLineupRequest(x.Home[7],x.Home[1],x.Home[2],x.Home[3],x.Home[4],x.Home[5]),Json);Assert.Equal(HttpStatusCode.BadRequest,libero.StatusCode);
        await Lineup(x.MatchId,1,MatchSide.Home,x.Home.Take(6).ToArray());var start=await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/start",new StartSetRequest(MatchSide.Home),Json);Assert.Equal(HttpStatusCode.Conflict,start.StatusCode);
    }

    [Fact]
    public async Task Point_uuid_is_idempotent_and_openapi_contains_all_engine_routes()
    {
        var x=await Open();await Prepare(x.MatchId);await Lineup(x.MatchId,1,MatchSide.Home,x.Home.Take(6).ToArray());await Lineup(x.MatchId,1,MatchSide.Away,x.Away.Take(6).ToArray());await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/start",new StartSetRequest(MatchSide.Home));var uuid=Guid.NewGuid();var responses=await Task.WhenAll(factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/points",new AddPointRequest(uuid,MatchSide.Home),Json),factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/points",new AddPointRequest(uuid,MatchSide.Home),Json));Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));var points=await Task.WhenAll(responses.Select(r=>r.Content.ReadFromJsonAsync<MatchEngineCommandResult>(Json)));Assert.All(points,p=>Assert.Equal((short)1,p!.State.HomePoints));Assert.Single(points.Where(p=>!p!.AlreadyApplied));Assert.Single(points.Where(p=>p!.AlreadyApplied));
        using var doc=JsonDocument.Parse(await factory.Client.GetStringAsync("/swagger/v1/swagger.json"));var paths=doc.RootElement.GetProperty("paths");foreach(var path in new[]{"/api/scorer/matches/{matchId}/sets/prepare","/api/scorer/matches/{matchId}/sets/{setNumber}/lineups/{side}","/api/scorer/matches/{matchId}/sets/{setNumber}/start","/api/scorer/matches/{matchId}/sets/{setNumber}/points","/api/scorer/matches/{matchId}/sets/{setNumber}/points/correct-last","/api/scorer/matches/{matchId}/sets/{setNumber}/substitutions","/api/scorer/matches/{matchId}/sets/{setNumber}/libero/enter","/api/scorer/matches/{matchId}/sets/{setNumber}/libero/exit","/api/scorer/matches/{matchId}/sets/{setNumber}/timeouts","/api/scorer/matches/{matchId}/close"})Assert.True(paths.TryGetProperty(path,out _),path);
    }

    [Fact]
    public async Task Optional_tracking_flags_reject_their_endpoints_but_not_basic_scoring()
    {
        var x=await Open(false,false);await Prepare(x.MatchId);await Lineup(x.MatchId,1,MatchSide.Home,x.Home.Take(6).ToArray());await Lineup(x.MatchId,1,MatchSide.Away,x.Away.Take(6).ToArray());await Post<MatchEngineCommandResult>($"/api/scorer/matches/{x.MatchId}/sets/1/start",new StartSetRequest(MatchSide.Home));
        var substitution=await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/substitutions",new AddSubstitutionRequest(Guid.NewGuid(),x.Home[0],x.Home[6]),Json);Assert.Equal(HttpStatusCode.Conflict,substitution.StatusCode);
        var libero=await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sets/1/libero/enter",new LiberoEnterRequest(Guid.NewGuid(),x.Home[7],x.Home[0]),Json);Assert.Equal(HttpStatusCode.Conflict,libero.StatusCode);
        var point=await Point(x.MatchId,1,MatchSide.Home);Assert.Equal((short)1,point.State.HomePoints);
    }

    [Fact]
    public async Task Offline_sync_overlap_concurrency_takeover_and_abandoned_session_are_consistent()
    {
        var x=await Open();var sheet=(await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"/api/scorer/matches/{x.MatchId}/sheet",Json))!;var prepare=Guid.NewGuid();var first=Sync(sheet,[(prepare,1L,ScorerSyncEventType.PrepareSet,new{})]);var accepted=await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync",first);Assert.Equal(1,accepted.LastAcceptedSequence);Assert.Equal(ScorerSyncResultStatus.Applied,accepted.Results[0].Status);
        var home=Guid.NewGuid();var overlap=Sync(sheet,[(prepare,1L,ScorerSyncEventType.PrepareSet,new{}),(home,2L,ScorerSyncEventType.SetLineup,new{setNumber=1,side=MatchSide.Home,p1MatchPlayerId=x.Home[0],p2MatchPlayerId=x.Home[1],p3MatchPlayerId=x.Home[2],p4MatchPlayerId=x.Home[3],p5MatchPlayerId=x.Home[4],p6MatchPlayerId=x.Home[5]})]);var overlapped=await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync",overlap);Assert.Equal([ScorerSyncResultStatus.AlreadyAccepted,ScorerSyncResultStatus.Applied],overlapped.Results.Select(r=>r.Status));
        var away=Guid.NewGuid();var concurrent=Sync(sheet,[(away,3L,ScorerSyncEventType.SetLineup,new{setNumber=1,side=MatchSide.Away,p1MatchPlayerId=x.Away[0],p2MatchPlayerId=x.Away[1],p3MatchPlayerId=x.Away[2],p4MatchPlayerId=x.Away[3],p5MatchPlayerId=x.Away[4],p6MatchPlayerId=x.Away[5]})]);var pair=await Task.WhenAll(factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sync",concurrent,Json),factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sync",concurrent,Json));Assert.All(pair,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));var pairBodies=await Task.WhenAll(pair.Select(r=>r.Content.ReadFromJsonAsync<SyncMatchSheetResponse>(Json)));Assert.Single(pairBodies.Where(r=>r!.Results[0].Status==ScorerSyncResultStatus.Applied));Assert.Single(pairBodies.Where(r=>r!.Results[0].Status==ScorerSyncResultStatus.AlreadyAccepted));
        var takeId=Guid.NewGuid();var takeover=new TakeOverMatchSheetRequest(sheet.Sheet.SheetUuid,sheet.Session.SessionUuid,"device-b",takeId);var taken=await Post<TakeOverMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/take-over",takeover);var retried=await Post<TakeOverMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/take-over",takeover);Assert.True(retried.AlreadyApplied);Assert.Equal(taken.SessionUuid,retried.SessionUuid);Assert.Equal(0,taken.LastAcceptedSequence);
        var rejected=await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{x.MatchId}/sync",Sync(sheet,[(Guid.NewGuid(),4L,ScorerSyncEventType.StartSet,new{setNumber=1,initialServingSide=MatchSide.Home})]),Json);Assert.Equal(HttpStatusCode.Conflict,rejected.StatusCode);using var problem=JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());Assert.Equal("match_sheet_session_not_active",problem.RootElement.GetProperty("code").GetString());
        var current=await factory.Client.GetFromJsonAsync<MatchSheetSnapshotDto>($"/api/scorer/matches/{x.MatchId}/sheet",Json);var started=await Post<SyncMatchSheetResponse>($"/api/scorer/matches/{x.MatchId}/sync",Sync(current!,[(Guid.NewGuid(),1L,ScorerSyncEventType.StartSet,new{setNumber=1,initialServingSide=MatchSide.Home})]));Assert.Equal((byte)1,started.Snapshot.CurrentState.CurrentSetNumber);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();Assert.Equal(1,await db.Set<MatchSheetSession>().CountAsync(s=>s.MatchSheet.MatchId==x.MatchId&&s.Status==MatchSheetSessionStatus.Active));Assert.Equal(4,await db.MatchEvents.CountAsync(e=>e.MatchSheet.MatchId==x.MatchId&&e.MatchSheetSessionId.HasValue));using var swagger=JsonDocument.Parse(await factory.Client.GetStringAsync("/swagger/v1/swagger.json"));var paths=swagger.RootElement.GetProperty("paths");Assert.True(paths.TryGetProperty("/api/scorer/matches/{matchId}/sync",out _));Assert.True(paths.TryGetProperty("/api/scorer/matches/{matchId}/take-over",out _));
    }

    private async Task WinSet(int matchId,byte set,MatchSide side,int points){for(var i=0;i<points;i++)await Point(matchId,set,side);}
    private Task<MatchEngineCommandResult> Point(int matchId,byte set,MatchSide side)=>Post<MatchEngineCommandResult>($"/api/scorer/matches/{matchId}/sets/{set}/points",new AddPointRequest(Guid.NewGuid(),side));
    private Task<MatchEngineCommandResult> Prepare(int id)=>Post<MatchEngineCommandResult>($"/api/scorer/matches/{id}/sets/prepare",new{});
    private Task<MatchEngineCommandResult> Lineup(int id,byte set,MatchSide side,int[] p)=>Post<MatchEngineCommandResult>($"/api/scorer/matches/{id}/sets/{set}/lineups/{side}",new SetLineupRequest(p[0],p[1],p[2],p[3],p[4],p[5]),HttpMethod.Put);
    private async Task<T> Post<T>(string url,object body,HttpMethod? method=null){var response=method==HttpMethod.Put?await factory.Client.PutAsJsonAsync(url,body,Json):await factory.Client.PostAsJsonAsync(url,body,Json);if(!response.IsSuccessStatusCode)throw new HttpRequestException($"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>(Json))!;}
    private static SyncMatchSheetRequest Sync(MatchSheetSnapshotDto sheet,IEnumerable<(Guid Id,long Sequence,ScorerSyncEventType Type,object Payload)> events)=>new(sheet.Sheet.SheetUuid,sheet.Session.SessionUuid,sheet.Session.DeviceId,events.Select(x=>new ScorerSyncEvent(x.Id,x.Sequence,x.Type,DateTimeOffset.UtcNow,JsonSerializer.SerializeToElement(x.Payload,Json))).ToArray());

    private async Task<Data> Open(bool trackSubstitutions=true,bool trackLiberos=true)
    {
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();var suffix=Guid.NewGuid().ToString("N")[..8];var hp=Enumerable.Range(0,8).Select(i=>new Player(new Person(null,null,$"H{i}",suffix,null,null,null,null))).ToArray();var ap=Enumerable.Range(0,8).Select(i=>new Player(new Person(null,null,$"A{i}",suffix,null,null,null,null))).ToArray();db.Players.AddRange(hp);db.Players.AddRange(ap);await db.SaveChangesAsync();var format=new CompetitionFormat($"ME{suffix}","Engine",null,2,2);format.Phases.Add(new FormatPhase("R","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom));var competition=new Competition("Engine",new Season((short)Random.Shared.Next(2100,30000),$"S{suffix}",null,null),new Division($"D{suffix}",(short)Random.Shared.Next(100,30000),Gender.Female),format,CompetitionPeriodType.Annual,null,null);competition.MarkScheduledAfterInitialFixture();var he=new TeamEntry(competition,new Team($"H{suffix}",Gender.Female,null),null);var ae=new TeamEntry(competition,new Team($"A{suffix}",Gender.Female,null),null);var hr=new CompetitionRoster(he);var ar=new CompetitionRoster(ae);for(var i=0;i<8;i++){hr.AddPlayer(hp[i],(short)(i+1),i==7?PlayerRole.Libero:PlayerRole.Setter);ar.AddPlayer(ap[i],(short)(i+1),i==7?PlayerRole.Libero:PlayerRole.Setter);}hr.Activate();ar.Activate();var match=new Match(competition,competition.Phases[0],null,he,ae,1,1);match.Schedule(DateTime.UtcNow.AddDays(1),null);var refs=Enumerable.Range(0,3).Select(i=>new Referee(new Person(null,null,$"O{i}",suffix,null,null,null,null))).ToArray();db.CompetitionRosters.AddRange(hr,ar);db.Matches.Add(match);db.MatchOfficials.AddRange(new MatchOfficial(match,refs[0],MatchOfficialRole.FirstReferee),new MatchOfficial(match,refs[1],MatchOfficialRole.SecondReferee),new MatchOfficial(match,refs[2],MatchOfficialRole.Scorer));await db.SaveChangesAsync();var request=new OpenMatchSheetRequest(Guid.NewGuid(),"engine-test",new(hr.Players.Select(p=>p.CompetitionRosterPlayerId).ToArray(),null,[hr.Players[7].CompetitionRosterPlayerId],[]),new(ar.Players.Select(p=>p.CompetitionRosterPlayerId).ToArray(),null,[ar.Players[7].CompetitionRosterPlayerId],[])){TrackSubstitutions=trackSubstitutions,TrackLiberoReplacements=trackLiberos};var response=await factory.Client.PostAsJsonAsync($"/api/scorer/matches/{match.MatchId}/open",request,Json);response.EnsureSuccessStatusCode();var opened=(await response.Content.ReadFromJsonAsync<OpenMatchSheetResponse>(Json))!;return new(match.MatchId,opened.MatchSheet.Home.Players.OrderBy(p=>p.JerseyNumber).Select(p=>p.MatchPlayerId).ToArray(),opened.MatchSheet.Away.Players.OrderBy(p=>p.JerseyNumber).Select(p=>p.MatchPlayerId).ToArray());
    }
    private sealed record Data(int MatchId,int[] Home,int[] Away);
}
