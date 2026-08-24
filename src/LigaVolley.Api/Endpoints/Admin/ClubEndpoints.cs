using LigaVolley.Application.Clubs;
using LigaVolley.Application.Common;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class ClubEndpoints
{
    public static IEndpointRouteBuilder MapClubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/clubs").WithTags("Admin Clubs");

        // Example: GET /api/admin/clubs?active=true
        group.MapGet("/", async (bool? active, ClubService service, CancellationToken ct) => Results.Ok(await service.ListAsync(active, ct)));

        // Example: GET /api/admin/clubs/1
        group.MapGet("/{id:int}", async (int id, ClubService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)));

        // Example: POST /api/admin/clubs
        // Body: { "name": "Club Atlético", "shortName": "CA" }
        group.MapPost("/", async (CreateClubRequest request, ClubService service, CancellationToken ct) => { var result = await service.CreateAsync(request, ct); return Results.Created($"/api/admin/clubs/{result.ClubId}", result); });

        // Example: PUT /api/admin/clubs/1
        // Body: { "name": "Club Atlético Actualizado", "shortName": "CAA" }
        group.MapPut("/{id:int}", async (int id, UpdateClubRequest request, ClubService service, CancellationToken ct) => Results.Ok(await service.UpdateAsync(id, request, ct)));

        // Example: PATCH /api/admin/clubs/1/active
        // Body: { "active": false }
        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, ClubService service, CancellationToken ct) => Results.Ok(await service.SetActiveAsync(id, request.Active, ct)));
        return endpoints;
    }
}
