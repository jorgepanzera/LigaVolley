using LigaVolley.Application.CompetitionProgression;
using Microsoft.AspNetCore.Mvc;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class CompetitionProgressionEndpoints
{
    public static IEndpointRouteBuilder MapCompetitionProgressionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/competitions/{competitionId:int}").WithTags("Admin Competition Progression");

        // Example: GET /api/admin/competitions/25/progression
        group.MapGet("/progression", async (int competitionId, CompetitionProgressionService service, CancellationToken ct) =>
                Results.Ok(await service.GetProgressionAsync(competitionId, ct)))
            .WithName("GetAdminCompetitionProgression").WithSummary("Get current sporting progression for a Competition")
            .Produces<CompetitionProgressionDto>().Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");

        // Example: GET /api/admin/competitions/25/completion-preview
        group.MapGet("/completion-preview", async (int competitionId, CompetitionProgressionService service, CancellationToken ct) =>
                Results.Ok(await service.PreviewCompletionAsync(competitionId, ct)))
            .WithName("PreviewAdminCompetitionCompletion").WithSummary("Preview Competition completion and derived movements without persistence")
            .Produces<CompetitionCompletionPreviewDto>().Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");

        // Example: POST /api/admin/competitions/25/complete
        group.MapPost("/complete", async (int competitionId, CompetitionProgressionService service, CancellationToken ct) =>
                Results.Ok(await service.CompleteAsync(competitionId, ct)))
            .WithName("CompleteAdminCompetition").WithSummary("Complete a sportingly resolved Competition transactionally")
            .Produces<CompetitionCompletionResultDto>().Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
        return endpoints;
    }
}
