using LigaVolley.Application.Competitions;
using LigaVolley.Domain.Competitions;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class CompetitionEndpoints
{
    public static IEndpointRouteBuilder MapCompetitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/competitions").WithTags("Admin Competitions");

        // Example: GET /api/admin/competitions?seasonId=1&divisionId=1&status=Draft
        group.MapGet("/", async (int? seasonId, int? divisionId, CompetitionStatus? status, CompetitionService service, CancellationToken ct) => Results.Ok(await service.ListAsync(seasonId, divisionId, status, ct)));

        // Example: GET /api/admin/competitions/1
        group.MapGet("/{id:int}", async (int id, CompetitionService service, CancellationToken ct) => Results.Ok(await service.GetAsync(id, ct)));

        // Example: POST /api/admin/competitions
        // Body: { "name": "Apertura 2026", "seasonId": 1, "divisionId": 1, "periodType": "Opening", "startDate": "2026-03-01", "endDate": "2026-06-30", "structureSource": { "type": "Format", "competitionFormatId": 1, "sourceCompetitionId": null } }
        group.MapPost("/", async (CreateCompetitionRequest request, CompetitionService service, CancellationToken ct) => { var result = await service.CreateAsync(request, ct); return Results.Created($"/api/admin/competitions/{result.CompetitionId}", result); });

        // Example: PUT /api/admin/competitions/1
        // Body: { "name": "Apertura 2026 actualizado", "periodType": "Opening", "startDate": "2026-03-01", "endDate": "2026-06-30" }
        group.MapPut("/{id:int}", async (int id, UpdateCompetitionRequest request, CompetitionService service, CancellationToken ct) => Results.Ok(await service.UpdateAsync(id, request, ct)));

        // Example: PATCH /api/admin/competitions/1/status
        // Body: { "status": "Cancelled" }
        group.MapPatch("/{id:int}/status", async (int id, ChangeCompetitionStatusRequest request, CompetitionService service, CancellationToken ct) => Results.Ok(await service.ChangeStatusAsync(id, request, ct)));

        // Example: GET /api/admin/competitions/1/structure
        group.MapGet("/{id:int}/structure", async (int id, CompetitionService service, CancellationToken ct) => Results.Ok(await service.GetStructureAsync(id, ct)));

        // Example: GET /api/admin/competitions/1/schedule-preview
        group.MapGet("/{id:int}/schedule-preview", async (int id, CompetitionSchedulingService service, CancellationToken ct) => Results.Ok(await service.PreviewAsync(id, ct)))
            .Produces<CompetitionSchedulePreviewDto>().ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/admin/competitions/1/schedule
        group.MapPost("/{id:int}/schedule", async (int id, CompetitionSchedulingService service, CancellationToken ct) => Results.Ok(await service.ScheduleAsync(id, ct)))
            .Produces<CompetitionScheduleResultDto>().ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }
}
