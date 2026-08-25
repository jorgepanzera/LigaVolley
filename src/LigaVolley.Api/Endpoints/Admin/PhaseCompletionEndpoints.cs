using LigaVolley.Application.PhaseCompletion;
using Microsoft.AspNetCore.Mvc;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class PhaseCompletionEndpoints
{
    public static IEndpointRouteBuilder MapPhaseCompletionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/admin/competitions/{competitionId:int}/phases/{phaseId:int}").WithTags("Admin Phase Completion");

        // Example: GET /api/admin/competitions/25/phases/81/completion-preview
        group.MapGet("/completion-preview",async(int competitionId,int phaseId,PhaseCompletionService service,CancellationToken ct)=>Results.Ok(await service.PreviewAsync(competitionId,phaseId,ct)))
            .WithName("PreviewAdminPhaseCompletion").WithSummary("Preview completion of a table phase without persistence")
            .Produces<PhaseCompletionPreviewDto>().Produces<ProblemDetails>(404,"application/problem+json").Produces<ProblemDetails>(409,"application/problem+json");

        // Example: POST /api/admin/competitions/25/phases/81/complete
        group.MapPost("/complete",async(int competitionId,int phaseId,PhaseCompletionService service,CancellationToken ct)=>Results.Ok(await service.CompleteAsync(competitionId,phaseId,ct)))
            .WithName("CompleteAdminPhase").WithSummary("Complete a table phase and materialize qualification effects transactionally")
            .Produces<PhaseCompletionResultDto>().Produces<ProblemDetails>(404,"application/problem+json").Produces<ProblemDetails>(409,"application/problem+json");
        return endpoints;
    }
}
