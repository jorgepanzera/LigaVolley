using LigaVolley.Application.Common;
using LigaVolley.Application.Teams;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/teams").WithTags("Admin Teams");

        // Example: GET /api/admin/teams?clubId=1&gender=Female&active=true
        group.MapGet("/", async (int? clubId, Gender? gender, bool? active, TeamService service, CancellationToken ct) => Results.Ok(await service.ListAsync(clubId, gender, active, ct)));

        // Example: GET /api/admin/teams/1
        group.MapGet("/{id:int}", async (int id, TeamService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)));

        // Example: POST /api/admin/teams
        // Body: { "name": "Club Atlético Femenino", "gender": "Female", "clubId": 1 }
        group.MapPost("/", async (CreateTeamRequest request, TeamService service, CancellationToken ct) => { var result = await service.CreateAsync(request, ct); return Results.Created($"/api/admin/teams/{result.TeamId}", result); });

        // Example: PUT /api/admin/teams/1
        // Body: { "name": "Club Atlético Primera", "gender": "Female", "clubId": 1 }
        group.MapPut("/{id:int}", async (int id, UpdateTeamRequest request, TeamService service, CancellationToken ct) => Results.Ok(await service.UpdateAsync(id, request, ct)));

        // Example: PATCH /api/admin/teams/1/active
        // Body: { "active": false }
        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, TeamService service, CancellationToken ct) => Results.Ok(await service.SetActiveAsync(id, request.Active, ct)));
        return endpoints;
    }
}
