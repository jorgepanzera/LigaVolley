using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.Tests;

public sealed class MatchEngineRuleTests
{
    [Fact] public void Rebuild_replays_only_active_points_in_sequence()
    {
        var sheet=TestSheet();var set=sheet.PrepareSet();set.Start(MatchSide.Away,DateTimeOffset.UtcNow);
        sheet.AddEvent(Guid.NewGuid(),MatchEventType.Point,set,MatchSide.Home,null,DateTimeOffset.UtcNow);
        var cancelled=sheet.AddEvent(Guid.NewGuid(),MatchEventType.Point,set,MatchSide.Away,null,DateTimeOffset.UtcNow);cancelled.Cancel();
        sheet.AddEvent(Guid.NewGuid(),MatchEventType.Point,set,MatchSide.Home,null,DateTimeOffset.UtcNow);
        var result=MatchSetRebuilder.Rebuild(MatchSide.Away,sheet.Events);
        Assert.Equal(((short)2,(short)0,MatchSide.Home,(byte)1,(byte)0),result);
    }
    private static MatchSheet TestSheet(){var f=new LigaVolley.Domain.CompetitionFormats.CompetitionFormat("F","F",null,2,2);f.Phases.Add(new LigaVolley.Domain.CompetitionFormats.FormatPhase("P","P",LigaVolley.Domain.CompetitionFormats.PhaseType.RoundRobin,LigaVolley.Domain.CompetitionFormats.PhaseRole.Regular,1,1,LigaVolley.Domain.CompetitionFormats.FixtureMode.BalancedRandom));var c=new LigaVolley.Domain.Competitions.Competition("C",new LigaVolley.Domain.Seasons.Season(2026,"S",null,null),new LigaVolley.Domain.Divisions.Division("D",1,LigaVolley.Domain.Divisions.Gender.Female),f,LigaVolley.Domain.Competitions.CompetitionPeriodType.Annual,null,null);var h=new LigaVolley.Domain.TeamEntries.TeamEntry(c,new LigaVolley.Domain.Teams.Team("H",LigaVolley.Domain.Divisions.Gender.Female,null),null);var a=new LigaVolley.Domain.TeamEntries.TeamEntry(c,new LigaVolley.Domain.Teams.Team("A",LigaVolley.Domain.Divisions.Gender.Female,null),null);return new MatchSheet(new Match(c,c.Phases[0],null,h,a,1,1),DateTimeOffset.UtcNow);}
}
