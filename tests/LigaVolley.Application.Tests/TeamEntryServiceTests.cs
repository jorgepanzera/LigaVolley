using LigaVolley.Application.Common;
using LigaVolley.Application.TeamEntries;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;
namespace LigaVolley.Application.Tests;
public sealed class TeamEntryServiceTests
{
    [Fact] public async Task Add_AllowsBelowMinimumAndCreatesContextualEntry() { var f=Fixture(validCount:0); var result=await f.Service.AddAsync(1,new(7,1),default); Assert.Equal(TeamEntryStatus.Registered,result.Status); Assert.Same(f.Team,f.Entries.Added!.Team); Assert.Same(f.Competition,f.Entries.Added.Competition); Assert.Equal(1,f.Unit.SaveCount); }
    [Fact] public async Task Add_RejectsDuplicateTeamWithinCompetition() { var f=Fixture(); f.Entries.TeamExists=true; var ex=await Assert.ThrowsAsync<ResourceConflictException>(()=>f.Service.AddAsync(1,new(7,null),default)); Assert.Equal("team_already_entered",ex.Code); }
    [Fact] public async Task Add_NeverExceedsMaximumValidTeams() { var f=Fixture(validCount:4); var ex=await Assert.ThrowsAsync<ResourceConflictException>(()=>f.Service.AddAsync(1,new(7,null),default)); Assert.Equal("competition_max_teams_reached",ex.Code); }
    [Fact] public async Task RangeValidation_DistinguishesLoadingLimitFromFixtureReadiness() { var below=Fixture(validCount:1); var first=await below.Service.ValidateRangeAsync(1,default); Assert.True(first.IsWithinLoadLimit); Assert.False(first.IsReadyForFixture); var ready=Fixture(validCount:2); Assert.True((await ready.Service.ValidateRangeAsync(1,default)).IsReadyForFixture); }
    [Fact] public async Task Remove_IsAllowedInDraft() { var f=Fixture(); var entry=new TeamEntry(f.Competition,f.Team,null); f.Entries.Seed(1,5,entry); await f.Service.RemoveAsync(1,5,default); Assert.True(f.Entries.Removed); }
    [Fact] public async Task Remove_IsRejectedOutsideDraft() { var f=Fixture(); f.Competition.ChangeStatus(CompetitionStatus.Cancelled); var ex=await Assert.ThrowsAsync<ResourceConflictException>(()=>f.Service.RemoveAsync(1,5,default)); Assert.Equal("competition_not_draft",ex.Code); Assert.False(f.Entries.Removed); }
    private static TestFixture Fixture(int validCount=0) { var format=new CompetitionFormat("F","Format",null,2,4); var competition=new Competition("Competition",new Season(2026,"2026",null,null),new Division("A",1,Gender.Female),format,CompetitionPeriodType.Annual,null,null); var competitions=new FakeCompetitionRepository(); competitions.Seed(1,competition); var team=new Team("Team",Gender.Female,null); var teams=new FakeTeamRepository(); teams.Seed(7,team); var entries=new FakeTeamEntryRepository{ValidCount=validCount}; var unit=new FakeUnitOfWork(); return new(new TeamEntryService(entries,competitions,teams,unit),entries,unit,competition,team); }
    private sealed record TestFixture(TeamEntryService Service,FakeTeamEntryRepository Entries,FakeUnitOfWork Unit,Competition Competition,Team Team);
}
