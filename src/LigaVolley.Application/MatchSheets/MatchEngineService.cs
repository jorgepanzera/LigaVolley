using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.PlayoffProgression;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.MatchSheets;

public sealed class MatchEngineService(IMatchSheetRepository sheets, IUnitOfWork unit, PlayoffProgressionService playoffs)
{
    public Task<MatchEngineCommandResult> PrepareSetAsync(int matchId, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        var active = sheet.Sets.SingleOrDefault(x => x.Status != MatchSetStatus.Finished);
        if (active is not null) return Result(true, sheet, active);
        if (sheet.HomeSets == 3 || sheet.AwaySets == 3) throw Conflict("match_already_decided", "The match is already decided.");
        if (sheet.Status == MatchSheetStatus.Open && sheet.Match.Status != MatchStatus.Scheduled || sheet.Status == MatchSheetStatus.InProgress && sheet.Match.Status != MatchStatus.InProgress) throw Conflict("match_sheet_invalid_state", "Match and MatchSheet states are inconsistent.");
        try { var set = sheet.PrepareSet(); await unit.SaveChangesAsync(ct); return Result(false, sheet, set); } catch (DomainValidationException ex) { throw Conflict("maximum_sets_reached", ex.Message); }
    }, ct);

    public Task<MatchEngineCommandResult> SaveLineupAsync(int matchId, byte setNumber, MatchSide side, SetLineupRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        var set = Set(sheet, setNumber); if (set.Status != MatchSetStatus.Ready) throw Conflict("lineup_locked", "Lineup is frozen after the set starts.");
        var ids = request.Players(); if (ids.Distinct().Count() != 6) throw Invalid("lineup_duplicate_player", "Lineup players must be different.");
        var team = Team(sheet, side); var players = new List<MatchPlayer>();
        foreach (var id in ids) { var p = team.Players.SingleOrDefault(x => x.MatchPlayerId == id); if (p is null) throw Invalid("lineup_player_wrong_team", $"MatchPlayer '{id}' does not belong to {side}."); if (p.Status != MatchPlayerStatus.Available) throw Invalid("lineup_invalid", "Every lineup player must be available."); if (team.Liberos.Any(x => x.MatchPlayerId == id)) throw Invalid("lineup_libero_not_allowed", "A declared libero cannot be in P1..P6."); players.Add(p); }
        var lineup = set.Lineups.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId); if (lineup is null) { lineup = new MatchLineup(set, team); set.Lineups.Add(lineup); }
        lineup.Replace(players); ConfigureLiberoPlan(sheet, set, team, request, ids); await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public Task<MatchEngineCommandResult> StartSetAsync(int matchId, byte setNumber, StartSetRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        var set = Set(sheet, setNumber); if (set.Status == MatchSetStatus.InProgress) return Result(true, sheet, set); if (set.Status != MatchSetStatus.Ready) throw Conflict("match_set_invalid_state", "Only a Ready set can start.");
        if (set.Lineups.Count != 2 || set.Lineups.Any(x => x.Positions.Count != 6)) throw Conflict("lineup_incomplete", $"Both complete lineups are required (lineups={set.Lineups.Count}, positions={string.Join(',', set.Lineups.Select(x => x.Positions.Count))}).");
        if (sheet.Sets.Any(x => x != set && x.Status == MatchSetStatus.InProgress)) throw Conflict("match_set_already_active", "Another set is in progress.");
        var now = DateTimeOffset.UtcNow; set.Start(request.InitialServingSide, now); ReconcileAutomaticLiberos(sheet, set, now);
        if (setNumber == 1) { sheet.StartFirstSet(now); sheet.Match.Start(); sheet.Match.Competition.MarkInProgressAfterMatchStart(); }
        await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public Task<MatchEngineCommandResult> AddPointAsync(int matchId, byte setNumber, AddPointRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        RequiredUuid(request.PointUuid, "point_duplicate"); var prior = sheet.Events.SingleOrDefault(x => x.EventUuid == request.PointUuid); var set = Set(sheet, setNumber); if (prior is not null) { if (prior.EventType != MatchEventType.Point) throw Conflict("point_duplicate", "EventUuid is already used."); return Result(true, sheet, set); }
        Mutable(sheet); if (set.Status != MatchSetStatus.InProgress) throw Conflict("point_invalid_state", "Point requires an InProgress set.");
        var now = DateTimeOffset.UtcNow; set.ApplyPoint(request.WinningSide, now); ReconcileAutomaticLiberos(sheet, set, now); sheet.AddEvent(request.PointUuid, MatchEventType.Point, set, request.WinningSide, null, now); sheet.RecalculateSets(); await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public Task<MatchEngineCommandResult> CorrectLastPointAsync(int matchId, byte setNumber, CorrectLastPointRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        RequiredUuid(request.CorrectionUuid, "point_duplicate"); var set = Set(sheet, setNumber); var existing = sheet.Events.SingleOrDefault(x => x.EventUuid == request.CorrectionUuid); if (existing is not null) { if (existing.EventType != MatchEventType.PointCorrection) throw Conflict("point_duplicate", "EventUuid is already used."); return Result(true, sheet, set); }
        Mutable(sheet);
        var sports = sheet.Events.Where(x => x.MatchSetId == set.MatchSetId && x.Status == MatchEventStatus.Active && x.EventType is MatchEventType.Point or MatchEventType.Substitution or MatchEventType.LiberoEnter or MatchEventType.LiberoExit or MatchEventType.Timeout).OrderByDescending(x => x.SequenceNumber).FirstOrDefault();
        if (sports is null) throw Conflict("no_point_to_correct", "There is no active point to correct."); if (sports.EventType != MatchEventType.Point) throw Conflict("point_not_last_effective_event", "The last effective sporting event is not a point.");
        var now = DateTimeOffset.UtcNow; sports.Cancel(); sheet.AddEvent(request.CorrectionUuid, MatchEventType.PointCorrection, set, null, null, now, sports); var rebuilt = MatchSetRebuilder.Rebuild(set.InitialServingSide!.Value, sheet.Events.Where(x => x.MatchSetId == set.MatchSetId)); set.Rebuild(rebuilt.Home, rebuilt.Away, rebuilt.Serving, rebuilt.HomeOffset, rebuilt.AwayOffset, now); foreach (var active in set.LiberoReplacements.Where(x => !x.ExitedAt.HasValue)) active.Exit(now); ReconcileAutomaticLiberos(sheet, set, now); sheet.RecalculateSets(); await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public Task<MatchEngineCommandResult> SubstituteAsync(int matchId, byte setNumber, AddSubstitutionRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        RequiredUuid(request.SubstitutionUuid, "invalid_substitution"); var set = Set(sheet, setNumber); var existing = sheet.Events.SingleOrDefault(x => x.EventUuid == request.SubstitutionUuid); if (existing is not null) return Result(true, sheet, set); Mutable(sheet); if (!sheet.TrackSubstitutions) throw Conflict("substitution_tracking_disabled", "Substitution tracking is disabled."); if (set.Status != MatchSetStatus.InProgress) throw Conflict("match_set_invalid_state", "Substitution requires an InProgress set.");
        var outPlayer = sheet.Teams.SelectMany(x => x.Players).SingleOrDefault(x => x.MatchPlayerId == request.PlayerOutMatchPlayerId); var inPlayer = sheet.Teams.SelectMany(x => x.Players).SingleOrDefault(x => x.MatchPlayerId == request.PlayerInMatchPlayerId); if (outPlayer is null || inPlayer is null || outPlayer.MatchTeamId != inPlayer.MatchTeamId || outPlayer == inPlayer) throw Invalid("invalid_substitution", "Players must be different and belong to the same team."); var team = sheet.Teams.Single(x => x.MatchTeamId == outPlayer.MatchTeamId); if (team.Liberos.Any(x => x.MatchPlayerId == outPlayer.MatchPlayerId || x.MatchPlayerId == inPlayer.MatchPlayerId)) throw Invalid("substitution_player_is_libero", "Liberos cannot participate in normal substitutions.");
        var lineup = set.Lineups.Single(x => x.MatchTeamId == team.MatchTeamId); var offset = team.Side == MatchSide.Home ? set.HomeRotationOffset : set.AwayRotationOffset; var regularCourt = MatchCourtStateCalculator.Calculate(lineup, offset, set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId), []); var slot = regularCourt.SingleOrDefault(x => x.EffectiveMatchPlayerId == outPlayer.MatchPlayerId); if (slot is null) throw Conflict("substitution_player_not_on_court", "PlayerOut is not the current regular player of an on-court logical position."); if (regularCourt.Any(x => x.EffectiveMatchPlayerId == inPlayer.MatchPlayerId)) throw Conflict("substitution_player_already_on_court", "PlayerIn is already on court.");
        var starter = lineup.Positions.Single(x => x.Position == slot.LogicalLineupPosition).MatchPlayerId; var history = set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId && x.LineupPosition == slot.LogicalLineupPosition).ToArray(); if (outPlayer.MatchPlayerId == starter) { if (history.Length > 0 || set.Substitutions.Any(x => x.PlayerInMatchPlayerId == inPlayer.MatchPlayerId)) throw Conflict("invalid_substitution_pair", "Starter already completed its substitution pair or substitute already entered."); } else if (inPlayer.MatchPlayerId != starter || history.Length != 1 || history[0].PlayerInMatchPlayerId != outPlayer.MatchPlayerId) throw Conflict("invalid_substitution_pair", "Only the original starter may replace its paired substitute.");
        var now = DateTimeOffset.UtcNow; var substitution = new MatchSubstitution(request.SubstitutionUuid, set, team, outPlayer, inPlayer, slot.LogicalLineupPosition, now); set.Substitutions.Add(substitution); var activeReplacement = set.LiberoReplacements.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId && x.LineupPosition == slot.LogicalLineupPosition && !x.ExitedAt.HasValue); if (activeReplacement is not null) { activeReplacement.Exit(now); ReconcileAutomaticLiberos(sheet, set, now); }
        sheet.AddEvent(request.SubstitutionUuid, MatchEventType.Substitution, set, team.Side, inPlayer.MatchPlayerId, now); await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public Task<MatchEngineCommandResult> EnterLiberoAsync(int matchId, byte setNumber, LiberoEnterRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        RequiredUuid(request.EventUuid, "invalid_libero_replacement"); var set = Set(sheet, setNumber); if (sheet.Events.Any(x => x.EventUuid == request.EventUuid)) return Result(true, sheet, set); Mutable(sheet); if (!sheet.TrackLiberoReplacements) throw Conflict("libero_tracking_disabled", "Libero tracking is disabled."); if (set.Status != MatchSetStatus.InProgress) throw Conflict("match_set_invalid_state", "Libero replacement requires an InProgress set.");
        var team = sheet.Teams.SingleOrDefault(x => x.Liberos.Any(l => l.MatchPlayerId == request.LiberoMatchPlayerId)) ?? throw Invalid("libero_not_declared", "The player is not a declared libero."); if (set.LiberoReplacements.Any(x => x.LiberoMatchPlayerId == request.LiberoMatchPlayerId && !x.ExitedAt.HasValue)) throw Conflict("libero_already_on_court", "Libero is already on court."); var lineup = set.Lineups.Single(x => x.MatchTeamId == team.MatchTeamId); var offset = team.Side == MatchSide.Home ? set.HomeRotationOffset : set.AwayRotationOffset; var court = MatchCourtStateCalculator.Calculate(lineup, offset, set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId), set.LiberoReplacements.Where(x => x.MatchTeamId == team.MatchTeamId)); var replaced = court.SingleOrDefault(x => x.EffectiveMatchPlayerId == request.ReplacedMatchPlayerId) ?? throw Invalid("libero_invalid_replaced_player", "Replaced player is not on court."); if (replaced.PhysicalPosition is not LineupPosition.P1 and not LineupPosition.P5 and not LineupPosition.P6) throw Conflict("libero_not_back_row", "Libero may only replace a back-row player."); var lp = team.Players.Single(x => x.MatchPlayerId == request.LiberoMatchPlayerId); var rp = team.Players.Single(x => x.MatchPlayerId == request.ReplacedMatchPlayerId); set.LiberoReplacements.Add(new MatchLiberoReplacement(request.EventUuid, set, team, lp, rp, replaced.LogicalLineupPosition, DateTimeOffset.UtcNow)); sheet.AddEvent(request.EventUuid, MatchEventType.LiberoEnter, set, team.Side, lp.MatchPlayerId, DateTimeOffset.UtcNow); await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public Task<MatchEngineCommandResult> ExitLiberoAsync(int matchId, byte setNumber, LiberoExitRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        RequiredUuid(request.EventUuid, "invalid_libero_replacement"); var set = Set(sheet, setNumber); if (sheet.Events.Any(x => x.EventUuid == request.EventUuid)) return Result(true, sheet, set); Mutable(sheet); if (!sheet.TrackLiberoReplacements) throw Conflict("libero_tracking_disabled", "Libero tracking is disabled."); var replacement = set.LiberoReplacements.SingleOrDefault(x => x.LiberoMatchPlayerId == request.LiberoMatchPlayerId && !x.ExitedAt.HasValue) ?? throw Invalid("invalid_libero_replacement", "Libero has no active replacement."); replacement.Exit(DateTimeOffset.UtcNow); sheet.AddEvent(request.EventUuid, MatchEventType.LiberoExit, set, replacement.MatchTeam.Side, request.LiberoMatchPlayerId, DateTimeOffset.UtcNow); await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public Task<MatchEngineCommandResult> TimeoutAsync(int matchId, byte setNumber, AddTimeoutRequest request, CancellationToken ct) => Mutate(matchId, async sheet =>
    {
        RequiredUuid(request.TimeoutUuid, "timeout_limit_reached"); var set = Set(sheet, setNumber); if (sheet.Events.Any(x => x.EventUuid == request.TimeoutUuid)) return Result(true, sheet, set); Mutable(sheet); if (set.Status != MatchSetStatus.InProgress) throw Conflict("match_set_invalid_state", "Timeout requires an InProgress set."); var team = Team(sheet, request.Side); var count = set.Timeouts.Count(x => x.MatchTeamId == team.MatchTeamId); if (count >= 2) throw Conflict("timeout_limit_reached", "A team may use at most two timeouts per set."); set.Timeouts.Add(new MatchTimeout(request.TimeoutUuid, set, team, (byte)(count + 1), DateTimeOffset.UtcNow)); sheet.AddEvent(request.TimeoutUuid, MatchEventType.Timeout, set, team.Side, null, DateTimeOffset.UtcNow); await unit.SaveChangesAsync(ct); return Result(false, sheet, set);
    }, ct);

    public async Task<CloseMatchResult> CloseAsync(int matchId, CloseMatchRequest request, CancellationToken ct)
    {
        RequiredUuid(request.CloseUuid, "match_result_inconsistent"); await using var tx = await unit.BeginSerializableTransactionAsync(ct); await sheets.AcquireMatchLockAsync(matchId, ct); var sheet = await Required(matchId, ct); var prior = sheet.Events.SingleOrDefault(x => x.EventUuid == request.CloseUuid); if (sheet.Status == MatchSheetStatus.Closed) { if (prior?.EventType != MatchEventType.MatchClosed) throw Conflict("match_already_closed", "MatchSheet is already closed."); await tx.CommitAsync(ct); return CloseResult(true, sheet); }
        if (sheet.Status != MatchSheetStatus.InProgress || sheet.Match.Status != MatchStatus.InProgress) throw Conflict("match_sheet_invalid_state", "Match and MatchSheet must be InProgress."); if (sheet.Sets.Any(x => x.Status != MatchSetStatus.Finished)) throw Conflict("match_not_decided", "Every prepared set must be Finished."); sheet.RecalculateSets(); if ((sheet.HomeSets == 3) == (sheet.AwaySets == 3) || sheet.HomeSets + sheet.AwaySets is < 3 or > 5) throw Conflict("match_result_inconsistent", "Result must be 3-0, 3-1 or 3-2."); var winner = sheet.HomeSets == 3 ? sheet.Match.HomeTeamEntry! : sheet.Match.AwayTeamEntry!; sheet.Close(winner.TeamEntryId, DateTimeOffset.UtcNow); sheet.Match.Finish(sheet.HomeSets, sheet.AwaySets, winner, sheet.Sets); sheet.AddEvent(request.CloseUuid, MatchEventType.MatchClosed, null, sheet.HomeSets == 3 ? MatchSide.Home : MatchSide.Away, null, DateTimeOffset.UtcNow); await unit.SaveChangesAsync(ct); if (sheet.Match.SeriesId.HasValue) await playoffs.ProcessFinishedMatchWithinTransactionAsync(matchId, ct); await tx.CommitAsync(ct); return CloseResult(false, sheet);
    }

    private async Task<T> Mutate<T>(int matchId, Func<MatchSheet, Task<T>> action, CancellationToken ct) { await using var tx = await unit.BeginSerializableTransactionAsync(ct); await sheets.AcquireMatchLockAsync(matchId, ct); var result = await action(await Required(matchId, ct)); await tx.CommitAsync(ct); return result; }
    private async Task<MatchSheet> Required(int id, CancellationToken ct) => await sheets.GetAsync(id, true, ct) ?? throw new ResourceNotFoundException("MatchSheet", id);
    private static MatchSet Set(MatchSheet sheet, byte number) => sheet.Sets.SingleOrDefault(x => x.SetNumber == number) ?? throw new ResourceNotFoundException("MatchSet", number);
    private static MatchTeam Team(MatchSheet sheet, MatchSide side) => sheet.Teams.Single(x => x.Side == side);
    private static void Mutable(MatchSheet sheet) { if (sheet.Status == MatchSheetStatus.Closed) throw Conflict("match_already_closed", "Closed MatchSheet is definitive."); }
    private static void RequiredUuid(Guid id, string code) { if (id == Guid.Empty) throw Invalid(code, "A non-empty UUID is required."); }
    private static MatchEngineCommandResult Result(bool already, MatchSheet sheet, MatchSet set) => new(already, State(sheet, set));
    private static MatchSetStateDto State(MatchSheet sheet, MatchSet set) { var home = Team(sheet, MatchSide.Home); var away = Team(sheet, MatchSide.Away); var hc = Court(set, home); var ac = Court(set, away); var serving = set.CurrentServingSide; int? server = serving is null ? null : RegularServer(set, serving == MatchSide.Home ? home : away); return new(set.SetNumber, set.Status, set.HomePoints, set.AwayPoints, sheet.HomeSets, sheet.AwaySets, set.InitialServingSide, serving, server, set.HomeRotationOffset, set.AwayRotationOffset, (byte)set.Timeouts.Count(x => x.MatchTeamId == home.MatchTeamId), (byte)set.Timeouts.Count(x => x.MatchTeamId == away.MatchTeamId), set.WinnerSide, sheet.HomeSets == 3 || sheet.AwaySets == 3, hc.Select(ToDto).ToArray(), ac.Select(ToDto).ToArray()); }
    private static IReadOnlyList<CourtPlayerState> Court(MatchSet set, MatchTeam team) { var lineup = set.Lineups.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId); if (lineup is null) return []; return MatchCourtStateCalculator.Calculate(lineup, team.Side == MatchSide.Home ? set.HomeRotationOffset : set.AwayRotationOffset, set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId), set.LiberoReplacements.Where(x => x.MatchTeamId == team.MatchTeamId)); }
    private static CourtPositionDto ToDto(CourtPlayerState x) => new(x.LogicalLineupPosition, x.PhysicalPosition, x.EffectiveMatchPlayerId, x.IsLiberoReplacement);
    private static int RegularServer(MatchSet set, MatchTeam team) { var lineup = set.Lineups.Single(x => x.MatchTeamId == team.MatchTeamId); var offset = team.Side == MatchSide.Home ? set.HomeRotationOffset : set.AwayRotationOffset; return MatchCourtStateCalculator.Server(MatchCourtStateCalculator.Calculate(lineup, offset, set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId), [])); }
    private static void ConfigureLiberoPlan(MatchSheet sheet, MatchSet set, MatchTeam team, SetLineupRequest request, int[] lineup)
    {
        var current = set.LiberoPlans.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId);
        if (!sheet.TrackLiberoReplacements || !request.LiberoMatchPlayerId.HasValue || (request.LiberoLogicalPositions?.Count ?? 0) == 0) { if (current is not null) set.LiberoPlans.Remove(current); return; }
        var liberoId = request.LiberoMatchPlayerId.Value; var libero = team.Players.SingleOrDefault(x => x.MatchPlayerId == liberoId); if (libero is null || !team.Liberos.Any(x => x.MatchPlayerId == liberoId)) throw Invalid("invalid_libero_plan", "The selected player is not a declared libero for this team."); if (lineup.Contains(liberoId)) throw Invalid("invalid_libero_plan", "The libero cannot be part of the regular lineup.");
        var positions = request.LiberoLogicalPositions!.Distinct().Order().ToArray(); if (positions.Any(x => x > 5)) throw Invalid("invalid_libero_plan", "Logical positions must be between 0 (P1) and 5 (P6)."); ValidateLiberoPlan(positions); var mask = (byte)positions.Aggregate(0, (value, p) => value | (1 << p)); if (current is null) set.LiberoPlans.Add(new MatchSetLiberoPlan(set, team, libero, mask)); else current.Replace(libero, mask);
    }
    private static void ValidateLiberoPlan(IReadOnlyList<byte> positions)
    {
        for (byte offset = 0; offset < 6; offset++) foreach (var serving in new[] { true, false })
            { var eligible = positions.Count(p => { var physical = MatchCourtStateCalculator.ToPhysical((LineupPosition)(p + 1), offset); return physical is LineupPosition.P5 or LineupPosition.P6 || (physical == LineupPosition.P1 && !serving); }); if (eligible > 1) throw Invalid("ambiguous_libero_plan", "The libero plan would require two simultaneous replacements."); }
    }
    private static void ReconcileAutomaticLiberos(MatchSheet sheet, MatchSet set, DateTimeOffset now)
    {
        if (!sheet.TrackLiberoReplacements) return;
        foreach (var team in sheet.Teams)
        {
            var plan = set.LiberoPlans.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId); if (plan is null) continue; var offset = team.Side == MatchSide.Home ? set.HomeRotationOffset : set.AwayRotationOffset;
            var desired = plan.LogicalPositions.Where(p => { var physical = MatchCourtStateCalculator.ToPhysical(p, offset); return physical is LineupPosition.P5 or LineupPosition.P6 || (physical == LineupPosition.P1 && set.CurrentServingSide != team.Side); }).ToArray(); if (desired.Length > 1) throw Conflict("ambiguous_libero_plan", "The libero plan requires two simultaneous replacements.");
            var active = set.LiberoReplacements.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId && !x.ExitedAt.HasValue); var target = desired.SingleOrDefault(); if (active is not null && active.LineupPosition != target) { active.Exit(now); active = null; }
            if (target == default || active is not null) continue;
            var lineup = set.Lineups.Single(x => x.MatchTeamId == team.MatchTeamId); var regular = MatchCourtStateCalculator.Calculate(lineup, offset, set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId), []).Single(x => x.LogicalLineupPosition == target); var libero = team.Players.Single(x => x.MatchPlayerId == plan.LiberoMatchPlayerId); var replaced = team.Players.Single(x => x.MatchPlayerId == regular.EffectiveMatchPlayerId); set.LiberoReplacements.Add(new MatchLiberoReplacement(Guid.NewGuid(), set, team, libero, replaced, target, now));
        }
    }
    private static CloseMatchResult CloseResult(bool already, MatchSheet s) => new(already, s.MatchId, s.Status, s.Match.Status, s.HomeSets, s.AwaySets, s.WinnerTeamEntryId!.Value);
    private static RequestValidationException Invalid(string c, string m) => new(c, m); private static ResourceConflictException Conflict(string c, string m) => new(c, m);
}
