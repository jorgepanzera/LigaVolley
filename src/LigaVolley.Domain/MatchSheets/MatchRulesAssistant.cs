namespace LigaVolley.Domain.MatchSheets;

// Plain, immutable inputs also used by the shared C#/TypeScript parity vectors.
public sealed record RuleSubstitution(int Position, int PlayerOutMatchPlayerId, int PlayerInMatchPlayerId);
public sealed record RuleReplacement(int Position, int LiberoMatchPlayerId, int ReplacedMatchPlayerId);
public sealed record RuleTeamState(int[] Players, int[] Liberos, int[] Lineup,
    IReadOnlyList<RuleSubstitution> Substitutions, IReadOnlyList<RuleReplacement> ActiveLiberos,
    int RotationOffset, int Timeouts, int? LastLiberoRally = null, int? LastLiberoRegular = null,
    IReadOnlyList<int>? IneligiblePlayers = null);
public sealed record RuleMatchState(bool Closed, string SetStatus, string? ServingSide, int RallyCount,
    bool TrackSubstitutions, bool TrackLiberoReplacements, RuleTeamState Home, RuleTeamState Away);
public sealed record RulePair(int PlayerOutMatchPlayerId, int PlayerInMatchPlayerId);
public sealed record RuleCommand(string Type, string Side, IReadOnlyList<RulePair>? Replacements = null,
    int? LiberoMatchPlayerId = null, int? ReplacedMatchPlayerId = null, int? ObservedServerMatchPlayerId = null);

public static class MatchRulesAssistant
{
    public static int[] Regular(RuleTeamState team) => MatchCourtStateCalculator.RegularPlayers(team.Lineup,
        team.Substitutions.Select(x => (x.Position, x.PlayerOutMatchPlayerId, x.PlayerInMatchPlayerId)));

    public static RuleEvaluation Evaluate(RuleMatchState state, RulesSnapshot rules, RuleCommand command)
    {
        var hard = new List<RuleIssue>();
        var warnings = new List<RuleIssue>();
        void Hard(string code) => hard.Add(new(code, new Dictionary<string, int>(), false));
        void Warn(string code, params (string Key, int Value)[] context)
        {
            if (warnings.All(x => x.Code != code))
                warnings.Add(new(code, context.ToDictionary(x => x.Key, x => x.Value), true));
        }
        RuleEvaluation Result() => new(hard, warnings);
        if (state.Closed) Hard("match_already_closed");
        if (state.SetStatus != "IN_PROGRESS") Hard("match_set_invalid_state");
        if (command.Side is not "HOME" and not "AWAY") Hard("invalid_side");
        if (hard.Count > 0) return Result();
        var team = command.Side == "HOME" ? state.Home : state.Away;
        var ineligible = team.IneligiblePlayers ?? [];
        var regular = Regular(team);
        var effective = regular.ToArray();
        foreach (var l in team.ActiveLiberos)
        {
            if (l.Position < 0 || l.Position >= effective.Length) Hard("invalid_libero_replacement");
            else effective[l.Position] = l.LiberoMatchPlayerId;
        }
        if (regular.Length != 6 || regular.Distinct().Count() != 6 || effective.Distinct().Count() != 6)
            Hard("invalid_court_state");
        if (command.Type is not "SUBSTITUTION" and not "SUBSTITUTION_REQUEST" && effective.Any(ineligible.Contains))
            Hard("sanctioned_player_must_leave_court");
        if (hard.Count > 0) return Result();
        switch (command.Type)
        {
            case "SUBSTITUTION_REQUEST":
            case "SUBSTITUTION":
            {
                if (!state.TrackSubstitutions) { Hard("substitution_tracking_disabled"); break; }
                var pairs = command.Replacements ?? [];
                if (pairs.Count == 0 || pairs.Select(x => x.PlayerOutMatchPlayerId).Distinct().Count() != pairs.Count ||
                    pairs.Select(x => x.PlayerInMatchPlayerId).Distinct().Count() != pairs.Count)
                { Hard("invalid_substitution"); break; }
                var final = regular.ToArray();
                foreach (var pair in pairs)
                {
                    var position = Array.IndexOf(regular, pair.PlayerOutMatchPlayerId);
                    if (!team.Players.Contains(pair.PlayerOutMatchPlayerId) || !team.Players.Contains(pair.PlayerInMatchPlayerId) ||
                        position < 0 || pair.PlayerOutMatchPlayerId == pair.PlayerInMatchPlayerId)
                    { Hard("invalid_substitution"); continue; }
                    if (ineligible.Contains(pair.PlayerInMatchPlayerId)) { Hard("sanctioned_player_ineligible"); continue; }
                    final[position] = pair.PlayerInMatchPlayerId;
                    var starter = team.Lineup[position];
                    var history = team.Substitutions.Where(x => x.Position == position).ToArray();
                    if (pair.PlayerOutMatchPlayerId == starter && history.Length > 0 ||
                        pair.PlayerOutMatchPlayerId != starter && (history.Length != 1 || history[0].PlayerInMatchPlayerId != pair.PlayerOutMatchPlayerId))
                        Warn("substitution_reentry_irregular", ("position", position));
                    if (team.Substitutions.Any(x => x.Position != position &&
                        (x.PlayerInMatchPlayerId == pair.PlayerInMatchPlayerId || x.PlayerOutMatchPlayerId == pair.PlayerInMatchPlayerId)) ||
                        team.Lineup.Where((_, i) => i != position).Contains(pair.PlayerInMatchPlayerId))
                        Warn("substitution_player_already_bound", ("playerInMatchPlayerId", pair.PlayerInMatchPlayerId));
                    if (pair.PlayerOutMatchPlayerId != starter && pair.PlayerInMatchPlayerId != starter)
                        Warn("substitution_original_player_mismatch", ("expectedMatchPlayerId", starter));
                    if (team.Liberos.Contains(pair.PlayerInMatchPlayerId) || team.Liberos.Contains(pair.PlayerOutMatchPlayerId))
                        Warn("libero_used_as_regular_substitute");
                }
                if (final.Distinct().Count() != 6) Hard("substitution_player_already_on_court");
                foreach (var l in team.ActiveLiberos) final[l.Position] = l.LiberoMatchPlayerId;
                if (final.Distinct().Count() != 6) Hard("duplicate_effective_player");
                if (final.Count(team.Liberos.Contains) > 1) Hard("invalid_libero_replacement");
                if (rules.MaxSubstitutionsPerSet is { } max && team.Substitutions.Count + pairs.Count > max)
                    Warn("substitution_limit_exceeded", ("used", team.Substitutions.Count), ("requested", pairs.Count),
                        ("projected", team.Substitutions.Count + pairs.Count), ("maximum", max));
                break;
            }
            case "TIMEOUT":
                if (team.Timeouts >= rules.MaxTimeoutsPerSet)
                    Warn("timeout_limit_exceeded", ("used", team.Timeouts), ("maximum", rules.MaxTimeoutsPerSet));
                break;
            case "POINT":
            {
                if (state.TrackLiberoReplacements)
                    foreach (var (courtTeam, side) in new[] { (state.Home, "HOME"), (state.Away, "AWAY") })
                        foreach (var active in courtTeam.ActiveLiberos)
                        {
                            var physical = ((active.Position - courtTeam.RotationOffset + 6) % 6) + 1;
                            if (physical is 2 or 3 or 4) Warn("libero_in_front_row", ("physicalPosition", physical));
                            if (physical == 1 && state.ServingSide == side && !rules.LiberoCanServe) Warn("libero_service_not_allowed");
                        }
                if (command.ObservedServerMatchPlayerId is not { } observed) break;
                if (state.ServingSide is not "HOME" and not "AWAY") { Hard("invalid_court_state"); break; }
                var serving = state.ServingSide == "HOME" ? state.Home : state.Away;
                if (!serving.Players.Contains(observed)) { Hard("server_player_wrong_team"); break; }
                var expected = Regular(serving)[serving.RotationOffset % 6];
                if (observed != expected) Warn("unexpected_server", ("expectedMatchPlayerId", expected), ("observedMatchPlayerId", observed));
                if (serving.Liberos.Contains(observed) && !rules.LiberoCanServe)
                    Warn("libero_service_not_allowed", ("observedMatchPlayerId", observed));
                break;
            }
            case "LIBERO_ENTER":
            {
                if (!state.TrackLiberoReplacements) { Hard("libero_tracking_disabled"); break; }
                var libero = command.LiberoMatchPlayerId ?? 0;
                var replaced = command.ReplacedMatchPlayerId ?? 0;
                if (!team.Liberos.Contains(libero)) { Hard("libero_not_declared"); break; }
                if (ineligible.Contains(libero)) { Hard("sanctioned_player_ineligible"); break; }
                var position = Array.IndexOf(effective, replaced);
                if (position < 0 || !team.Players.Contains(replaced)) { Hard("libero_invalid_replaced_player"); break; }
                if (effective.Contains(libero)) { Hard("libero_already_on_court"); break; }
                if (effective.Where((_, i) => i != position).Count(team.Liberos.Contains) > 0) { Hard("invalid_libero_replacement"); break; }
                var current = team.ActiveLiberos.SingleOrDefault(x => x.Position == position);
                // Switching the acting libero preserves the regular logical occupant.
                if (team.ActiveLiberos.Count > 0 && current is null)
                    Hard("invalid_libero_replacement");
                if (current is null && team.LastLiberoRegular.HasValue && team.LastLiberoRegular != regular[position])
                    Warn("libero_wrong_regular_replacement", ("expectedMatchPlayerId", team.LastLiberoRegular.Value));
                if (team.LastLiberoRally == state.RallyCount) Warn("libero_replacement_without_completed_rally");
                var physical = ((position - team.RotationOffset + 6) % 6) + 1;
                if (physical is 2 or 3 or 4) Warn("libero_in_front_row", ("physicalPosition", physical));
                if (physical == 1 && state.ServingSide == command.Side && !rules.LiberoCanServe)
                    Warn("libero_service_not_allowed");
                break;
            }
            case "LIBERO_EXIT":
                if (!state.TrackLiberoReplacements) Hard("libero_tracking_disabled");
                if (!team.ActiveLiberos.Any(x => x.LiberoMatchPlayerId == command.LiberoMatchPlayerId)) Hard("invalid_libero_replacement");
                if (team.LastLiberoRally == state.RallyCount) Warn("libero_replacement_without_completed_rally");
                break;
            default:
                Hard("sync_invalid_event_type");
                break;
        }
        return Result();
    }
}
