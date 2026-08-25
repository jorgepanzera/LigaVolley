using System.Text.Json;
namespace LigaVolley.Application.MatchSheets;
public enum ScorerSyncEventType{PrepareSet,SetLineup,StartSet,Point,CorrectLastPoint,Substitution,LiberoEnter,LiberoExit,Timeout,MatchClose}
public enum ScorerSyncResultStatus{Applied,AlreadyAccepted}
public sealed record ScorerSyncEvent(Guid EventUuid,long Sequence,ScorerSyncEventType Type,DateTimeOffset OccurredAt,JsonElement Payload);
public sealed record SyncMatchSheetRequest(Guid SheetUuid,Guid SessionUuid,string DeviceId,IReadOnlyList<ScorerSyncEvent> Events);
public sealed record ScorerSyncEventResult(Guid EventUuid,long Sequence,ScorerSyncResultStatus Status);
public sealed record SyncMatchSheetResponse(Guid SheetUuid,Guid SessionUuid,long LastAcceptedSequence,IReadOnlyList<ScorerSyncEventResult> Results,MatchSheetSnapshotDto Snapshot);
public sealed record TakeOverMatchSheetRequest(Guid SheetUuid,Guid ExpectedSessionUuid,string DeviceId,Guid ClientRequestId);
public sealed record TakeOverMatchSheetResponse(bool AlreadyApplied,Guid PreviousSessionUuid,Guid SessionUuid,long LastAcceptedSequence,MatchSheetSnapshotDto Snapshot);
internal sealed record SyncSetPayload(byte SetNumber);
internal sealed record SyncLineupPayload(byte SetNumber,Domain.MatchSheets.MatchSide Side,int P1MatchPlayerId,int P2MatchPlayerId,int P3MatchPlayerId,int P4MatchPlayerId,int P5MatchPlayerId,int P6MatchPlayerId);
internal sealed record SyncStartPayload(byte SetNumber,Domain.MatchSheets.MatchSide InitialServingSide);
internal sealed record SyncPointPayload(byte SetNumber,Domain.MatchSheets.MatchSide WinningSide);
internal sealed record SyncSubstitutionPayload(byte SetNumber,int PlayerOutMatchPlayerId,int PlayerInMatchPlayerId);
internal sealed record SyncLiberoEnterPayload(byte SetNumber,int LiberoMatchPlayerId,int ReplacedMatchPlayerId);
internal sealed record SyncLiberoExitPayload(byte SetNumber,int LiberoMatchPlayerId);
internal sealed record SyncTimeoutPayload(byte SetNumber,Domain.MatchSheets.MatchSide Side);
