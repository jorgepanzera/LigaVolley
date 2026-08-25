using LigaVolley.Domain.MatchOfficials;using LigaVolley.Domain.People;
namespace LigaVolley.Application.MatchOfficials;
public sealed record AddMatchOfficialRequest(int RefereeId,MatchOfficialRole Role);public sealed record UpdateMatchOfficialRequest(int RefereeId,MatchOfficialRole Role);public sealed record ReplaceMatchOfficialRequest(int RefereeId);public sealed record MatchOfficialDto(int MatchOfficialId,int MatchId,int RefereeId,int PersonId,string FirstName,string LastName,MatchOfficialRole Role,HealthCardStatus HealthCardStatus);
