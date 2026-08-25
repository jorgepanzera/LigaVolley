using LigaVolley.Domain.Common;using LigaVolley.Domain.Fixtures;using LigaVolley.Domain.People;
namespace LigaVolley.Domain.MatchOfficials;
public enum MatchOfficialRole{FirstReferee,SecondReferee,Scorer}
public sealed class MatchOfficial
{
 private MatchOfficial(){} public MatchOfficial(Match match,Referee referee,MatchOfficialRole role){Match=match??throw new DomainValidationException("Match is required.");MatchId=match.MatchId;Update(referee,role);}
 public int MatchOfficialId{get;private set;}public int MatchId{get;private set;}public Match Match{get;private set;}=null!;public int RefereeId{get;private set;}public Referee Referee{get;private set;}=null!;public MatchOfficialRole Role{get;private set;}
 public void Update(Referee referee,MatchOfficialRole role){if(!Enum.IsDefined(role))throw new DomainValidationException("MatchOfficialRole is invalid.");Referee=referee??throw new DomainValidationException("Referee is required.");RefereeId=referee.RefereeId;Role=role;}
}
