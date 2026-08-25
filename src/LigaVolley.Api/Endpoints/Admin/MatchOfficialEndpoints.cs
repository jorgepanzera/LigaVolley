using LigaVolley.Application.MatchOfficials;
namespace LigaVolley.Api.Endpoints.Admin;internal static class MatchOfficialEndpoints{public static IEndpointRouteBuilder MapMatchOfficialEndpoints(this IEndpointRouteBuilder e){var g=e.MapGroup("/api/admin/matches/{matchId:int}/officials").WithTags("Admin Match Officials");
// Example: GET /api/admin/matches/1/officials
g.MapGet("/",async(int matchId,MatchOfficialService s,CancellationToken ct)=>Results.Ok(await s.ListAsync(matchId,ct))).Produces<IReadOnlyList<MatchOfficialDto>>().ProducesProblem(404);
// Example: POST /api/admin/matches/1/officials
// Body: { "refereeId": 2, "role": "FirstReferee" }
g.MapPost("/",async(int matchId,AddMatchOfficialRequest r,MatchOfficialService s,CancellationToken ct)=>{var x=await s.AddAsync(matchId,r,ct);return Results.Created($"/api/admin/matches/{matchId}/officials/{x.MatchOfficialId}",x);}).Produces<MatchOfficialDto>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
// Example: PUT /api/admin/matches/1/officials/2
// Body: { "refereeId": 3, "role": "SecondReferee" }
g.MapPut("/{matchOfficialId:int}",async(int matchId,int matchOfficialId,UpdateMatchOfficialRequest r,MatchOfficialService s,CancellationToken ct)=>Results.Ok(await s.UpdateAsync(matchId,matchOfficialId,r,ct))).Produces<MatchOfficialDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
// Example: DELETE /api/admin/matches/1/officials/2
g.MapDelete("/{matchOfficialId:int}",async(int matchId,int matchOfficialId,MatchOfficialService s,CancellationToken ct)=>{await s.DeleteAsync(matchId,matchOfficialId,ct);return Results.NoContent();}).Produces(204).ProducesProblem(404).ProducesProblem(409);return e;}}
