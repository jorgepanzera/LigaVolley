using System.Text.Json;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.MatchSheets;

public sealed partial class MatchEngineService
{
    private static readonly JsonSerializerOptions CommandJson = new(JsonSerializerDefaults.Web);

    public Task<MatchEngineCommandResult> SubstituteAsync(int matchId, byte setNumber, AddSubstitutionRequest request, CancellationToken ct)
        => SubstituteSingle(matchId, setNumber, request, SportingDecisionOrigin.Direct, ct);

    internal Task<MatchEngineCommandResult> SubstituteSingle(int matchId, byte setNumber, AddSubstitutionRequest request, SportingDecisionOrigin origin, CancellationToken ct)
        => Mutate(matchId, async sheet =>
        {
            var player = sheet.Teams.SelectMany(x => x.Players).SingleOrDefault(x => x.MatchPlayerId == request.PlayerOutMatchPlayerId)
                ?? throw Invalid("invalid_substitution", "PlayerOut must belong to this MatchSheet.");
            return await ApplySubstitution(sheet, setNumber, new(request.SubstitutionUuid, player.MatchTeam.Side,
                [new(request.PlayerOutMatchPlayerId, request.PlayerInMatchPlayerId)], request.ConfirmedRuleWarnings),
                MatchEventType.Substitution, origin, ct);
        }, ct);

    public Task<MatchEngineCommandResult> SubstituteRequestAsync(int matchId, byte setNumber, SubstitutionRequest request, CancellationToken ct)
        => SubstituteRequestCore(matchId, setNumber, request, SportingDecisionOrigin.Direct, ct);

    internal Task<MatchEngineCommandResult> SubstituteRequestCore(int matchId, byte setNumber, SubstitutionRequest request, SportingDecisionOrigin origin, CancellationToken ct)
        => Mutate(matchId, sheet => ApplySubstitution(sheet, setNumber, request, MatchEventType.SubstitutionRequest, origin, ct), ct);

    private async Task<MatchEngineCommandResult> ApplySubstitution(MatchSheet sheet, byte number, SubstitutionRequest request,
        MatchEventType type, SportingDecisionOrigin origin, CancellationToken ct)
    {
        RequiredUuid(request.EventUuid, "invalid_substitution");
        var set = Set(sheet, number);
        if (CommandRetry(sheet, request.EventUuid, type, request, origin)) return Result(true, sheet, set);
        var command = new RuleCommand("SUBSTITUTION_REQUEST", request.Side.ToString().ToUpperInvariant(),
            request.Replacements?.Select(x => new RulePair(x.PlayerOutMatchPlayerId, x.PlayerInMatchPlayerId)).ToArray());
        EvaluateDecision(sheet, set, command, request.ConfirmedRuleWarnings, origin);
        var team = Team(sheet, request.Side);
        var regular = MatchRulesAssistant.Regular(RuleTeam(sheet, set, team));
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in request.Replacements!)
        {
            var position = (LineupPosition)(Array.IndexOf(regular, pair.PlayerOutMatchPlayerId) + 1);
            var substitution = new MatchSubstitution(Guid.NewGuid(), set, team,
                team.Players.Single(x => x.MatchPlayerId == pair.PlayerOutMatchPlayerId),
                team.Players.Single(x => x.MatchPlayerId == pair.PlayerInMatchPlayerId), position, now);
            substitution.BindRequest(request.EventUuid);
            set.Substitutions.Add(substitution);
        }
        // Active libero replacements cover logical slots, so changing their regular never removes the libero.
        var e = sheet.AddEvent(request.EventUuid, type, set, team.Side, null, now);
        e.RecordCommand(JsonSerializer.Serialize(request, CommandJson));
        await unit.SaveChangesAsync(ct);
        return Result(false, sheet, set);
    }

    public Task<MatchEngineCommandResult> TimeoutAsync(int matchId, byte setNumber, AddTimeoutRequest request, CancellationToken ct)
        => TimeoutCore(matchId, setNumber, request, SportingDecisionOrigin.Direct, ct);

    internal Task<MatchEngineCommandResult> TimeoutCore(int matchId, byte setNumber, AddTimeoutRequest request, SportingDecisionOrigin origin, CancellationToken ct)
        => Mutate(matchId, async sheet =>
        {
            RequiredUuid(request.TimeoutUuid, "invalid_request");
            var set = Set(sheet, setNumber);
            if (CommandRetry(sheet, request.TimeoutUuid, MatchEventType.Timeout, request, origin)) return Result(true, sheet, set);
            EvaluateDecision(sheet, set, new("TIMEOUT", request.Side.ToString().ToUpperInvariant()), request.ConfirmedRuleWarnings, origin);
            var team = Team(sheet, request.Side);
            var now = DateTimeOffset.UtcNow;
            set.Timeouts.Add(new(request.TimeoutUuid, set, team, set.Timeouts.Count(x => x.MatchTeamId == team.MatchTeamId) + 1, now));
            sheet.AddEvent(request.TimeoutUuid, MatchEventType.Timeout, set, team.Side, null, now).RecordCommand(JsonSerializer.Serialize(request, CommandJson));
            await unit.SaveChangesAsync(ct);
            return Result(false, sheet, set);
        }, ct);

    public Task<MatchEngineCommandResult> EnterLiberoAsync(int matchId, byte setNumber, LiberoEnterRequest request, CancellationToken ct)
        => EnterLiberoCore(matchId, setNumber, request, SportingDecisionOrigin.Direct, ct);

    internal Task<MatchEngineCommandResult> EnterLiberoCore(int matchId, byte setNumber, LiberoEnterRequest request, SportingDecisionOrigin origin, CancellationToken ct)
        => Mutate(matchId, async sheet =>
        {
            RequiredUuid(request.EventUuid, "invalid_libero_replacement");
            var set = Set(sheet, setNumber);
            if (CommandRetry(sheet, request.EventUuid, MatchEventType.LiberoEnter, request, origin)) return Result(true, sheet, set);
            var team = sheet.Teams.SingleOrDefault(x => x.Liberos.Any(l => l.MatchPlayerId == request.LiberoMatchPlayerId))
                ?? throw Invalid("libero_not_declared", "The player must be a declared libero.");
            EvaluateDecision(sheet, set, new("LIBERO_ENTER", team.Side.ToString().ToUpperInvariant(),
                LiberoMatchPlayerId: request.LiberoMatchPlayerId, ReplacedMatchPlayerId: request.ReplacedMatchPlayerId), request.ConfirmedRuleWarnings, origin);
            var court = Court(set, team);
            var position = court.Single(x => x.EffectiveMatchPlayerId == request.ReplacedMatchPlayerId).LogicalLineupPosition;
            var now = DateTimeOffset.UtcNow;
            foreach (var active in set.LiberoReplacements.Where(x => x.MatchTeamId == team.MatchTeamId && x.LineupPosition == position && !x.ExitedAt.HasValue)) active.Exit(now);
            var regular = MatchRulesAssistant.Regular(RuleTeam(sheet, set, team))[(int)position - 1];
            set.LiberoReplacements.Add(new(request.EventUuid, set, team,
                team.Players.Single(x => x.MatchPlayerId == request.LiberoMatchPlayerId),
                team.Players.Single(x => x.MatchPlayerId == regular), position, now));
            sheet.AddEvent(request.EventUuid, MatchEventType.LiberoEnter, set, team.Side, request.LiberoMatchPlayerId, now)
                .RecordCommand(JsonSerializer.Serialize(request, CommandJson));
            await unit.SaveChangesAsync(ct);
            return Result(false, sheet, set);
        }, ct);

    public Task<MatchEngineCommandResult> ExitLiberoAsync(int matchId, byte setNumber, LiberoExitRequest request, CancellationToken ct)
        => ExitLiberoCore(matchId, setNumber, request, SportingDecisionOrigin.Direct, ct);

    internal Task<MatchEngineCommandResult> ExitLiberoCore(int matchId, byte setNumber, LiberoExitRequest request, SportingDecisionOrigin origin, CancellationToken ct)
        => Mutate(matchId, async sheet =>
        {
            RequiredUuid(request.EventUuid, "invalid_libero_replacement");
            var set = Set(sheet, setNumber);
            if (CommandRetry(sheet, request.EventUuid, MatchEventType.LiberoExit, request, origin)) return Result(true, sheet, set);
            var team = sheet.Teams.SingleOrDefault(x => x.Liberos.Any(l => l.MatchPlayerId == request.LiberoMatchPlayerId))
                ?? throw Invalid("libero_not_declared", "The player must be a declared libero.");
            EvaluateDecision(sheet, set, new("LIBERO_EXIT", team.Side.ToString().ToUpperInvariant(),
                LiberoMatchPlayerId: request.LiberoMatchPlayerId), request.ConfirmedRuleWarnings, origin);
            var now = DateTimeOffset.UtcNow;
            set.LiberoReplacements.Single(x => x.LiberoMatchPlayerId == request.LiberoMatchPlayerId && !x.ExitedAt.HasValue).Exit(now);
            sheet.AddEvent(request.EventUuid, MatchEventType.LiberoExit, set, team.Side, request.LiberoMatchPlayerId, now)
                .RecordCommand(JsonSerializer.Serialize(request, CommandJson));
            await unit.SaveChangesAsync(ct);
            return Result(false, sheet, set);
        }, ct);

    private static bool CommandRetry<T>(MatchSheet sheet, Guid uuid, MatchEventType type, T request, SportingDecisionOrigin origin)
    {
        var prior = sheet.Events.SingleOrDefault(x => x.EventUuid == uuid);
        if (prior is null) return false;
        if (prior.EventType != type || origin == SportingDecisionOrigin.Direct && prior.CommandPayload is not null &&
            prior.CommandPayload != JsonSerializer.Serialize(request, CommandJson))
            throw Conflict("sync_event_uuid_conflict", "EventUuid was already used with different content.");
        return true;
    }

    private static void EvaluateDecision(MatchSheet sheet, MatchSet set, RuleCommand command, IReadOnlyList<string>? confirmations, SportingDecisionOrigin origin)
    {
        var evaluation = MatchRulesAssistant.Evaluate(new(sheet.Status == MatchSheetStatus.Closed,
            set.Status == MatchSetStatus.InProgress ? "IN_PROGRESS" : set.Status.ToString().ToUpperInvariant(),
            set.CurrentServingSide?.ToString().ToUpperInvariant(), set.HomePoints + set.AwayPoints,
            sheet.TrackSubstitutions, sheet.TrackLiberoReplacements, RuleTeam(sheet, set, Team(sheet, MatchSide.Home)),
            RuleTeam(sheet, set, Team(sheet, MatchSide.Away))), sheet.RulesSnapshot, command);
        if (evaluation.HardViolations.Count > 0) throw Conflict(evaluation.HardViolations[0].Code, "The command cannot be represented in the current MatchSheet state.");
        if (origin == SportingDecisionOrigin.Direct && evaluation.Warnings.Any(x => !(confirmations ?? []).Contains(x.Code)))
            throw new ResourceConflictException("rule_confirmation_required", "Confirm the specific sporting warnings before recording this action.")
            { Extensions = new Dictionary<string, object?> { ["warnings"] = evaluation.Warnings.Select(x => new RuleWarningDto(x.Code, x.Context, x.RequiresConfirmation)).ToArray() } };
    }

    private static RuleTeamState RuleTeam(MatchSheet sheet, MatchSet set, MatchTeam team)
    {
        var events = sheet.Events.Where(x => x.MatchSetId == set.MatchSetId && x.Status == MatchEventStatus.Active).OrderBy(x => x.SequenceNumber).ToArray();
        var last = events.LastOrDefault(x => x.Side == team.Side && x.EventType is MatchEventType.LiberoEnter or MatchEventType.LiberoExit);
        var lastEnter = events.LastOrDefault(x => x.Side == team.Side && x.EventType == MatchEventType.LiberoEnter);
        var lastReplacement = lastEnter is null ? null : set.LiberoReplacements.SingleOrDefault(x => x.ReplacementUuid == lastEnter.EventUuid);
        var ineligible = sheet.Events.Where(x => x.Status == MatchEventStatus.Active && x.MatchPlayerId.HasValue &&
                (x.EventType == MatchEventType.Disqualification || x.EventType == MatchEventType.Expulsion && x.MatchSetId == set.MatchSetId))
            .Select(x => x.MatchPlayerId!.Value).Distinct().ToArray();
        return new(team.Players.Select(x => x.MatchPlayerId).ToArray(), team.Liberos.Select(x => x.MatchPlayerId).ToArray(),
            set.Lineups.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId)?.Positions.OrderBy(x => x.Position).Select(x => x.MatchPlayerId).ToArray() ?? [],
            set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId).OrderBy(x => x.MatchSubstitutionId == 0 ? int.MaxValue : x.MatchSubstitutionId).Select(x => new RuleSubstitution((int)x.LineupPosition - 1, x.PlayerOutMatchPlayerId, x.PlayerInMatchPlayerId)).ToArray(),
            set.LiberoReplacements.Where(x => x.MatchTeamId == team.MatchTeamId && !x.ExitedAt.HasValue).Select(x => new RuleReplacement((int)x.LineupPosition - 1, x.LiberoMatchPlayerId, x.ReplacedMatchPlayerId)).ToArray(),
            team.Side == MatchSide.Home ? set.HomeRotationOffset : set.AwayRotationOffset, set.Timeouts.Count(x => x.MatchTeamId == team.MatchTeamId),
            last is null ? null : events.Count(x => x.EventType == MatchEventType.Point && x.SequenceNumber < last.SequenceNumber),
            lastReplacement?.ReplacedMatchPlayerId, ineligible);
    }
}
