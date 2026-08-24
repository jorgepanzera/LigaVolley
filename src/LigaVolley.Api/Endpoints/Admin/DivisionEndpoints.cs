using LigaVolley.Application.Common;
using LigaVolley.Application.Divisions;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class DivisionEndpoints
{
    public static IEndpointRouteBuilder MapDivisionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/divisions").WithTags("Admin Divisions");

        // Example: GET /api/admin/divisions?gender=Female&active=true
        group.MapGet("/", async (Gender? gender, bool? active, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.ListAsync(gender, active, cancellationToken)));

        // Example: GET /api/admin/divisions/1
        group.MapGet("/{id:int}", async (int id, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAsync(id, cancellationToken)));

        // Example: POST /api/admin/divisions
        // Body: { "name": "B Femenina", "levelOrder": 2, "gender": "Female" }
        group.MapPost("/", async (CreateDivisionRequest request, DivisionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/admin/divisions/{result.DivisionId}", result);
        });

        // Example: PUT /api/admin/divisions/1
        // Body: { "name": "A Femenina", "levelOrder": 1, "gender": "Female" }
        group.MapPut("/{id:int}", async (int id, UpdateDivisionRequest request, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));

        // Example: PATCH /api/admin/divisions/1/active
        // Body: { "active": false }
        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.SetActiveAsync(id, request.Active, cancellationToken)));

        return endpoints;
    }
}
