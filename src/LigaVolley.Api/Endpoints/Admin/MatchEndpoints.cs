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

        return endpoints;
    }
}
