using LigaVolley.Domain.Fixtures;
namespace LigaVolley.Domain.Tests;
public sealed class RoundRobinFixtureGeneratorTests
{
    [Fact] public void MirroredEightTeams_HasExactMirrorAndBalancedLocality()
    {
        var fixture=RoundRobinFixtureGenerator.Generate(Enumerable.Range(1,8).ToArray(),12345,true);
        Assert.Equal(56,fixture.Count); Assert.Equal(14,fixture.Max(x=>x.RoundNumber));
        foreach(var team in Enumerable.Range(1,8)){Assert.Equal(14,fixture.Count(x=>x.HomeParticipantId==team||x.AwayParticipantId==team));Assert.Equal(7,fixture.Count(x=>x.HomeParticipantId==team));Assert.Equal(7,fixture.Count(x=>x.AwayParticipantId==team));}
        foreach(var pair in fixture.GroupBy(x=>Ordered(x.HomeParticipantId,x.AwayParticipantId))){Assert.Equal(2,pair.Count());Assert.Equal(2,pair.Select(x=>x.HomeParticipantId).Distinct().Count());}
        foreach(var first in fixture.Where(x=>x.RoundNumber<=7)){var mirror=Assert.Single(fixture.Where(x=>x.RoundNumber==first.RoundNumber+7&&x.HomeParticipantId==first.AwayParticipantId&&x.AwayParticipantId==first.HomeParticipantId));Assert.NotNull(mirror);}
    }
    [Fact] public void BalancedTenTeams_HasAllPairsAndOptimalGlobalBalance()
    {
        var fixture=RoundRobinFixtureGenerator.Generate(Enumerable.Range(1,10).ToArray(),12345,false);
        Assert.Equal(45,fixture.Count);Assert.Equal(9,fixture.Max(x=>x.RoundNumber));Assert.All(fixture.GroupBy(x=>Ordered(x.HomeParticipantId,x.AwayParticipantId)),x=>Assert.Single(x));
        foreach(var round in fixture.GroupBy(x=>x.RoundNumber))Assert.Equal(10,round.SelectMany(x=>new[]{x.HomeParticipantId,x.AwayParticipantId}).Distinct().Count());
        var home=Enumerable.Range(1,10).Select(t=>fixture.Count(x=>x.HomeParticipantId==t)).ToArray();Assert.Equal(5,home.Count(x=>x==5));Assert.Equal(5,home.Count(x=>x==4));Assert.DoesNotContain(home,x=>x is <4 or >5);
    }
    [Fact] public void BalancedFiveTeams_UsesByeWithoutCreatingByeMatches()
    {
        var fixture=RoundRobinFixtureGenerator.Generate(Enumerable.Range(1,5).ToArray(),99,false);
        Assert.Equal(10,fixture.Count);Assert.Equal(5,fixture.Max(x=>x.RoundNumber));
        foreach(var team in Enumerable.Range(1,5)){Assert.Equal(4,fixture.Count(x=>x.HomeParticipantId==team||x.AwayParticipantId==team));Assert.Equal(2,fixture.Count(x=>x.HomeParticipantId==team));Assert.Equal(2,fixture.Count(x=>x.AwayParticipantId==team));Assert.Equal(4,fixture.Select(x=>x.RoundNumber).Distinct().Count(r=>fixture.Any(x=>x.RoundNumber==r&&(x.HomeParticipantId==team||x.AwayParticipantId==team))));}
        Assert.All(fixture.GroupBy(x=>x.RoundNumber),x=>Assert.Equal(2,x.Count()));
    }
    [Fact] public void SameParticipantsAndSeed_IsExactlyReproducible()
    { var first=RoundRobinFixtureGenerator.Generate(Enumerable.Range(1,10).ToArray(),777,false);var second=RoundRobinFixtureGenerator.Generate(Enumerable.Range(1,10).Reverse().ToArray(),777,false);Assert.NotEqual(first,second);Assert.Equal(first,RoundRobinFixtureGenerator.Generate(Enumerable.Range(1,10).ToArray(),777,false)); }
    private static (int,int) Ordered(int a,int b)=>a<b?(a,b):(b,a);
}
