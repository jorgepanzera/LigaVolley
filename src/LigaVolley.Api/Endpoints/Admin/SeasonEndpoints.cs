using LigaVolley.Application.Common;
using LigaVolley.Application.Seasons;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class SeasonEndpoints
{
    public static IEndpointRouteBuilder MapSeasonEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/seasons").WithTags("Admin Seasons");

        group.MapGet("/", async (bool? active, short? year, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.ListAsync(active, year, cancellationToken)));

        group.MapGet("/{id:int}", async (int id, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAsync(id, cancellationToken)));

        group.MapPost("/", async (CreateSeasonRequest request, SeasonService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/admin/seasons/{result.SeasonId}", result);
        });

        group.MapPut("/{id:int}", async (int id, UpdateSeasonRequest request, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));

        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, SeasonService service, CancellationToken cancellationToken)
            => Results.Ok(await service.SetActiveAsync(id, request.Active, cancellationToken)));

        return endpoints;
    }
}
