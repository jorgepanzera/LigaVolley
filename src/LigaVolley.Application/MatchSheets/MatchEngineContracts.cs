using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.MatchSheets;

public sealed record SetLineupRequest(int P1MatchPlayerId,int P2MatchPlayerId,int P3MatchPlayerId,int P4MatchPlayerId,int P5MatchPlayerId,int P6MatchPlayerId,int? LiberoMatchPlayerId=null,IReadOnlyList<byte>? LiberoLogicalPositions=null)
{ public int[] Players()=>[P1MatchPlayerId,P2MatchPlayerId,P3MatchPlayerId,P4MatchPlayerId,P5MatchPlayerId,P6MatchPlayerId]; }
public sealed record StartSetRequest(MatchSide InitialServingSide);
public sealed record AddPointRequest(Guid PointUuid,MatchSide WinningSide);
public sealed record CorrectLastPointRequest(Guid CorrectionUuid);
public sealed record AddSubstitutionRequest(Guid SubstitutionUuid,int PlayerOutMatchPlayerId,int PlayerInMatchPlayerId);
public sealed record LiberoEnterRequest(Guid EventUuid,int LiberoMatchPlayerId,int ReplacedMatchPlayerId);
public sealed record LiberoExitRequest(Guid EventUuid,int LiberoMatchPlayerId);
public sealed record AddTimeoutRequest(Guid TimeoutUuid,MatchSide Side);
public sealed record CloseMatchRequest(Guid CloseUuid);
public sealed record CourtPositionDto(LineupPosition LogicalLineupPosition,LineupPosition PhysicalPosition,int EffectiveMatchPlayerId,bool IsLiberoReplacement);
public sealed record MatchSetStateDto(byte SetNumber,MatchSetStatus SetStatus,short HomePoints,short AwayPoints,byte HomeSets,byte AwaySets,MatchSide? InitialServingSide,MatchSide? CurrentServingSide,int? ServerMatchPlayerId,byte HomeRotationOffset,byte AwayRotationOffset,byte HomeTimeouts,byte AwayTimeouts,MatchSide? WinnerSide,bool MatchDecided,IReadOnlyList<CourtPositionDto> HomeCourtState,IReadOnlyList<CourtPositionDto> AwayCourtState);
public sealed record MatchEngineCommandResult(bool AlreadyApplied,MatchSetStateDto State);
public sealed record CloseMatchResult(bool AlreadyClosed,int MatchId,MatchSheetStatus MatchSheetStatus,MatchStatus MatchStatus,byte HomeSets,byte AwaySets,int WinnerTeamEntryId);
