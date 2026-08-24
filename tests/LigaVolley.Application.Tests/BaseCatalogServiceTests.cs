using LigaVolley.Application.Clubs;
using LigaVolley.Application.Common;
using LigaVolley.Application.Teams;
using LigaVolley.Application.Venues;
using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Divisions;
namespace LigaVolley.Application.Tests;
public sealed class BaseCatalogServiceTests
{
    [Fact] public async Task Club_CreatePersistsAndRejectsDuplicateName() { var repo=new FakeClubRepository(); var unit=new FakeUnitOfWork(); var service=new ClubService(repo,unit); var result=await service.CreateAsync(new("Club", "C"),default); Assert.Equal("Club",result.Name); Assert.Equal(1,unit.SaveCount); repo.Seed(1,new Club("Duplicate",null)); await Assert.ThrowsAsync<ResourceConflictException>(()=>service.CreateAsync(new("Duplicate",null),default)); }
    [Fact] public async Task Team_CreateResolvesOptionalClubAndEnforcesNameGenderUniqueness() { var teams=new FakeTeamRepository(); var clubs=new FakeClubRepository(); var club=new Club("Club",null); clubs.Seed(1,club); var unit=new FakeUnitOfWork(); var service=new TeamService(teams,clubs,unit); var result=await service.CreateAsync(new("First",Gender.Female,1),default); Assert.Equal("Club",result.Club!.Name); Assert.Same(club,teams.Added!.Club); teams.Seed(1,teams.Added); await Assert.ThrowsAsync<ResourceConflictException>(()=>service.CreateAsync(new("First",Gender.Female,null),default)); }
    [Fact] public async Task Team_CreateRejectsMissingClub() { var service=new TeamService(new FakeTeamRepository(),new FakeClubRepository(),new FakeUnitOfWork()); await Assert.ThrowsAsync<ResourceNotFoundException>(()=>service.CreateAsync(new("First",Gender.Male,99),default)); }
    [Fact] public async Task Venue_CreateUpdateAndDeactivatePersist() { var repo=new FakeVenueRepository(); var unit=new FakeUnitOfWork(); var service=new VenueService(repo,unit); await service.CreateAsync(new("Gym",null),default); repo.Seed(1,repo.Added!); await service.UpdateAsync(1,new("Gym 2","Street"),default); var result=await service.SetActiveAsync(1,false,default); Assert.False(result.Active); Assert.Equal(3,unit.SaveCount); }
}
