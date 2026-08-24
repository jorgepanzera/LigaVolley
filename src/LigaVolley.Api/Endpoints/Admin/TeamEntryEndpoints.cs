using LigaVolley.Application.TeamEntries;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class TeamEntryEndpoints
{
    public static IEndpointRouteBuilder MapTeamEntryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/competitions/{competitionId:int}/entries").WithTags("Admin Team Entries");

        // Example: GET /api/admin/competitions/1/entries
        group.MapGet("/", async (int competitionId, TeamEntryService service, CancellationToken ct) => Results.Ok(await service.ListAsync(competitionId, ct)));

        // Example: POST /api/admin/competitions/1/entries
        // Body: { "teamId": 7, "seed": 1 }
        group.MapPost("/", async (int competitionId, AddTeamEntryRequest request, TeamEntryService service, CancellationToken ct) => { var result = await service.AddAsync(competitionId, request, ct); return Results.Created($"/api/admin/competitions/{competitionId}/entries/{result.TeamEntryId}", result); });

        // Example: PATCH /api/admin/competitions/1/entries/5/seed
        // Body: { "seed": 2 }
        group.MapPatch("/{entryId:int}/seed", async (int competitionId, int entryId, SetTeamEntrySeedRequest request, TeamEntryService service, CancellationToken ct) => Results.Ok(await service.SetSeedAsync(competitionId, entryId, request, ct)));

        // Example: PATCH /api/admin/competitions/1/entries/5/status
        // Body: { "status": "Active" }
        group.MapPatch("/{entryId:int}/status", async (int competitionId, int entryId, ChangeTeamEntryStatusRequest request, TeamEntryService service, CancellationToken ct) => Results.Ok(await service.ChangeStatusAsync(competitionId, entryId, request, ct)));

        // Example: DELETE /api/admin/competitions/1/entries/5
        group.MapDelete("/{entryId:int}", async (int competitionId, int entryId, TeamEntryService service, CancellationToken ct) => { await service.RemoveAsync(competitionId, entryId, ct); return Results.NoContent(); });
        return endpoints;
    }
}
