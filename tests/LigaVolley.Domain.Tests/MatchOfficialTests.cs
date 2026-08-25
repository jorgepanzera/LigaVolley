using LigaVolley.Domain.Common;using LigaVolley.Domain.MatchOfficials;using LigaVolley.Domain.People;using LigaVolley.Domain.Fixtures;using LigaVolley.Domain.CompetitionFormats;using LigaVolley.Domain.Competitions;using LigaVolley.Domain.Seasons;using LigaVolley.Domain.Divisions;using LigaVolley.Domain.Teams;using LigaVolley.Domain.TeamEntries;
namespace LigaVolley.Domain.Tests;
public sealed class MatchOfficialTests
{
 [Fact]public void Assignment_requires_referee_profile_and_accepts_all_v1_roles(){var r=new Referee(new Person(null,null,"Ana","Official",null,null,null,null));foreach(var role in Enum.GetValues<MatchOfficialRole>())Assert.Equal(role,new MatchOfficial(A_Match(),r,role).Role);}
 [Fact]public void Invalid_role_is_rejected(){var r=new Referee(new Person(null,null,"Ana","Official",null,null,null,null));Assert.Throws<DomainValidationException>(()=>new MatchOfficial(A_Match(),r,(MatchOfficialRole)99));}
 private static Match A_Match(){var f=new CompetitionFormat("MO","Officials",null,2,2);f.Phases.Add(new FormatPhase("R","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom));var c=new Competition("C",new Season(2026,"2026",null,null),new Division("D",1,Gender.Female),f,CompetitionPeriodType.Annual,null,null);var a=new TeamEntry(c,new Team("A",Gender.Female,null),null);var b=new TeamEntry(c,new Team("B",Gender.Female,null),null);return new Match(c,c.Phases[0],null,a,b,1,1);}
}
