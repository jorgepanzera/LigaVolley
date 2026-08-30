using LigaVolley.Application.Matches;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/matches").WithTags("Admin Matches");

        // Example: GET /api/admin/matches/125
        group.MapGet("/{matchId:int}", async (int matchId, MatchAdminService service, CancellationToken ct) => Results.Ok(await service.GetAsync(matchId, ct)));

        // Example: PUT /api/admin/matches/125/schedule
        // Body: { "matchDate": "2026-09-12T19:30:00-03:00", "venueId": 8 }
        group.MapPut("/{matchId:int}/schedule", async (int matchId, ScheduleMatchRequest request, MatchAdminService service, CancellationToken ct) => Results.Ok(await service.ScheduleAsync(matchId, request, ct)));

        // GET /api/admin/matches/123/readiness
        group.MapGet("/{matchId:int}/readiness", async (int matchId, MatchOperationsService service, CancellationToken ct) => Results.Ok(await service.GetReadinessAsync(matchId, ct)))
            .WithSummary("Evaluate whether Scorer can open the MatchSheet")
            .Produces<MatchReadinessDto>().ProducesProblem(404).ProducesProblem(409);

        // GET /api/admin/matches/123/match-sheet
        group.MapGet("/{matchId:int}/match-sheet", async (int matchId, MatchOperationsService service, CancellationToken ct) => Results.Ok(await service.GetMatchSheetAsync(matchId, ct)))
            .WithSummary("Get the read-only Admin MatchSheet oversight projection")
            .Produces<AdminMatchSheetDto>().ProducesProblem(404).ProducesProblem(409);

        return endpoints;
    }
}
