using LigaVolley.Application.Common;
using LigaVolley.Application.CompetitionFormats;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class CompetitionFormatEndpoints
{
    public static IEndpointRouteBuilder MapCompetitionFormatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/competition-formats").WithTags("Admin Competition Formats");

        // Example: GET /api/admin/competition-formats?active=true&teamCount=10
        group.MapGet("/", async (bool? active, short? teamCount, CompetitionFormatService service, CancellationToken ct) => Results.Ok(await service.ListAsync(active, teamCount, ct))).Produces<CompetitionFormatSummaryDto[]>();

        // Example: GET /api/admin/competition-formats/1
        group.MapGet("/{id:int}", async (int id, CompetitionFormatService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct))).Produces<CompetitionFormatDto>().ProducesProblem(404);

        // Example: POST /api/admin/competition-formats
        // Body: { "code": "RR8", "name": "Round Robin 8", "description": null, "minTeams": 8, "maxTeams": 8, "definition": { "phases": [{ "code": "REGULAR", "name": "Regular", "phaseType": "RoundRobin", "phaseRole": "Regular", "sequence": 1, "rounds": 2, "fixtureMode": "MirroredHomeAway", "groups": [], "series": [] }], "qualificationRules": [], "scoringRules": [], "tiebreakRules": [], "movementRules": [] } }
        group.MapPost("/", async (CreateCompetitionFormatRequest request, CompetitionFormatService service, CancellationToken ct) => { var result = await service.CreateAsync(request, ct); return Results.Created($"/api/admin/competition-formats/{result.CompetitionFormatId}", result); }).Produces<CompetitionFormatDto>(201).ProducesProblem(400).ProducesProblem(409);

        // Example: PUT /api/admin/competition-formats/1
        // Body: { "code": "RR8", "name": "Updated Round Robin 8", "description": null, "minTeams": 8, "maxTeams": 8, "definition": { "phases": [{ "code": "REGULAR", "name": "Regular", "phaseType": "RoundRobin", "phaseRole": "Regular", "sequence": 1, "rounds": 2, "fixtureMode": "MirroredHomeAway", "groups": [], "series": [] }], "qualificationRules": [], "scoringRules": [], "tiebreakRules": [], "movementRules": [] } }
        group.MapPut("/{id:int}", async (int id, UpdateCompetitionFormatRequest request, CompetitionFormatService service, CancellationToken ct) => Results.Ok(await service.UpdateAsync(id, request, ct))).Produces<CompetitionFormatDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/admin/competition-formats/1/clone
        // Body: { "code": "RR8_V2", "name": "Round Robin 8 v2", "description": "Independent variant" }
        group.MapPost("/{id:int}/clone", async (int id, CloneCompetitionFormatRequest request, CompetitionFormatService service, CancellationToken ct) => { var result = await service.CloneAsync(id, request, ct); return Results.Created($"/api/admin/competition-formats/{result.CompetitionFormatId}", result); }).Produces<CompetitionFormatDto>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: PATCH /api/admin/competition-formats/1/active
        // Body: { "active": false }
        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, CompetitionFormatService service, CancellationToken ct) => Results.Ok(await service.SetActiveAsync(id, request.Active, ct))).Produces<CompetitionFormatDto>().ProducesProblem(400).ProducesProblem(404);

        // Example: POST /api/admin/competition-formats/validate
        // Body: { "minTeams": 8, "maxTeams": 8, "definition": { "phases": [{ "code": "REGULAR", "name": "Regular", "phaseType": "RoundRobin", "phaseRole": "Regular", "sequence": 1, "rounds": 2, "fixtureMode": "MirroredHomeAway", "groups": [], "series": [] }], "qualificationRules": [], "scoringRules": [], "tiebreakRules": [], "movementRules": [] } }
        group.MapPost("/validate", async (ValidateCompetitionFormatRequest request, CompetitionFormatService service) => Results.Ok(await service.ValidateAsync(request))).Produces<CompetitionFormatValidationDto>().ProducesProblem(400);
        return endpoints;
    }
}
