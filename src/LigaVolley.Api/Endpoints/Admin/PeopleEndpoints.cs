using LigaVolley.Application.Common;using LigaVolley.Application.People;
namespace LigaVolley.Api.Endpoints.Admin;
internal static class PeopleEndpoints
{
 public static IEndpointRouteBuilder MapPeopleEndpoints(this IEndpointRouteBuilder e){var people=e.MapGroup("/api/admin/people").WithTags("Admin People");
 // Example: GET /api/admin/people?query=ana&page=1&pageSize=25
 people.MapGet("/",async(string? query,string? documentType,string? documentNumber,DateOnly? birthDate,bool? active,int page,int pageSize,PeopleService s,CancellationToken ct)=>Results.Ok(await s.ListPeopleAsync(query,documentType,documentNumber,birthDate,active,page,pageSize,ct))).Produces<PagedResult<PersonSummaryDto>>();
 // Example: GET /api/admin/people/1
 people.MapGet("/{personId:int}",async(int personId,PeopleService s,CancellationToken ct)=>Results.Ok(await s.GetPersonAsync(personId,ct))).Produces<PersonDto>().ProducesProblem(404);
 // Example: POST /api/admin/people
 // Body: { "documentType": "CI", "documentNumber": "1.234.567-8", "firstName": "Ana", "lastName": "Pérez", "birthDate": "2000-05-10", "gender": "Female", "email": "ana@example.com", "phone": "099123456" }
 people.MapPost("/",async(CreatePersonRequest r,PeopleService s,CancellationToken ct)=>{var x=await s.CreatePersonAsync(r,ct);return Results.Created($"/api/admin/people/{x.PersonId}",x);}).Produces<PersonDto>(201).ProducesProblem(400).ProducesProblem(409);
 // Example: PUT /api/admin/people/1
 // Body: { "documentType": "CI", "documentNumber": "1.234.567-8", "firstName": "Ana María", "lastName": "Pérez", "birthDate": "2000-05-10", "gender": "Female", "email": "ana@example.com", "phone": "099123456" }
 people.MapPut("/{personId:int}",async(int personId,UpdatePersonRequest r,PeopleService s,CancellationToken ct)=>Results.Ok(await s.UpdatePersonAsync(personId,r,ct))).Produces<PersonDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
 // Example: PATCH /api/admin/people/1/active
 // Body: { "active": false }
 people.MapPatch("/{personId:int}/active",async(int personId,SetActiveRequest r,PeopleService s,CancellationToken ct)=>Results.Ok(await s.SetPersonActiveAsync(personId,r.Active,ct))).Produces<PersonDto>().ProducesProblem(404);
 // Example: GET /api/admin/people/1/additional-documents
 people.MapGet("/{personId:int}/additional-documents",async(int personId,PeopleService s,CancellationToken ct)=>Results.Ok(await s.ListDocumentsAsync(personId,ct))).Produces<IReadOnlyList<PersonAdditionalDocumentDto>>().ProducesProblem(404);
 // Example: POST /api/admin/people/1/additional-documents
 // Body: { "documentType": "HealthCard", "documentNumber": "HC-100", "validFrom": "2026-01-01", "validTo": "2027-01-01", "notes": "Control anual" }
 people.MapPost("/{personId:int}/additional-documents",async(int personId,CreatePersonAdditionalDocumentRequest r,PeopleService s,CancellationToken ct)=>{var x=await s.AddDocumentAsync(personId,r,ct);return Results.Created($"/api/admin/people/{personId}/additional-documents/{x.PersonAdditionalDocumentId}",x);}).Produces<PersonAdditionalDocumentDto>(201).ProducesProblem(400).ProducesProblem(404);
 // Example: PUT /api/admin/people/1/additional-documents/1
 // Body: { "documentType": "HealthCard", "documentNumber": "HC-100", "validFrom": "2026-01-01", "validTo": "2027-12-31", "notes": "Renovado" }
 people.MapPut("/{personId:int}/additional-documents/{documentId:int}",async(int personId,int documentId,UpdatePersonAdditionalDocumentRequest r,PeopleService s,CancellationToken ct)=>Results.Ok(await s.UpdateDocumentAsync(personId,documentId,r,ct))).Produces<PersonAdditionalDocumentDto>().ProducesProblem(400).ProducesProblem(404);
 // Example: PATCH /api/admin/people/1/additional-documents/1/active
 // Body: { "active": false }
 people.MapPatch("/{personId:int}/additional-documents/{documentId:int}/active",async(int personId,int documentId,SetActiveRequest r,PeopleService s,CancellationToken ct)=>Results.Ok(await s.SetDocumentActiveAsync(personId,documentId,r.Active,ct))).Produces<PersonAdditionalDocumentDto>().ProducesProblem(404);
 // Example: POST /api/admin/people/1/player
 people.MapPost("/{personId:int}/player",async(int personId,PeopleService s,CancellationToken ct)=>{var x=await s.CreatePlayerAsync(personId,ct);return Results.Created($"/api/admin/players/{x.PlayerId}",x);}).Produces<PlayerDto>(201).ProducesProblem(404).ProducesProblem(409);
 // Example: POST /api/admin/people/1/coach
 people.MapPost("/{personId:int}/coach",async(int personId,PeopleService s,CancellationToken ct)=>{var x=await s.CreateCoachAsync(personId,ct);return Results.Created($"/api/admin/coaches/{x.CoachId}",x);}).Produces<CoachDto>(201).ProducesProblem(404).ProducesProblem(409);
 // Example: POST /api/admin/people/1/referee
 people.MapPost("/{personId:int}/referee",async(int personId,PeopleService s,CancellationToken ct)=>{var x=await s.CreateRefereeAsync(personId,ct);return Results.Created($"/api/admin/referees/{x.RefereeId}",x);}).Produces<RefereeDto>(201).ProducesProblem(404).ProducesProblem(409);
 MapProfiles(e);return e;}
 private static void MapProfiles(IEndpointRouteBuilder e){var players=e.MapGroup("/api/admin/players").WithTags("Admin Players");
 // Example: GET /api/admin/players?query=ana&active=true&page=1&pageSize=25
 players.MapGet("/",async(string? query,bool? active,int page,int pageSize,PeopleService s,CancellationToken ct)=>Results.Ok(await s.ListPlayersAsync(query,active,page,pageSize,ct))).Produces<PagedResult<PlayerDto>>();
 // Example: GET /api/admin/players/1
 players.MapGet("/{playerId:int}",async(int playerId,PeopleService s,CancellationToken ct)=>Results.Ok(await s.GetPlayerAsync(playerId,ct))).Produces<PlayerDto>().ProducesProblem(404);
 // Example: PATCH /api/admin/players/1/active
 // Body: { "active": false }
 players.MapPatch("/{playerId:int}/active",async(int playerId,SetActiveRequest r,PeopleService s,CancellationToken ct)=>Results.Ok(await s.SetPlayerActiveAsync(playerId,r.Active,ct))).Produces<PlayerDto>();var coaches=e.MapGroup("/api/admin/coaches").WithTags("Admin Coaches");
 // Example: GET /api/admin/coaches?query=ana&active=true&page=1&pageSize=25
 coaches.MapGet("/",async(string? query,bool? active,int page,int pageSize,PeopleService s,CancellationToken ct)=>Results.Ok(await s.ListCoachesAsync(query,active,page,pageSize,ct))).Produces<PagedResult<CoachDto>>();
 // Example: GET /api/admin/coaches/1
 coaches.MapGet("/{coachId:int}",async(int coachId,PeopleService s,CancellationToken ct)=>Results.Ok(await s.GetCoachAsync(coachId,ct))).Produces<CoachDto>();
 // Example: PATCH /api/admin/coaches/1/active
 // Body: { "active": false }
 coaches.MapPatch("/{coachId:int}/active",async(int coachId,SetActiveRequest r,PeopleService s,CancellationToken ct)=>Results.Ok(await s.SetCoachActiveAsync(coachId,r.Active,ct))).Produces<CoachDto>();var refs=e.MapGroup("/api/admin/referees").WithTags("Admin Referees");
 // Example: GET /api/admin/referees?query=ana&active=true&page=1&pageSize=25
 refs.MapGet("/",async(string? query,bool? active,int page,int pageSize,PeopleService s,CancellationToken ct)=>Results.Ok(await s.ListRefereesAsync(query,active,page,pageSize,ct))).Produces<PagedResult<RefereeDto>>();
 // Example: GET /api/admin/referees/1
 refs.MapGet("/{refereeId:int}",async(int refereeId,PeopleService s,CancellationToken ct)=>Results.Ok(await s.GetRefereeAsync(refereeId,ct))).Produces<RefereeDto>();
 // Example: PATCH /api/admin/referees/1/active
 // Body: { "active": false }
 refs.MapPatch("/{refereeId:int}/active",async(int refereeId,SetActiveRequest r,PeopleService s,CancellationToken ct)=>Results.Ok(await s.SetRefereeActiveAsync(refereeId,r.Active,ct))).Produces<RefereeDto>();}
}
