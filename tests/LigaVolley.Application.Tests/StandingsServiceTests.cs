using System.Reflection;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.Standings;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.Standings;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;

namespace LigaVolley.Application.Tests;

public sealed class StandingsServiceTests
{
    [Fact]
    public async Task CountsOnlyFinishedMatchesKeepsZeroPlayedTeamsAndReportsNotFinal()
    {
        var f = CreateFixture(false); var finished = new Match(f.Competition,f.Phase,null,f.Entries[0],f.Entries[1],1,1);
        SetId(finished,"MatchId",11); SetId(finished,"HomeTeamEntryId",1); SetId(finished,"AwayTeamEntryId",2); finished.Finish(3,0,f.Entries[0],[new(1,25,10),new(2,25,11),new(3,25,12)]);
        var scheduled = new Match(f.Competition,f.Phase,null,f.Entries[0],f.Entries[2],1,2); scheduled.Schedule(DateTime.UtcNow,null);
        var inProgress=new Match(f.Competition,f.Phase,null,f.Entries[1],f.Entries[2],1,3); SetProperty(inProgress,"Status",MatchStatus.InProgress);
        var suspended=new Match(f.Competition,f.Phase,null,f.Entries[1],f.Entries[2],1,4); SetProperty(suspended,"Status",MatchStatus.Suspended);
        var cancelled=new Match(f.Competition,f.Phase,null,f.Entries[1],f.Entries[2],1,5); SetProperty(cancelled,"Status",MatchStatus.Cancelled);
        f.Repository.Matches.AddRange([finished,scheduled,inProgress,suspended,cancelled]);
        var result = await f.Service.GetAsync(1,10,null,default);
        Assert.False(result.IsFinal); Assert.Equal(3,result.Positions.Count); Assert.Equal(0,result.Positions.Single(x=>x.TeamEntryId==3).Played);
        Assert.Equal(1,result.Positions.Single(x=>x.TeamEntryId==1).Won);
    }

    [Fact]
    public async Task ValidatesGroupParameterAndOwnershipWithStableCodes()
    {
        var noGroups=CreateFixture(false);
        var notAllowed=await Assert.ThrowsAsync<RequestValidationException>(()=>noGroups.Service.GetAsync(1,10,20,default));
        Assert.Equal("standings_group_not_allowed",notAllowed.Code);

        var grouped=CreateFixture(true);
        var required=await Assert.ThrowsAsync<RequestValidationException>(()=>grouped.Service.GetAsync(1,10,null,default));
        Assert.Equal("standings_group_required",required.Code);
        grouped.Repository.ExistingGroupIds.Add(99);
        var other=await Assert.ThrowsAsync<RequestValidationException>(()=>grouped.Service.GetAsync(1,10,99,default));
        Assert.Equal("standings_group_not_in_phase",other.Code);
        await Assert.ThrowsAsync<ResourceNotFoundException>(()=>grouped.Service.GetAsync(1,10,98,default));
    }

    [Fact]
    public async Task RejectsPlayoffPhase()
    {
        var f=CreateFixture(false,PhaseType.Playoff);
        var error=await Assert.ThrowsAsync<RequestValidationException>(()=>f.Service.GetAsync(1,10,null,default));
        Assert.Equal("standings_not_supported_for_phase",error.Code);
    }

    private static Fixture CreateFixture(bool groups, PhaseType type=PhaseType.RoundRobin)
    {
        var format=new CompetitionFormat("TEST","Test",null,2,4);
        var phase=new FormatPhase("REGULAR","Regular",type,PhaseRole.Regular,1,type==PhaseType.Playoff?null:(short)1,type==PhaseType.Playoff?FixtureMode.Playoff:FixtureMode.BalancedRandom);
        if(groups) phase.Groups.Add(new("G1","Group 1",GroupRole.Other,1,1,FixtureMode.BalancedRandom,CarryOverMode.None));
        if(type==PhaseType.Playoff) phase.Series.Add(new("S1","Series",1,1,0,0));
        format.Phases.Add(phase); format.ScoringRules.Add(new(3,0,2,1)); format.TiebreakRules.Add(new(1,TiebreakCriterion.TablePoints,SortDirection.Desc));
        var competition=new Competition("Competition",new Season(2091,"2091",null,null),new Division("Division",1,Gender.Female),format,CompetitionPeriodType.Annual,null,null);
        SetId(competition,"CompetitionId",1); SetId(competition.Phases[0],"CompetitionPhaseId",10); if(groups) SetId(competition.Phases[0].Groups[0],"PhaseGroupId",20);
        var entries=Enumerable.Range(1,3).Select(i=>{var e=new TeamEntry(competition,new Team($"Team {i}",Gender.Female,null),null);SetId(e,"TeamEntryId",i);SetId(e.Team,"TeamId",100+i);return e;}).ToArray();
        var competitions=new FakeCompetitionRepository(); competitions.Seed(1,competition); var repository=new FakeStandingsRepository(); repository.PhaseEntries.AddRange(entries); if(groups)repository.GroupEntries.AddRange(entries);
        return new(new StandingsService(competitions,repository,new StandingsCalculator()),repository,competition,competition.Phases[0],entries);
    }

    private static void SetId<T>(T value,string propertyName,int id)
    {
        var property=typeof(T).GetProperty(propertyName,BindingFlags.Public|BindingFlags.Instance)!;
        property.SetValue(value,id);
    }
    private static void SetProperty<T,TValue>(T value,string propertyName,TValue propertyValue)
        => typeof(T).GetProperty(propertyName,BindingFlags.Public|BindingFlags.Instance)!.SetValue(value,propertyValue);
    private sealed record Fixture(StandingsService Service,FakeStandingsRepository Repository,Competition Competition,CompetitionPhase Phase,TeamEntry[] Entries);
    private sealed class FakeStandingsRepository:IStandingsRepository
    {
        public List<TeamEntry> PhaseEntries {get;}=[]; public List<TeamEntry> GroupEntries {get;}=[]; public List<Match> Matches {get;}=[]; public HashSet<int> ExistingGroupIds {get;}=[20];
        public Task<bool> PhaseGroupExistsAsync(int id,CancellationToken ct)=>Task.FromResult(ExistingGroupIds.Contains(id));
        public Task<IReadOnlyList<TeamEntry>> ListPhaseParticipantsAsync(int c,CancellationToken ct)=>Task.FromResult<IReadOnlyList<TeamEntry>>(PhaseEntries);
        public Task<IReadOnlyList<TeamEntry>> ListGroupParticipantsAsync(int c,int g,CancellationToken ct)=>Task.FromResult<IReadOnlyList<TeamEntry>>(GroupEntries);
        public Task<IReadOnlyList<Match>> ListScopeMatchesAsync(int c,int p,int? g,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Match>>(Matches);
    }
}
