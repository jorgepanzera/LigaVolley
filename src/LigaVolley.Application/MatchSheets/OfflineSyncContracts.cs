using System.Text.Json;
using System.Text.Json.Serialization;
namespace LigaVolley.Application.MatchSheets;
[JsonConverter(typeof(UpperSnakeCaseEnumConverter<ScorerSyncEventType>))]
public enum ScorerSyncEventType{PrepareSet,SetLineup,StartSet,Point,CorrectLastPoint,Substitution,LiberoEnter,LiberoExit,Timeout,MatchClose}
[JsonConverter(typeof(UpperSnakeCaseEnumConverter<ScorerSyncResultStatus>))]
public enum ScorerSyncResultStatus{Applied,AlreadyAccepted}
public sealed record ScorerSyncEvent(Guid EventUuid,long Sequence,ScorerSyncEventType Type,DateTimeOffset OccurredAt,JsonElement Payload);
public sealed record SyncMatchSheetRequest(Guid SheetUuid,Guid SessionUuid,string DeviceId,IReadOnlyList<ScorerSyncEvent> Events);
public sealed record ScorerSyncEventResult(Guid EventUuid,long Sequence,ScorerSyncResultStatus Status);
public sealed record SyncMatchSheetResponse(Guid SheetUuid,Guid SessionUuid,long LastAcceptedSequence,IReadOnlyList<ScorerSyncEventResult> Results,MatchSheetSnapshotDto Snapshot);
public sealed record TakeOverMatchSheetRequest(Guid SheetUuid,Guid ExpectedSessionUuid,string DeviceId,Guid ClientRequestId);
public sealed record TakeOverMatchSheetResponse(bool AlreadyApplied,Guid PreviousSessionUuid,Guid SessionUuid,long LastAcceptedSequence,MatchSheetSnapshotDto Snapshot);
internal sealed record SyncSetPayload(byte SetNumber);
internal sealed record SyncLineupPayload(byte SetNumber,Domain.MatchSheets.MatchSide Side,int P1MatchPlayerId,int P2MatchPlayerId,int P3MatchPlayerId,int P4MatchPlayerId,int P5MatchPlayerId,int P6MatchPlayerId,int? LiberoMatchPlayerId=null,IReadOnlyList<byte>? LiberoLogicalPositions=null);
internal sealed record SyncStartPayload(byte SetNumber,Domain.MatchSheets.MatchSide InitialServingSide);
internal sealed record SyncPointPayload(byte SetNumber,Domain.MatchSheets.MatchSide WinningSide);
internal sealed record SyncSubstitutionPayload(byte SetNumber,int PlayerOutMatchPlayerId,int PlayerInMatchPlayerId);
internal sealed record SyncLiberoEnterPayload(byte SetNumber,int LiberoMatchPlayerId,int ReplacedMatchPlayerId);
internal sealed record SyncLiberoExitPayload(byte SetNumber,int LiberoMatchPlayerId);
internal sealed record SyncTimeoutPayload(byte SetNumber,Domain.MatchSheets.MatchSide Side);

public sealed class UpperSnakeCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? throw new JsonException($"A string is required for {typeof(TEnum).Name}.");
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
        foreach (var candidate in Enum.GetValues<TEnum>())
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                return candidate;
        throw new JsonException($"'{value}' is not a valid {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        var name = value.ToString();
        writer.WriteStringValue(string.Concat(name.Select((c, index) =>
            index > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant());
    }
}
