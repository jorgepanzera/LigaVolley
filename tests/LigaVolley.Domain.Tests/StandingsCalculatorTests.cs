using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Standings;

namespace LigaVolley.Domain.Tests;

public sealed class StandingsCalculatorTests
{
    private readonly StandingsCalculator calculator = new();
    private static readonly StandingsTeam[] Teams = [new(1, 101, "A"), new(2, 102, "B"), new(3, 103, "C")];
    private static readonly StandingsTiebreakRule[] StandardTiebreaks =
    [new(1,TiebreakCriterion.TablePoints,SortDirection.Desc),new(2,TiebreakCriterion.MatchWins,SortDirection.Desc),new(3,TiebreakCriterion.SetRatio,SortDirection.Desc),new(4,TiebreakCriterion.PointRatio,SortDirection.Desc),new(5,TiebreakCriterion.HeadToHead,SortDirection.Desc)];

    [Fact]
    public void SameThreeTwoResult_UsesOnlyConfiguredScoringRule()
    {
        var match = Match(1, 2, 3, 2, 1, [(25,20),(20,25),(25,21),(22,25),(15,10)]);
        var twoOne = calculator.Calculate(Teams[..2], [match], [new(3,2,2,1)], StandardTiebreaks);
        var threeZero = calculator.Calculate(Teams[..2], [match], [new(3,2,3,0)], StandardTiebreaks);
        Assert.Equal((2,1), (twoOne[0].TablePoints, twoOne[1].TablePoints));
        Assert.Equal((3,0), (threeZero[0].TablePoints, threeZero[1].TablePoints));
    }

    [Fact]
    public void AccumulatesMatchSetAndPointStatistics()
    {
        var positions = calculator.Calculate(Teams[..2], [Match(1,2,3,1,1,[(25,20),(25,18),(22,25),(25,21)])], [new(3,1,2,1)], StandardTiebreaks);
        var winner = positions.Single(x => x.TeamEntryId == 1);
        Assert.Equal((1,1,0,3,1,97,84,2), (winner.Played,winner.Won,winner.Lost,winner.SetsWon,winner.SetsLost,winner.PointsWon,winner.PointsLost,winner.TablePoints));
        Assert.Equal(1, positions.Single(x => x.TeamEntryId == 2).Lost);
    }

    [Fact]
    public void RatiosWithZeroDenominatorAreNullAndRankAsInfinityDescending()
    {
        var matches = new[] { Match(1,2,3,0,1,[(25,0),(25,0),(25,0)]), Match(3,2,3,1,3,[(25,20),(20,25),(25,20),(25,20)]) };
        var positions = calculator.Calculate(Teams, matches, [new(3,0,2,1),new(3,1,2,1)], [new(1,TiebreakCriterion.SetRatio,SortDirection.Desc)]);
        var unbeaten = positions.Single(x => x.TeamEntryId == 1);
        Assert.Null(unbeaten.SetRatio); Assert.Null(unbeaten.PointRatio); Assert.Equal(1, unbeaten.Position);
    }

    [Fact]
    public void AppliesCriteriaSequentiallyAndHeadToHeadOnlyForTwoTeams()
    {
        var matches = new[] { Match(1,2,3,0,1,[(25,20),(25,20),(25,20)]), Match(2,1,3,0,2,[(25,20),(25,20),(25,20)]) };
        var positions = calculator.Calculate(Teams[..2], matches, [new(3,0,1,0)], [new(1,TiebreakCriterion.TablePoints,SortDirection.Desc),new(2,TiebreakCriterion.HeadToHead,SortDirection.Desc)]);
        Assert.All(positions, x => Assert.True(x.IsTied)); Assert.All(positions, x => Assert.Equal(1, x.Position));

        var threeWay = calculator.Calculate(Teams, [], [], [new(1,TiebreakCriterion.HeadToHead,SortDirection.Desc)]);
        Assert.All(threeWay, x => Assert.True(x.IsTied)); Assert.All(threeWay, x => Assert.Equal(1, x.Position));
    }

    [Fact]
    public void MatchWinsPointRatioAndResolvedHeadToHeadAreSupported()
    {
        var wins = calculator.Calculate(Teams, [Match(1,2,3,0,1,[(25,20),(25,20),(25,20)]),Match(1,3,3,0,1,[(25,20),(25,20),(25,20)]),Match(2,3,3,0,2,[(25,20),(25,20),(25,20)])], [new(3,0,0,0)], [new(1,TiebreakCriterion.MatchWins,SortDirection.Desc)]);
        Assert.Equal(1,wins[0].TeamEntryId);

        var cycle = new[] { Match(1,2,3,0,1,[(25,10),(25,10),(25,10)]), Match(2,3,3,0,2,[(25,20),(25,20),(25,20)]), Match(3,1,3,0,3,[(25,24),(25,24),(25,24)]) };
        var points = calculator.Calculate(Teams,cycle,[new(3,0,0,0)],[new(1,TiebreakCriterion.PointRatio,SortDirection.Desc)]);
        Assert.Equal(new[]{1,3,2},points.Select(x=>x.TeamEntryId));

        var direct = calculator.Calculate(Teams[..2],[Match(1,2,3,0,1,[(25,20),(25,20),(25,20)])],[new(3,0,0,0)],[new(1,TiebreakCriterion.HeadToHead,SortDirection.Desc)]);
        Assert.Equal(1,direct[0].TeamEntryId); Assert.False(direct[0].IsTied);
    }

    [Fact]
    public void UnresolvedTieSharesCompetitionPositionAndUsesTechnicalIdOrder()
    {
        var positions = calculator.Calculate(Teams, [], [], StandardTiebreaks);
        Assert.Equal(new[]{1,2,3}, positions.Select(x=>x.TeamEntryId)); Assert.All(positions,x=>Assert.Equal(1,x.Position)); Assert.All(positions,x=>Assert.True(x.IsTied));
    }

    [Fact]
    public void MissingScoringRuleAndInvalidFinishedResultHaveStableCodes()
    {
        var match = Match(1,2,3,2,1,[(25,20),(20,25),(25,21),(22,25),(15,10)]);
        var missing = Assert.Throws<StandingsCalculationException>(()=>calculator.Calculate(Teams[..2],[match],[new(3,0,3,0)],StandardTiebreaks));
        Assert.Equal("standings_scoring_rule_missing",missing.Code);
        var invalid = match with { WinnerTeamEntryId = 2 };
        var incoherent = Assert.Throws<StandingsCalculationException>(()=>calculator.Calculate(Teams[..2],[invalid],[new(3,2,2,1)],StandardTiebreaks));
        Assert.Equal("standings_match_result_invalid",incoherent.Code);
        var invalidRules=Assert.Throws<StandingsCalculationException>(()=>calculator.Calculate(Teams,[],[],[new(1,TiebreakCriterion.TablePoints,SortDirection.Desc),new(1,TiebreakCriterion.MatchWins,SortDirection.Desc)]));
        Assert.Equal("standings_tiebreak_configuration_invalid",invalidRules.Code);
    }

    private static StandingsMatch Match(int home,int away,byte hs,byte @as,int winner,(short H,short A)[] sets)
        => new(home*100+away,home,away,hs,@as,winner,sets.Select((x,i)=>new StandingsSet((byte)(i+1),x.H,x.A)).ToArray());
}
