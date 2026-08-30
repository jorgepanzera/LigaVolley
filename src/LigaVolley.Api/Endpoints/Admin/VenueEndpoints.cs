using LigaVolley.Application.Common;
using LigaVolley.Application.Venues;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class VenueEndpoints
{
    public static IEndpointRouteBuilder MapVenueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/venues").WithTags("Admin Venues");

        // GET /api/admin/venues?page=1&pageSize=20&search=central&active=true
        group.MapGet("/",async(int? page,int? pageSize,string? search,bool? active,VenueService service,CancellationToken ct)=>{var result=await service.ListAsync(search,active,page??1,pageSize??20,ct);return Results.Ok(page.HasValue||pageSize.HasValue?result:result.Items);});

        // Example: GET /api/admin/venues/1
        group.MapGet("/{id:int}", async (int id, VenueService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)));

        // Example: POST /api/admin/venues
        // Body: { "name": "Gimnasio Central", "address": "Av. Principal 1234" }
        group.MapPost("/", async (CreateVenueRequest request, VenueService service, CancellationToken ct) => { var result = await service.CreateAsync(request, ct); return Results.Created($"/api/admin/venues/{result.VenueId}", result); });

        // Example: PUT /api/admin/venues/1
        // Body: { "name": "Gimnasio Central", "address": "Av. Principal 1500" }
        group.MapPut("/{id:int}", async (int id, UpdateVenueRequest request, VenueService service, CancellationToken ct) => Results.Ok(await service.UpdateAsync(id, request, ct)));

        // Example: PATCH /api/admin/venues/1/active
        // Body: { "active": false }
        group.MapPatch("/{id:int}/active", async (int id, SetActiveRequest request, VenueService service, CancellationToken ct) => Results.Ok(await service.SetActiveAsync(id, request.Active, ct)));
        return endpoints;
    }
}
