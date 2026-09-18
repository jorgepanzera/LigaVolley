using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Domain.Tests;

public sealed class SetReplayEngineTests
{
    [Fact]
    public void Points_change_service_and_rotate_only_the_side_recovering_service()
    {
        var state = Replay(new ReplayPoint(MatchSide.Home), new ReplayPoint(MatchSide.Away), new ReplayPoint(MatchSide.Away));
        Assert.Equal((short)1, state.HomePoints); Assert.Equal((short)2, state.AwayPoints);
        Assert.Equal(MatchSide.Away, state.ServingSide); Assert.Equal((byte)0, state.HomeRotationOffset); Assert.Equal((byte)1, state.AwayRotationOffset);
    }

    [Fact]
    public void Substitutions_rebuild_regular_court_and_history()
    {
        var state = Replay(new ReplaySubstitution(MatchSide.Home, [new(1, 7), new(2, 8)]));
        Assert.Equal([7, 8, 3, 4, 5, 6], state.Home.RegularPlayers); Assert.Equal(2, state.Home.Substitutions.Count);
        Assert.Equal(8, state.Home.Court.Single(x => x.LogicalPosition == LineupPosition.P2).EffectiveMatchPlayerId);
    }

    [Fact]
    public void Timeouts_are_counted_per_side() => Assert.Equal(2, Replay(new ReplayTimeout(MatchSide.Home), new ReplayTimeout(MatchSide.Home), new ReplayTimeout(MatchSide.Away)).Home.Timeouts);

    [Fact]
    public void Sanctions_replay_penalty_and_ineligibilities()
    {
        var state = Replay(new ReplaySanction(MatchSide.Home, MatchEventType.MisconductWarning, 3),
            new ReplaySanction(MatchSide.Home, MatchEventType.MisconductPenalty),
            new ReplaySanction(MatchSide.Away, MatchEventType.Expulsion, 12),
            new ReplaySanction(MatchSide.Home, MatchEventType.Disqualification, 5));
        Assert.Equal((short)0, state.HomePoints); Assert.Equal((short)1, state.AwayPoints); Assert.Equal(MatchSide.Away, state.ServingSide);
        Assert.Contains(12, state.SetIneligiblePlayers); Assert.Contains(5, state.MatchIneligiblePlayers); Assert.Equal(4, state.Sanctions.Count);
    }

    [Fact]
    public void Libero_enter_exit_and_exchange_rebuild_effective_court()
    {
        var entered = Replay(new ReplayLiberoEnter(MatchSide.Home, 9, 5));
        Assert.Equal(9, entered.Home.Court.Single(x => x.LogicalPosition == LineupPosition.P5).EffectiveMatchPlayerId);
        Assert.True(entered.Home.Court.Single(x => x.LogicalPosition == LineupPosition.P5).IsLiberoReplacement);
        var exchanged = Replay(new ReplayLiberoEnter(MatchSide.Home, 9, 5), new ReplayLiberoEnter(MatchSide.Home, 10, 9), new ReplayLiberoExit(MatchSide.Home, 10));
        Assert.Empty(exchanged.Home.ActiveLiberos); Assert.Equal(5, exchanged.Home.Court.Single(x => x.LogicalPosition == LineupPosition.P5).EffectiveMatchPlayerId);
    }

    [Fact]
    public void Representative_sporting_sequence_is_deterministic()
    {
        SetReplayEvent[] events = [new ReplayPoint(MatchSide.Home), new ReplayPoint(MatchSide.Away),
            new ReplaySubstitution(MatchSide.Home, [new(2, 7)]), new ReplayPoint(MatchSide.Home),
            new ReplayTimeout(MatchSide.Away), new ReplayLiberoEnter(MatchSide.Home, 9, 5),
            new ReplaySanction(MatchSide.Away, MatchEventType.DelayPenalty), new ReplayPoint(MatchSide.Away)];
        var first = Replay(events); var second = Replay(events);
        Assert.Equal(first.HomePoints, second.HomePoints); Assert.Equal(first.AwayPoints, second.AwayPoints); Assert.Equal(first.ServingSide, second.ServingSide);
        Assert.Equal(first.Home.Court, second.Home.Court); Assert.Equal((short)3, first.HomePoints); Assert.Equal((short)2, first.AwayPoints);
        Assert.Equal(7, first.Home.RegularPlayers[1]); Assert.Equal(9, first.Home.Court[4].EffectiveMatchPlayerId); Assert.Equal(1, first.Away.Timeouts);
    }

    [Fact]
    public void Base_preserves_preexisting_match_disqualifications()
    {
        var state = SetReplayEngine.Replay(Base(matchIneligible: [99]), []);
        Assert.Contains(99, state.MatchIneligiblePlayers);
    }

    [Fact]
    public void Final_point_marks_set_finished_with_winner()
    {
        var events = Enumerable.Repeat<SetReplayEvent>(new ReplayPoint(MatchSide.Home), 25).ToArray();
        var state = Replay(events);
        Assert.Equal(MatchSetStatus.Finished, state.Status); Assert.Equal(MatchSide.Home, state.WinnerSide);
    }

    private static SetReplayState Replay(params SetReplayEvent[] events) => SetReplayEngine.Replay(Base(), events);
    private static SetReplayBase Base(IReadOnlyList<int>? matchIneligible = null) => new(1, MatchSide.Home, RulesSnapshot.Create(6, 2), true, true,
        new([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], [9, 10], [1, 2, 3, 4, 5, 6]),
        new([11, 12, 13, 14, 15, 16, 17], [], [11, 12, 13, 14, 15, 16]), matchIneligible);
}
