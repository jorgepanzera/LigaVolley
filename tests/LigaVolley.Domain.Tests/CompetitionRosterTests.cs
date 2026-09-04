using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.People;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;
using LigaVolley.Domain.Common;
namespace LigaVolley.Domain.Tests;
public sealed class CompetitionRosterTests
{
 [Fact]public void Draft_roster_activates_without_minimums(){var r=Roster();r.Activate();Assert.Equal(CompetitionRosterStatus.Active,r.Status);}
 [Fact]public void Inactive_players_do_not_count_toward_limit(){var r=Roster();for(var i=0;i<15;i++)r.AddPlayer(Player(i),PlayerRole.Setter);r.ChangePlayerStatus(r.Players[0],CompetitionRosterMemberStatus.Inactive);r.AddPlayer(Player(20),PlayerRole.Setter);Assert.Equal(15,r.Players.Count(x=>x.Status==CompetitionRosterMemberStatus.Active));}
 [Fact]public void Rejects_sixteenth_active_player(){var r=Roster();for(var i=0;i<15;i++)r.AddPlayer(Player(i),PlayerRole.Setter);Assert.Throws<DomainValidationException>(()=>r.AddPlayer(Player(20),PlayerRole.Setter));}
 [Fact]public void Rejects_third_libero_without_any_roster_jersey_rule(){var r=Roster();r.AddPlayer(Player(1),PlayerRole.Libero);r.AddPlayer(Player(2),PlayerRole.Libero);Assert.Throws<DomainValidationException>(()=>r.AddPlayer(Player(3),PlayerRole.Libero));r.AddPlayer(Player(4),PlayerRole.Setter);}
 [Fact]public void Rejects_third_active_coach_and_closed_edits(){var r=Roster();r.AddStaff(Coach());r.AddStaff(Coach());Assert.Throws<DomainValidationException>(()=>r.AddStaff(Coach()));r.Activate();r.Close();Assert.Throws<DomainValidationException>(()=>r.AddPlayer(Player(9),PlayerRole.Setter));}
 private static CompetitionRoster Roster(){var season=new Season(2026,"2026",null,null);var division=new Division("A",1,Gender.Female);var format=new CompetitionFormat("F","F",null,2,20);var competition=new Competition("C",season,division,format,CompetitionPeriodType.Annual,null,null);var club=new Club("Club",null);var team=new Team("Team",Gender.Female,club);return new CompetitionRoster(new TeamEntry(competition,team,null));}
 private static LigaVolley.Domain.People.Player Player(int n){var x=new LigaVolley.Domain.People.Player(new Person(null,null,$"P{n}","Test",null,null,null,null));typeof(LigaVolley.Domain.People.Player).GetProperty(nameof(LigaVolley.Domain.People.Player.PlayerId))!.SetValue(x,n+1);return x;}
 private static LigaVolley.Domain.People.Coach Coach(){var x=new LigaVolley.Domain.People.Coach(new Person(null,null,Guid.NewGuid().ToString(),"Coach",null,null,null,null));typeof(LigaVolley.Domain.People.Coach).GetProperty(nameof(LigaVolley.Domain.People.Coach.CoachId))!.SetValue(x,Random.Shared.Next(1,int.MaxValue));return x;}
}
