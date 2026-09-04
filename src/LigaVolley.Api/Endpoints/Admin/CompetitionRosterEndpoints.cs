using LigaVolley.Application.CompetitionRosters;
namespace LigaVolley.Api.Endpoints.Admin;
internal static class CompetitionRosterEndpoints
{
 public static IEndpointRouteBuilder MapCompetitionRosterEndpoints(this IEndpointRouteBuilder e){var g=e.MapGroup("/api/admin/competitions/{competitionId:int}/entries/{teamEntryId:int}/roster").WithTags("Admin Competition Rosters");
 // Example: GET /api/admin/competitions/1/entries/2/roster
 g.MapGet("/",async(int competitionId,int teamEntryId,CompetitionRosterService s,CancellationToken ct)=>Results.Ok(await s.GetAsync(competitionId,teamEntryId,ct))).Produces<CompetitionRosterDto>().ProducesProblem(404).ProducesProblem(409);
 // Example: POST /api/admin/competitions/1/entries/2/roster
 g.MapPost("/",async(int competitionId,int teamEntryId,CompetitionRosterService s,CancellationToken ct)=>{var x=await s.CreateAsync(competitionId,teamEntryId,ct);return Results.Created($"/api/admin/competitions/{competitionId}/entries/{teamEntryId}/roster",x);}).Produces<CompetitionRosterDto>(201).ProducesProblem(404).ProducesProblem(409);
 // Example: PATCH /api/admin/competitions/1/entries/2/roster/status
 // Body: { "status": "Active" }
 g.MapPatch("/status",async(int competitionId,int teamEntryId,ChangeCompetitionRosterStatusRequest r,CompetitionRosterService s,CancellationToken ct)=>Results.Ok(await s.ChangeStatusAsync(competitionId,teamEntryId,r,ct))).Produces<CompetitionRosterDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
 // Example: POST /api/admin/competitions/1/entries/2/roster/players
 // Body: { "playerId": 10, "role": "Setter" }
 g.MapPost("/players",async(int competitionId,int teamEntryId,AddCompetitionRosterPlayerRequest r,CompetitionRosterService s,CancellationToken ct)=>Results.Ok(await s.AddPlayerAsync(competitionId,teamEntryId,r,ct))).Produces<CompetitionRosterDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
 // Example: PUT /api/admin/competitions/1/entries/2/roster/players/3
 // Body: { "role": "OutsideHitter" }
 g.MapPut("/players/{rosterPlayerId:int}",async(int competitionId,int teamEntryId,int rosterPlayerId,UpdateCompetitionRosterPlayerRequest r,CompetitionRosterService s,CancellationToken ct)=>Results.Ok(await s.UpdatePlayerAsync(competitionId,teamEntryId,rosterPlayerId,r,ct))).Produces<CompetitionRosterDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
 // Example: PATCH /api/admin/competitions/1/entries/2/roster/players/3/status
 // Body: { "status": "Inactive" }
 g.MapPatch("/players/{rosterPlayerId:int}/status",async(int competitionId,int teamEntryId,int rosterPlayerId,ChangeRosterMemberStatusRequest r,CompetitionRosterService s,CancellationToken ct)=>Results.Ok(await s.ChangePlayerStatusAsync(competitionId,teamEntryId,rosterPlayerId,r,ct))).Produces<CompetitionRosterDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
 // Example: POST /api/admin/competitions/1/entries/2/roster/staff
 // Body: { "coachId": 4 }
 g.MapPost("/staff",async(int competitionId,int teamEntryId,AddCompetitionRosterStaffRequest r,CompetitionRosterService s,CancellationToken ct)=>Results.Ok(await s.AddStaffAsync(competitionId,teamEntryId,r,ct))).Produces<CompetitionRosterDto>().ProducesProblem(404).ProducesProblem(409);
 // Example: PATCH /api/admin/competitions/1/entries/2/roster/staff/5/status
 // Body: { "status": "Inactive" }
 g.MapPatch("/staff/{rosterStaffId:int}/status",async(int competitionId,int teamEntryId,int rosterStaffId,ChangeRosterMemberStatusRequest r,CompetitionRosterService s,CancellationToken ct)=>Results.Ok(await s.ChangeStaffStatusAsync(competitionId,teamEntryId,rosterStaffId,r,ct))).Produces<CompetitionRosterDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);return e;}
}
