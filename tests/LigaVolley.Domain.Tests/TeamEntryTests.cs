using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;
namespace LigaVolley.Domain.Tests;
public sealed class TeamEntryTests
{
    [Fact] public void Constructor_CreatesContextualRegisteredEntry() { var competition=Competition(); var team=new Team("Team",Gender.Female,new Club("Club",null)); var entry=new TeamEntry(competition,team,3); Assert.Same(competition,entry.Competition); Assert.Same(team,entry.Team); Assert.Equal((short)3,entry.Seed); Assert.Equal(TeamEntryStatus.Registered,entry.Status); Assert.True(entry.IsValid); }
    [Fact] public void Seed_IsOptionalButMustBePositive() { var entry=new TeamEntry(Competition(),new Team("Team",Gender.Female,null),null); entry.SetSeed(1); entry.SetSeed(null); Assert.Null(entry.Seed); Assert.Throws<DomainValidationException>(()=>entry.SetSeed(0)); }
    [Fact] public void WithdrawnAndDisqualifiedEntriesAreNotValid() { var entry=new TeamEntry(Competition(),new Team("Team",Gender.Female,null),null); entry.ChangeStatus(TeamEntryStatus.Withdrawn); Assert.False(entry.IsValid); entry.ChangeStatus(TeamEntryStatus.Disqualified); Assert.False(entry.IsValid); }
    private static Competition Competition()=>new("Competition",new Season(2026,"2026",null,null),new Division("A",1,Gender.Female),new CompetitionFormat("F","Format",null,2,4),CompetitionPeriodType.Annual,null,null);
}
