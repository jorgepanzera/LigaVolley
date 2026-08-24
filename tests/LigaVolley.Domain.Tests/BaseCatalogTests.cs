using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Teams;
using LigaVolley.Domain.Venues;
namespace LigaVolley.Domain.Tests;
public sealed class BaseCatalogTests
{
    [Fact] public void Club_NormalizesAndValidatesFields() { var x=new Club(" Club "," C "); Assert.Equal("Club",x.Name); Assert.Equal("C",x.ShortName); Assert.True(x.Active); Assert.Throws<DomainValidationException>(()=>new Club("",null)); }
    [Fact] public void Team_SupportsOptionalClubAndValidatesGender() { var club=new Club("Club",null); var x=new Team(" Team ",Gender.Female,club); Assert.Same(club,x.Club); x.Update("Independent",Gender.Male,null); Assert.Null(x.Club); Assert.Throws<DomainValidationException>(()=>new Team("Team",(Gender)99,null)); }
    [Fact] public void Venue_NormalizesAndValidatesFields() { var x=new Venue(" Gym "," Address "); Assert.Equal("Gym",x.Name); Assert.Equal("Address",x.Address); Assert.Throws<DomainValidationException>(()=>new Venue("",null)); }
}
