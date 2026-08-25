using LigaVolley.Application.MatchSheets;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Api.Endpoints.Scorer;

internal static class ScorerMatchEngineEndpoints
{
    public static IEndpointRouteBuilder MapScorerMatchEngineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/api/scorer/matches/{matchId:int}").WithTags("Scorer Match Engine");

        // Example: POST /api/scorer/matches/123/sets/prepare
        group.MapPost("/sets/prepare",async(int matchId,MatchEngineService service,CancellationToken ct)=>{var x=await service.PrepareSetAsync(matchId,ct);return x.AlreadyApplied?Results.Ok(x):Results.Created($"/api/scorer/matches/{matchId}/sets/{x.State.SetNumber}",x);}).Produces<MatchEngineCommandResult>(201).Produces<MatchEngineCommandResult>(200).ProducesProblem(404).ProducesProblem(409);

        // Example: PUT /api/scorer/matches/123/sets/1/lineups/HOME
        // Body: { "p1MatchPlayerId": 101, "p2MatchPlayerId": 105, "p3MatchPlayerId": 103, "p4MatchPlayerId": 108, "p5MatchPlayerId": 102, "p6MatchPlayerId": 106 }
        group.MapPut("/sets/{setNumber:int}/lineups/{side}",async(int matchId,byte setNumber,MatchSide side,SetLineupRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.SaveLineupAsync(matchId,setNumber,side,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/sets/1/start
        // Body: { "initialServingSide": "HOME" }
        group.MapPost("/sets/{setNumber:int}/start",async(int matchId,byte setNumber,StartSetRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.StartSetAsync(matchId,setNumber,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/sets/1/points
        // Body: { "pointUuid": "0b757aaf-1455-480b-8558-024ac051c05e", "winningSide": "HOME" }
        group.MapPost("/sets/{setNumber:int}/points",async(int matchId,byte setNumber,AddPointRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.AddPointAsync(matchId,setNumber,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/sets/1/points/correct-last
        // Body: { "correctionUuid": "917efc03-a4bd-44b0-a6fb-e941f7113a46" }
        group.MapPost("/sets/{setNumber:int}/points/correct-last",async(int matchId,byte setNumber,CorrectLastPointRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.CorrectLastPointAsync(matchId,setNumber,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/sets/1/substitutions
        // Body: { "substitutionUuid": "6b6db725-b93f-41be-b431-f21b1a2673e0", "playerOutMatchPlayerId": 101, "playerInMatchPlayerId": 109 }
        group.MapPost("/sets/{setNumber:int}/substitutions",async(int matchId,byte setNumber,AddSubstitutionRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.SubstituteAsync(matchId,setNumber,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/sets/1/libero/enter
        // Body: { "eventUuid": "865139a2-05c8-47b2-a25e-6dd624850847", "liberoMatchPlayerId": 112, "replacedMatchPlayerId": 108 }
        group.MapPost("/sets/{setNumber:int}/libero/enter",async(int matchId,byte setNumber,LiberoEnterRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.EnterLiberoAsync(matchId,setNumber,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/sets/1/libero/exit
        // Body: { "eventUuid": "0d73a738-8281-460a-a183-50c15549fc37", "liberoMatchPlayerId": 112 }
        group.MapPost("/sets/{setNumber:int}/libero/exit",async(int matchId,byte setNumber,LiberoExitRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.ExitLiberoAsync(matchId,setNumber,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/sets/1/timeouts
        // Body: { "timeoutUuid": "4bc868ec-5d72-4995-8765-38df5310ecc2", "side": "AWAY" }
        group.MapPost("/sets/{setNumber:int}/timeouts",async(int matchId,byte setNumber,AddTimeoutRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.TimeoutAsync(matchId,setNumber,request,ct))).Produces<MatchEngineCommandResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Example: POST /api/scorer/matches/123/close
        // Body: { "closeUuid": "7f88581e-acbe-4232-8917-f6a94011b3d3" }
        group.MapPost("/close",async(int matchId,CloseMatchRequest request,MatchEngineService service,CancellationToken ct)=>Results.Ok(await service.CloseAsync(matchId,request,ct))).Produces<CloseMatchResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return endpoints;
    }
}
