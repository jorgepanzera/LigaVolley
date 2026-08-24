using LigaVolley.Application.Common;
using LigaVolley.Application.Seasons;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class SeasonEndpoints
{
    public static IEndpointRouteBuilder MapSeasonEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/seasons").WithTags("Admin Seasons");

        // Example: GET /api/admin/seasons?active=true&year=2026
        group.MapGet("/", async (bool? active, short? year, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.ListAsync(active, year, cancellationToken)));

        // Example: GET /api/admin/seasons/1
        group.MapGet("/{id:int}", async (int id, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAsync(id, cancellationToken)));

        // Example: POST /api/admin/seasons
        // Body: { "year": 2026, "name": "Season 2026", "startDate": "2026-01-01", "endDate": "2026-12-31" }
        group.MapPost("/", async (CreateSeasonRequest request, SeasonService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/admin/seasons/{result.SeasonId}", result);
        });

        // Example: PUT /api/admin/seasons/1
        // Body: { "year": 2026, "name": "Updated Season 2026", "startDate": "2026-02-01", "endDate": "2026-11-30" }
        group.MapPut("/{id:int}", async (int id, UpdateSeasonRequest request, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));

        // Example: PATCH /api/admin/seasons/1/active
        // Body: { "active": false }
        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.SetActiveAsync(id, request.Active, cancellationToken)));

        return endpoints;
    }
}
