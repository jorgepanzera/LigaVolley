using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.MatchSheets;

public sealed record SetLineupRequest(int P1MatchPlayerId,int P2MatchPlayerId,int P3MatchPlayerId,int P4MatchPlayerId,int P5MatchPlayerId,int P6MatchPlayerId,int? LiberoMatchPlayerId=null,IReadOnlyList<byte>? LiberoLogicalPositions=null)
{ public int[] Players()=>[P1MatchPlayerId,P2MatchPlayerId,P3MatchPlayerId,P4MatchPlayerId,P5MatchPlayerId,P6MatchPlayerId]; }
public sealed record StartSetRequest(MatchSide InitialServingSide);
public sealed record AddPointRequest(Guid PointUuid,MatchSide WinningSide,int? ObservedServerMatchPlayerId=null,IReadOnlyList<string>? ConfirmedRuleWarnings=null);
public sealed record CorrectLastPointRequest(Guid CorrectionUuid);
public sealed record AddSubstitutionRequest(Guid SubstitutionUuid,int PlayerOutMatchPlayerId,int PlayerInMatchPlayerId,IReadOnlyList<string>? ConfirmedRuleWarnings=null);
public sealed record LiberoEnterRequest(Guid EventUuid,int LiberoMatchPlayerId,int ReplacedMatchPlayerId,IReadOnlyList<string>? ConfirmedRuleWarnings=null);
public sealed record LiberoExitRequest(Guid EventUuid,int LiberoMatchPlayerId,IReadOnlyList<string>? ConfirmedRuleWarnings=null);
public sealed record AddTimeoutRequest(Guid TimeoutUuid,MatchSide Side,IReadOnlyList<string>? ConfirmedRuleWarnings=null);
public enum SanctionType { MisconductWarning, MisconductPenalty, Expulsion, Disqualification, ImproperRequest, DelayWarning, DelayPenalty }
public enum SanctionSubjectType { Player, Staff, Team }
public sealed record RecordSanctionRequest(Guid EventUuid, MatchSide Side, SanctionType Type, SanctionSubjectType SubjectType, int? MatchPlayerId, int? MatchTeamStaffId, IReadOnlyList<string>? ConfirmedRuleWarnings=null);
public sealed record CloseMatchRequest(Guid CloseUuid);
public sealed record CourtPositionDto(LineupPosition LogicalLineupPosition,LineupPosition PhysicalPosition,int EffectiveMatchPlayerId,bool IsLiberoReplacement);
public sealed record MatchSetStateDto(byte SetNumber,MatchSetStatus SetStatus,short HomePoints,short AwayPoints,byte HomeSets,byte AwaySets,MatchSide? InitialServingSide,MatchSide? CurrentServingSide,int? ServerMatchPlayerId,byte HomeRotationOffset,byte AwayRotationOffset,int HomeTimeouts,int AwayTimeouts,MatchSide? WinnerSide,bool MatchDecided,IReadOnlyList<CourtPositionDto> HomeCourtState,IReadOnlyList<CourtPositionDto> AwayCourtState);
public sealed record MatchEngineCommandResult(bool AlreadyApplied,MatchSetStateDto State);
public sealed record CloseMatchResult(bool AlreadyClosed,int MatchId,MatchSheetStatus MatchSheetStatus,MatchStatus MatchStatus,byte HomeSets,byte AwaySets,int WinnerTeamEntryId);

public sealed record SubstitutionPairRequest(int PlayerOutMatchPlayerId,int PlayerInMatchPlayerId);
public sealed record SubstitutionRequest(Guid EventUuid,MatchSide Side,IReadOnlyList<SubstitutionPairRequest> Replacements,IReadOnlyList<string>? ConfirmedRuleWarnings=null);
internal enum SportingDecisionOrigin { Direct, PersistedLocalEvent }
