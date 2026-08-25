using LigaVolley.Application.Standings;
using Microsoft.AspNetCore.Mvc;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class StandingsEndpoints
{
    public static IEndpointRouteBuilder MapStandingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/competitions/{competitionId:int}/phases/{phaseId:int}/standings").WithTags("Admin Standings");

        // Example: GET /api/admin/competitions/25/phases/81/standings?phaseGroupId=12
        group.MapGet("/", async (int competitionId, int phaseId, int? phaseGroupId, StandingsService service, CancellationToken ct)
                => Results.Ok(await service.GetAsync(competitionId, phaseId, phaseGroupId, ct)))
            .WithName("GetAdminStandings")
            .WithSummary("Calculate standings for a competition phase or phase group")
            .WithDescription("Calculates standings on demand from finished matches, match sets, and competition-format scoring and tiebreak rules.")
            .Produces<StandingsDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");
        return endpoints;
    }
}
