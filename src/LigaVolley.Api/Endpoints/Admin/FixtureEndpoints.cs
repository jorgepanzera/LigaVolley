using LigaVolley.Application.Fixtures;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class FixtureEndpoints
{
    public static IEndpointRouteBuilder MapFixtureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/competitions/{competitionId:int}/fixture").WithTags("Admin Fixture");

        // Example: POST /api/admin/competitions/25/fixture/generate
        // Body: { "randomSeed": 12345 }
        group.MapPost("/generate", async (int competitionId, GenerateFixtureRequest request, FixtureService service, CancellationToken ct) => Results.Ok(await service.GenerateInitialAsync(competitionId, request, ct)));

        // Example: GET /api/admin/competitions/25/fixture
        group.MapGet("/", async (int competitionId, FixtureService service, CancellationToken ct) => Results.Ok(await service.GetAsync(competitionId, ct)));
        return endpoints;
    }
}
