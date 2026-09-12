using LigaVolley.Application.ParticipantComposition;

namespace LigaVolley.Api.Endpoints.Admin;

internal static class ParticipantCompositionEndpoints
{
    public static IEndpointRouteBuilder MapParticipantCompositionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/competitions/{competitionId:int}").WithTags("Admin Competition Participants");
        // Example: GET /api/admin/competitions/1/participant-suggestion-sources
        group.MapGet("/participant-suggestion-sources", async (int competitionId, ParticipantCompositionService service, CancellationToken ct) => Results.Ok(await service.ListSourcesAsync(competitionId, ct)));
        // Example: PUT /api/admin/competitions/1/participant-suggestion-sources
        // Body: { "sourceCompetitionIds": [2, 7] }
        group.MapPut("/participant-suggestion-sources", async (int competitionId, ReplaceParticipantSuggestionSourcesRequest request, ParticipantCompositionService service, CancellationToken ct) => Results.Ok(await service.ReplaceSourcesAsync(competitionId, request, ct)));
        // Example: GET /api/admin/competitions/1/participant-composition
        group.MapGet("/participant-composition", async (int competitionId, ParticipantCompositionService service, CancellationToken ct) => Results.Ok(await service.GetAsync(competitionId, ct)));
        return endpoints;
    }
}
