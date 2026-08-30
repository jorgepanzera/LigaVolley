using LigaVolley.Application.Clubs;
using LigaVolley.Application.Common;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class ClubEndpoints
{
    public static IEndpointRouteBuilder MapClubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/clubs").WithTags("Admin Clubs");

        // GET /api/admin/clubs?page=1&pageSize=20&search=bank&active=true
        group.MapGet("/",async(int? page,int? pageSize,string? search,bool? active,ClubService service,CancellationToken ct)=>{var result=await service.ListAsync(search,active,page??1,pageSize??20,ct);return Results.Ok(page.HasValue||pageSize.HasValue?result:result.Items);});

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
        // PUT /api/admin/clubs/1/logo
        // multipart/form-data: file=<club-logo.png>
        group.MapPut("/{id:int}/logo",async(int id,IFormFile? file,ClubService service,CancellationToken ct)=>{if(file is null)throw new RequestValidationException("club_logo_missing_file","A logo file is required.");await using var stream=file.OpenReadStream();return Results.Ok(await service.ReplaceLogoAsync(id,stream,file.ContentType,ct));}).DisableAntiforgery().Produces<ClubLogoDto>().ProducesProblem(400).ProducesProblem(404);
        // DELETE /api/admin/clubs/1/logo
        group.MapDelete("/{id:int}/logo",async(int id,ClubService service,CancellationToken ct)=>{await service.RemoveLogoAsync(id,ct);return Results.NoContent();}).Produces(204).ProducesProblem(404);
        return endpoints;
    }
    public static IEndpointRouteBuilder MapPublicClubLogoEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // GET /api/public/clubs/1/logo?v=2
        endpoints.MapGet("/api/public/clubs/{id:int}/logo",async(int id,ClubService service,HttpContext context,CancellationToken ct)=>{var logo=await service.OpenLogoAsync(id,ct);context.Response.Headers.CacheControl="public,max-age=31536000,immutable";return Results.Stream(logo.Content,logo.ContentType);}).WithTags("Public Assets").Produces(200).ProducesProblem(404);
        return endpoints;
    }
}
