using LigaVolley.Application.MatchOfficials;using LigaVolley.Domain.MatchOfficials;
namespace LigaVolley.Api.Endpoints;internal static class ScorerMatchOfficialEndpoints{public static IEndpointRouteBuilder MapScorerMatchOfficialEndpoints(this IEndpointRouteBuilder e){var g=e.MapGroup("/api/scorer/matches/{matchId:int}/officials").WithTags("Scorer Match Officials");
// Example: PUT /api/scorer/matches/1/officials/FirstReferee
// Body: { "refereeId": 4 }
g.MapPut("/{role}",async(int matchId,MatchOfficialRole role,ReplaceMatchOfficialRequest r,MatchOfficialService s,CancellationToken ct)=>Results.Ok(await s.ReplaceAsync(matchId,role,r,ct))).Produces<MatchOfficialDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);return e;}}
