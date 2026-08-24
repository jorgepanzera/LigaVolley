using LigaVolley.Application.Common;
using LigaVolley.Application.Divisions;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class DivisionEndpoints
{
    public static IEndpointRouteBuilder MapDivisionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/divisions").WithTags("Admin Divisions");

        group.MapGet("/", async (Gender? gender, bool? active, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.ListAsync(gender, active, cancellationToken)));

        group.MapGet("/{id:int}", async (int id, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.GetAsync(id, cancellationToken)));

        group.MapPost("/", async (CreateDivisionRequest request, DivisionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/admin/divisions/{result.DivisionId}", result);
        });

        group.MapPut("/{id:int}", async (int id, UpdateDivisionRequest request, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));

        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, DivisionService service, CancellationToken cancellationToken)
            => Results.Ok(await service.SetActiveAsync(id, request.Active, cancellationToken)));

        return endpoints;
    }
}
