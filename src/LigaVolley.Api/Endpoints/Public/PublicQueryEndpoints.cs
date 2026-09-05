using LigaVolley.Application.PublicQueries;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using Microsoft.AspNetCore.Mvc;

namespace LigaVolley.Api.Endpoints.Public;

public static class PublicQueryEndpoints
{
    public static IEndpointRouteBuilder MapPublicQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/public").WithTags("Public Query");

        // GET /api/public/seasons
        group.MapGet("/seasons",async(PublicQueryService service,CancellationToken ct)=>Results.Ok(await service.ListSeasonsAsync(ct)))
            .WithSummary("List public seasons").WithDescription("Returns seasons containing at least one public competition, newest first.").Produces<IReadOnlyList<PublicSeasonDto>>();

        // GET /api/public/competitions?seasonId=1&divisionId=2&gender=Female&status=Scheduled
        group.MapGet("/competitions",async(int? seasonId,int? divisionId,Gender? gender,CompetitionStatus? status,PublicQueryService service,CancellationToken ct)=>Results.Ok(await service.ListCompetitionsAsync(seasonId,divisionId,gender,status,ct)))
            .WithSummary("List public competitions").WithDescription("Anonymous public catalogue filtered by season, division, gender and public status.").Produces<IReadOnlyList<PublicCompetitionSummaryDto>>().Produces<ProblemDetails>(400,"application/problem+json");

        // GET /api/public/competitions/123
        group.MapGet("/competitions/{competitionId:int}",async(int competitionId,PublicQueryService service,CancellationToken ct)=>Results.Ok(await service.GetCompetitionAsync(competitionId,ct)))
            .WithSummary("Get public competition").WithDescription("Returns teams and ordered sporting structure, including playoff bracket series.").Produces<PublicCompetitionDto>().Produces<ProblemDetails>(404,"application/problem+json").Produces<ProblemDetails>(409,"application/problem+json");

        // GET /api/public/competitions/123/fixture?phaseId=10&phaseGroupId=20&teamEntryId=30&status=Scheduled&status=Finished
        group.MapGet("/competitions/{competitionId:int}/fixture",async(int competitionId,int? phaseId,int? phaseGroupId,int? teamEntryId,[FromQuery]MatchStatus[]? status,PublicQueryService service,CancellationToken ct)=>Results.Ok(await service.GetFixtureAsync(competitionId,phaseId,phaseGroupId,teamEntryId,status?.ToHashSet(),ct)))
            .WithSummary("Get public fixture and results").WithDescription("Returns materialized matches grouped by phase, group, round or playoff series.").Produces<PublicCompetitionFixtureDto>().Produces<ProblemDetails>(400,"application/problem+json").Produces<ProblemDetails>(404,"application/problem+json").Produces<ProblemDetails>(409,"application/problem+json");

        // GET /api/public/competitions/123/standings?phaseId=10&phaseGroupId=20
        group.MapGet("/competitions/{competitionId:int}/standings",async(int competitionId,int? phaseId,int? phaseGroupId,PublicQueryService service,CancellationToken ct)=>Results.Ok(await service.GetStandingsAsync(competitionId,phaseId,phaseGroupId,ct)))
            .WithSummary("Get public standings").WithDescription("Returns canonical backend standings as independent phase/group tables; playoff phases do not produce tables.").Produces<PublicCompetitionStandingsDto>().Produces<ProblemDetails>(400,"application/problem+json").Produces<ProblemDetails>(404,"application/problem+json").Produces<ProblemDetails>(409,"application/problem+json");

        // GET /api/public/matches/456
        group.MapGet("/matches/{matchId:int}",async(int matchId,PublicQueryService service,CancellationToken ct)=>Results.Ok(await service.GetMatchAsync(matchId,ct)))
            .WithSummary("Get public match detail").WithDescription("Returns identity, sporting scope, schedule and final result when available.").Produces<PublicMatchDto>().Produces<ProblemDetails>(404,"application/problem+json").Produces<ProblemDetails>(409,"application/problem+json");

        // GET /api/public/matches/456/live
        group.MapGet("/matches/{matchId:int}/live",async(int matchId,PublicQueryService service,CancellationToken ct)=>Results.Ok(await service.GetLiveAsync(matchId,ct)))
            .WithSummary("Get central public livescore").WithDescription("Returns the latest accepted central operational state, effective P1..P6 courts and server-side freshness timestamps. servingSide remains explicit. Nullable servingPlayer contains only jerseyNumber (integer) and displayName, resolved by the canonical backend server calculator during an IN_PROGRESS match and set; it is null between sets, in READY, SUSPENDED, FINISHED or when no server can be determined. LastUpdatedAt may be null for historical data and never advances on GET. PENDING, SCHEDULED and CANCELLED have no Live (404 public_live_match_not_available).").Produces<PublicLiveMatchDto>().Produces<ProblemDetails>(404,"application/problem+json").Produces<ProblemDetails>(409,"application/problem+json");
        return app;
    }
}
