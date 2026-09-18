using LigaVolley.Domain.Fixtures;

namespace LigaVolley.Domain.MatchSheets;

// This is deliberately a pure reducer. Its caller selects the effective history.
public sealed record SetReplayTeamBase(IReadOnlyList<int> Players, IReadOnlyList<int> Liberos,
    IReadOnlyList<int> InitialLineup);

public sealed record SetReplayBase(byte SetNumber, MatchSide InitialServingSide, RulesSnapshot Rules,
    bool TrackSubstitutions, bool TrackLiberoReplacements, SetReplayTeamBase Home, SetReplayTeamBase Away,
    IReadOnlyList<int>? MatchIneligiblePlayers = null, IReadOnlyList<int>? MatchIneligibleStaff = null);

public abstract record SetReplayEvent;
public sealed record ReplayPoint(MatchSide WinningSide) : SetReplayEvent;
public sealed record ReplaySubstitution(MatchSide Side, IReadOnlyList<RulePair> Replacements) : SetReplayEvent;
public sealed record ReplayTimeout(MatchSide Side) : SetReplayEvent;
public sealed record ReplayLiberoEnter(MatchSide Side, int LiberoMatchPlayerId, int ReplacedMatchPlayerId) : SetReplayEvent;
public sealed record ReplayLiberoExit(MatchSide Side, int LiberoMatchPlayerId) : SetReplayEvent;
public sealed record ReplaySanction(MatchSide Side, MatchEventType Type, int? MatchPlayerId = null,
    int? MatchTeamStaffId = null) : SetReplayEvent;

public sealed record SetReplaySubstitution(int Position, int PlayerOutMatchPlayerId, int PlayerInMatchPlayerId);
public sealed record SetReplayLiberoReplacement(int Position, int LiberoMatchPlayerId, int ReplacedMatchPlayerId);
public sealed record SetReplayCourtPosition(LineupPosition LogicalPosition, LineupPosition PhysicalPosition,
    int EffectiveMatchPlayerId, bool IsLiberoReplacement);
public sealed record SetReplayTeamState(IReadOnlyList<int> RegularPlayers, IReadOnlyList<SetReplaySubstitution> Substitutions,
    IReadOnlyList<SetReplayLiberoReplacement> ActiveLiberos, int Timeouts, int? LastLiberoRally,
    int? LastLiberoRegular, IReadOnlyList<SetReplayCourtPosition> Court);
public sealed record SetReplaySanction(MatchSide Side, MatchEventType Type, int? MatchPlayerId, int? MatchTeamStaffId);
public sealed record SetReplayState(short HomePoints, short AwayPoints, MatchSide ServingSide, byte HomeRotationOffset,
    byte AwayRotationOffset, MatchSetStatus Status, MatchSide? WinnerSide, int RallyCount,
    SetReplayTeamState Home, SetReplayTeamState Away, IReadOnlyList<SetReplaySanction> Sanctions,
    IReadOnlyList<int> SetIneligiblePlayers, IReadOnlyList<int> MatchIneligiblePlayers,
    IReadOnlyList<int> SetIneligibleStaff, IReadOnlyList<int> MatchIneligibleStaff);

public static class SetReplayEngine
{
    public static SetReplayState Replay(SetReplayBase baseState, IReadOnlyList<SetReplayEvent> events)
    {
        ArgumentNullException.ThrowIfNull(baseState);
        ArgumentNullException.ThrowIfNull(events);
        var home = new Team(baseState.Home);
        var away = new Team(baseState.Away);
        short hp = 0, ap = 0; byte ho = 0, ao = 0; var serving = baseState.InitialServingSide;
        var sanctions = new List<SetReplaySanction>();
        var setIneligiblePlayers = new HashSet<int>(); var matchIneligiblePlayers = new HashSet<int>(baseState.MatchIneligiblePlayers ?? []);
        var setIneligibleStaff = new HashSet<int>(); var matchIneligibleStaff = new HashSet<int>(baseState.MatchIneligibleStaff ?? []);

        void Point(MatchSide side)
        {
            if (side == MatchSide.Home) hp++; else ap++;
            if (serving != side) { if (side == MatchSide.Home) ho = (byte)((ho + 1) % 6); else ao = (byte)((ao + 1) % 6); }
            serving = side;
        }
        foreach (var item in events)
        {
            switch (item)
            {
                case ReplayPoint p: Point(p.WinningSide); break;
                case ReplaySubstitution s:
                    var substituteTeam = TeamFor(s.Side);
                    foreach (var pair in s.Replacements)
                    {
                        var position = substituteTeam.Regular.IndexOf(pair.PlayerOutMatchPlayerId);
                        if (position >= 0) { substituteTeam.Regular[position] = pair.PlayerInMatchPlayerId; substituteTeam.Substitutions.Add(new(position, pair.PlayerOutMatchPlayerId, pair.PlayerInMatchPlayerId)); }
                    }
                    break;
                case ReplayTimeout timeout: TeamFor(timeout.Side).Timeouts++; break;
                case ReplayLiberoEnter enter:
                    var enterTeam = TeamFor(enter.Side); var enterPosition = enterTeam.EffectiveIndexOf(enter.ReplacedMatchPlayerId);
                    if (enterPosition >= 0)
                    {
                        enterTeam.ActiveLiberos.RemoveAll(x => x.Position == enterPosition);
                        // A valid exchange replaces the only active libero while preserving the logical regular player.
                        if (enterTeam.ActiveLiberos.Count > 0) enterTeam.ActiveLiberos.Clear();
                        enterTeam.ActiveLiberos.Add(new(enterPosition, enter.LiberoMatchPlayerId, enterTeam.Regular[enterPosition]));
                        enterTeam.LastLiberoRally = hp + ap;
                        enterTeam.LastLiberoRegular = enterTeam.Regular[enterPosition];
                    }
                    break;
                case ReplayLiberoExit exit:
                    var exitTeam = TeamFor(exit.Side); exitTeam.ActiveLiberos.RemoveAll(x => x.LiberoMatchPlayerId == exit.LiberoMatchPlayerId);
                    exitTeam.LastLiberoRally = hp + ap;
                    break;
                case ReplaySanction sanction:
                    sanctions.Add(new(sanction.Side, sanction.Type, sanction.MatchPlayerId, sanction.MatchTeamStaffId));
                    if (sanction.Type == MatchEventType.Expulsion) { if (sanction.MatchPlayerId.HasValue) setIneligiblePlayers.Add(sanction.MatchPlayerId.Value); if (sanction.MatchTeamStaffId.HasValue) setIneligibleStaff.Add(sanction.MatchTeamStaffId.Value); }
                    if (sanction.Type == MatchEventType.Disqualification) { if (sanction.MatchPlayerId.HasValue) matchIneligiblePlayers.Add(sanction.MatchPlayerId.Value); if (sanction.MatchTeamStaffId.HasValue) matchIneligibleStaff.Add(sanction.MatchTeamStaffId.Value); }
                    if (sanction.Type is MatchEventType.MisconductPenalty or MatchEventType.DelayPenalty) Point(sanction.Side == MatchSide.Home ? MatchSide.Away : MatchSide.Home);
                    break;
            }
        }
        var target = baseState.SetNumber == 5 ? 15 : 25;
        var finished = (hp >= target || ap >= target) && Math.Abs(hp - ap) >= 2;
        MatchSide? winner = finished ? hp > ap ? MatchSide.Home : MatchSide.Away : null;
        var rallyCount = hp + ap;
        return new(hp, ap, serving, ho, ao, finished ? MatchSetStatus.Finished : MatchSetStatus.InProgress, winner, rallyCount,
            State(home, ho), State(away, ao), sanctions, setIneligiblePlayers.Order().ToArray(), matchIneligiblePlayers.Order().ToArray(),
            setIneligibleStaff.Order().ToArray(), matchIneligibleStaff.Order().ToArray());

        Team TeamFor(MatchSide side) => side == MatchSide.Home ? home : away;
    }

    private static SetReplayTeamState State(Team team, byte offset)
    {
        var court = team.Regular.Select((player, index) => new { player, index }).Select(x =>
        {
            var libero = team.ActiveLiberos.SingleOrDefault(l => l.Position == x.index);
            return new SetReplayCourtPosition((LineupPosition)(x.index + 1), MatchCourtStateCalculator.ToPhysical((LineupPosition)(x.index + 1), offset),
                libero?.LiberoMatchPlayerId ?? x.player, libero is not null);
        }).ToArray();
        return new(team.Regular.ToArray(), team.Substitutions.ToArray(), team.ActiveLiberos.ToArray(), team.Timeouts,
            team.LastLiberoRally, team.LastLiberoRegular, court);
    }

    private sealed class Team(SetReplayTeamBase value)
    {
        public List<int> Regular { get; } = value.InitialLineup.ToList();
        public List<SetReplaySubstitution> Substitutions { get; } = [];
        public List<SetReplayLiberoReplacement> ActiveLiberos { get; } = [];
        public int Timeouts { get; set; }
        public int? LastLiberoRally { get; set; }
        public int? LastLiberoRegular { get; set; }
        public int EffectiveIndexOf(int player) { var libero = ActiveLiberos.FindIndex(x => x.LiberoMatchPlayerId == player); return libero >= 0 ? ActiveLiberos[libero].Position : Regular.IndexOf(player); }
    }
}
