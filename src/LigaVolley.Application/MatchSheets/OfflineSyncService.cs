using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;
namespace LigaVolley.Application.MatchSheets;
public sealed class OfflineSyncService(IMatchSheetRepository sheets, IUnitOfWork unit, MatchEngineService engine, MatchSheetService snapshots)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public async Task<SyncMatchSheetResponse> SyncAsync(int matchId, SyncMatchSheetRequest r, CancellationToken ct) { Validate(r); await using var tx = await unit.BeginSerializableTransactionAsync(ct); await sheets.AcquireMatchLockAsync(matchId, ct); var sheet = await sheets.GetAsync(matchId, true, ct) ?? throw new ResourceNotFoundException("MatchSheet", matchId); if (sheet.SheetUuid != r.SheetUuid) throw Conflict("match_sheet_session_mismatch", "SheetUuid does not belong to this match."); var session = sheet.Sessions.SingleOrDefault(x => x.SessionUuid == r.SessionUuid) ?? throw new ResourceNotFoundException("MatchSheetSession", r.SessionUuid); if (session.DeviceId != r.DeviceId.Trim()) throw Conflict("match_sheet_session_mismatch", "DeviceId does not match the session."); var items = r.Events.Select(e => (e, hash: Hash(e.Type, e.Payload), existing: sheet.Events.SingleOrDefault(x => x.EventUuid == e.EventUuid))).ToArray(); foreach (var x in items) if (x.existing is not null && (x.existing.MatchSheetSessionId != session.MatchSheetSessionId || x.existing.LocalSequence != x.e.Sequence || x.existing.EventType != Map(x.e.Type) || x.existing.SyncPayloadHash != x.hash)) throw Conflict("sync_event_uuid_conflict", "EventUuid was already accepted with different content."); var expected = session.LastAcceptedSequence + 1; foreach (var x in items.Where(x => x.existing is null)) { if (x.e.Sequence != expected) throw new ResourceConflictException("sync_sequence_gap", "New events must be contiguous.") { Extensions = new Dictionary<string, object?> { { "expectedSequence", expected } } }; expected++; } if (session.Status != MatchSheetSessionStatus.Active && items.Any(x => x.existing is null)) throw Conflict("match_sheet_session_not_active", "This session cannot accept new events."); var results = new List<ScorerSyncEventResult>(); foreach (var x in items) { if (x.existing is not null) { results.Add(new(x.e.EventUuid, x.e.Sequence, ScorerSyncResultStatus.AlreadyAccepted)); continue; } session.Accept(x.e.Sequence); await Apply(matchId, x.e, ct); var ev = sheet.Events.SingleOrDefault(y => y.EventUuid == x.e.EventUuid); if (ev is null) { var set = TrySet(sheet, x.e.Payload); ev = sheet.AddEvent(x.e.EventUuid, Map(x.e.Type), set, null, null, DateTimeOffset.UtcNow); } ev.BindSynchronization(session, x.e.Sequence, x.hash, x.e.OccurredAt); await unit.SaveChangesAsync(ct); results.Add(new(x.e.EventUuid, x.e.Sequence, ScorerSyncResultStatus.Applied)); } await tx.CommitAsync(ct); var snapshot = await snapshots.GetSheetAsync(matchId, ct); return new(r.SheetUuid, r.SessionUuid, session.LastAcceptedSequence, results, snapshot); }
    public async Task<TakeOverMatchSheetResponse> TakeOverAsync(int matchId, TakeOverMatchSheetRequest r, CancellationToken ct) { if (r.SheetUuid == Guid.Empty || r.ExpectedSessionUuid == Guid.Empty || r.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(r.DeviceId)) throw Invalid("invalid_request", "SheetUuid, ExpectedSessionUuid, ClientRequestId and DeviceId are required."); await using var tx = await unit.BeginSerializableTransactionAsync(ct); await sheets.AcquireMatchLockAsync(matchId, ct); var sheet = await sheets.GetAsync(matchId, true, ct) ?? throw new ResourceNotFoundException("MatchSheet", matchId); if (sheet.SheetUuid != r.SheetUuid) throw Conflict("match_sheet_session_mismatch", "SheetUuid does not belong to this match."); var retry = sheet.Audits.SingleOrDefault(x => x.ClientRequestId == r.ClientRequestId); if (retry is not null) { if (retry.Type != MatchSheetAuditType.MatchSheetTakenOver || retry.PreviousSessionUuid != r.ExpectedSessionUuid || retry.NewDeviceId != r.DeviceId.Trim()) throw Conflict("match_sheet_takeover_request_conflict", "ClientRequestId was used with different content."); await tx.CommitAsync(ct); return new(true, retry.PreviousSessionUuid!.Value, retry.NewSessionUuid!.Value, 0, await snapshots.GetSheetAsync(matchId, ct)); } if (sheet.Status == MatchSheetStatus.Closed) throw Conflict("match_sheet_closed", "A closed MatchSheet cannot be taken over."); var active = sheet.Sessions.SingleOrDefault(x => x.Status == MatchSheetSessionStatus.Active) ?? throw Conflict("match_sheet_session_not_active", "No active session exists."); if (active.SessionUuid != r.ExpectedSessionUuid) throw Conflict("match_sheet_takeover_request_conflict", "The expected active session changed."); var previous = active.SessionUuid; var next = sheet.TakeOver(r.DeviceId, r.ClientRequestId, DateTimeOffset.UtcNow); await unit.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(false, previous, next.SessionUuid, 0, await snapshots.GetSheetAsync(matchId, ct)); }
    async Task Apply(int id, ScorerSyncEvent e, CancellationToken ct)
    {
        try
        {
            await ApplyCore(id, e, ct);
        }
        catch (RequestValidationException exception)
        {
            throw new RequestValidationException(exception.Code, exception.Message)
            {
                Extensions = EventExtensions(e, exception.Extensions)
            };
        }
        catch (ResourceConflictException exception)
        {
            throw new ResourceConflictException(exception.Code, exception.Message)
            {
                Extensions = EventExtensions(e, exception.Extensions)
            };
        }
    }

    async Task ApplyCore(int id, ScorerSyncEvent e, CancellationToken ct)
    {
        switch (e.Type)
        {
            case ScorerSyncEventType.PrepareSet: await engine.PrepareSetAsync(id, ct); break;
            case ScorerSyncEventType.SetLineup: { var p = Read<SyncLineupPayload>(e); await engine.SaveLineupAsync(id, p.SetNumber, p.Side, new(p.P1MatchPlayerId, p.P2MatchPlayerId, p.P3MatchPlayerId, p.P4MatchPlayerId, p.P5MatchPlayerId, p.P6MatchPlayerId, p.LiberoMatchPlayerId, p.LiberoLogicalPositions), ct); break; }
            case ScorerSyncEventType.StartSet: { var p = Read<SyncStartPayload>(e); await engine.StartSetAsync(id, p.SetNumber, new(p.InitialServingSide), ct); break; }
            case ScorerSyncEventType.Point: { var p = Read<SyncPointPayload>(e); await engine.AddPointAsync(id, p.SetNumber, new(e.EventUuid, p.WinningSide), ct); break; }
            case ScorerSyncEventType.CorrectLastPoint: { var p = Read<SyncSetPayload>(e); await engine.CorrectLastPointAsync(id, p.SetNumber, new(e.EventUuid), ct); break; }
            case ScorerSyncEventType.Substitution: { var p = Read<SyncSubstitutionPayload>(e); await engine.SubstituteAsync(id, p.SetNumber, new(e.EventUuid, p.PlayerOutMatchPlayerId, p.PlayerInMatchPlayerId), ct); break; }
            case ScorerSyncEventType.LiberoEnter: { var p = Read<SyncLiberoEnterPayload>(e); await engine.EnterLiberoAsync(id, p.SetNumber, new(e.EventUuid, p.LiberoMatchPlayerId, p.ReplacedMatchPlayerId), ct); break; }
            case ScorerSyncEventType.LiberoExit: { var p = Read<SyncLiberoExitPayload>(e); await engine.ExitLiberoAsync(id, p.SetNumber, new(e.EventUuid, p.LiberoMatchPlayerId), ct); break; }
            case ScorerSyncEventType.Timeout: { var p = Read<SyncTimeoutPayload>(e); await engine.TimeoutAsync(id, p.SetNumber, new(e.EventUuid, p.Side), ct); break; }
            case ScorerSyncEventType.MatchClose: await engine.CloseAsync(id, new(e.EventUuid), ct); break;
            default: throw Invalid("sync_invalid_event_type", "Unsupported sync event type.");
        }
    }

    static IReadOnlyDictionary<string, object?> EventExtensions(
        ScorerSyncEvent e,
        IReadOnlyDictionary<string, object?> existing)
    {
        var result = new Dictionary<string, object?>(existing)
        {
            ["eventUuid"] = e.EventUuid,
            ["localSequence"] = e.Sequence
        };
        return result;
    }
    static T Read<T>(ScorerSyncEvent e) => e.Payload.Deserialize<T>(Json) ?? throw Invalid("invalid_request", $"Payload for {e.Type} is required."); static MatchSet? TrySet(MatchSheet s, JsonElement p) => p.TryGetProperty("setNumber", out var n) && n.TryGetByte(out var v) ? s.Sets.SingleOrDefault(x => x.SetNumber == v) : null; static void Validate(SyncMatchSheetRequest r) { if (r.SheetUuid == Guid.Empty || r.SessionUuid == Guid.Empty || string.IsNullOrWhiteSpace(r.DeviceId) || r.Events is null) throw Invalid("invalid_request", "SheetUuid, SessionUuid, DeviceId and Events are required."); long prior = 0; var ids = new HashSet<Guid>(); foreach (var e in r.Events) { if (e.EventUuid == Guid.Empty || e.Sequence <= 0) throw Invalid("invalid_request", "EventUuid and positive Sequence are required."); if (e.Sequence <= prior || !ids.Add(e.EventUuid)) throw Invalid("sync_duplicate_sequence", "Events must be strictly ordered with unique UUIDs and sequences."); prior = e.Sequence; } }
    static MatchEventType Map(ScorerSyncEventType t) => t switch { ScorerSyncEventType.PrepareSet => MatchEventType.PrepareSet, ScorerSyncEventType.SetLineup => MatchEventType.SetLineup, ScorerSyncEventType.StartSet => MatchEventType.StartSet, ScorerSyncEventType.Point => MatchEventType.Point, ScorerSyncEventType.CorrectLastPoint => MatchEventType.PointCorrection, ScorerSyncEventType.Substitution => MatchEventType.Substitution, ScorerSyncEventType.LiberoEnter => MatchEventType.LiberoEnter, ScorerSyncEventType.LiberoExit => MatchEventType.LiberoExit, ScorerSyncEventType.Timeout => MatchEventType.Timeout, ScorerSyncEventType.MatchClose => MatchEventType.MatchClosed, _ => throw Invalid("sync_invalid_event_type", "Unsupported sync event type.") }; static string Hash(ScorerSyncEventType t, JsonElement p) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{t}:{p.GetRawText()}"))); static RequestValidationException Invalid(string c, string m) => new(c, m); static ResourceConflictException Conflict(string c, string m) => new(c, m);
}
