using LigaVolley.Application.MatchSheets;
namespace LigaVolley.Api.Endpoints.Scorer;
internal static class ScorerMatchSheetEndpoints
{
 public static IEndpointRouteBuilder MapScorerMatchSheetEndpoints(this IEndpointRouteBuilder e){var g=e.MapGroup("/api/scorer/matches/{matchId:int}").WithTags("Scorer Match Sheet");
 // Example: GET /api/scorer/matches/1/open-context
 g.MapGet("/open-context",async(int matchId,MatchSheetService s,CancellationToken ct)=>Results.Ok(await s.GetOpenContextAsync(matchId,ct))).Produces<OpenMatchContextDto>().ProducesProblem(404).ProducesProblem(409);
 // Example: POST /api/scorer/matches/1/open
 // Body: { "clientRequestId": "91489d08-52cb-4c04-8af5-ca76400931f3", "deviceId": "scorer-tablet-07", "home": { "competitionRosterPlayerIds": [101,102,103,104,105,106], "captainCompetitionRosterPlayerId": 103, "liberoCompetitionRosterPlayerIds": [], "competitionRosterStaffIds": [] }, "away": { "competitionRosterPlayerIds": [201,202,203,204,205,206], "captainCompetitionRosterPlayerId": 204, "liberoCompetitionRosterPlayerIds": [], "competitionRosterStaffIds": [] } }
 g.MapPost("/open",async(int matchId,OpenMatchSheetRequest r,MatchSheetService s,CancellationToken ct)=>{var x=await s.OpenAsync(matchId,r,ct);return x.AlreadyOpen?Results.Ok(x):Results.Created($"/api/scorer/matches/{matchId}/sheet",x);}).Produces<OpenMatchSheetResponse>(201).Produces<OpenMatchSheetResponse>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
 // Example: GET /api/scorer/matches/1/sheet
 g.MapGet("/sheet",async(int matchId,MatchSheetService s,CancellationToken ct)=>Results.Ok(await s.GetSheetAsync(matchId,ct))).Produces<MatchSheetSnapshotDto>().ProducesProblem(404);return e;}
}
