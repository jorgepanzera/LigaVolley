using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Domain.Tests;

public sealed class MatchEngineTests
{
    [Fact] public void Prepared_set_is_ready_with_zero_state(){var set=CreateSheet().PrepareSet();Assert.Equal(MatchSetStatus.Ready,set.Status);Assert.Equal((short)0,set.HomePoints);Assert.Null(set.CurrentServingSide);}
    [Theory]
    [InlineData(1,25,23,true)][InlineData(1,25,24,false)][InlineData(1,26,24,true)][InlineData(1,30,28,true)]
    [InlineData(5,15,13,true)][InlineData(5,15,14,false)][InlineData(5,16,14,true)]
    public void Set_finishes_at_target_with_two_point_difference(byte number,int home,int away,bool finished)
    {var set=Started(number);var tied=Math.Min(home,away);for(var i=0;i<tied;i++){set.ApplyPoint(MatchSide.Home,DateTimeOffset.UtcNow);set.ApplyPoint(MatchSide.Away,DateTimeOffset.UtcNow);}for(var i=tied;i<home;i++)set.ApplyPoint(MatchSide.Home,DateTimeOffset.UtcNow);for(var i=tied;i<away;i++)set.ApplyPoint(MatchSide.Away,DateTimeOffset.UtcNow);Assert.Equal(finished, set.Status==MatchSetStatus.Finished);}
    [Fact] public void Serving_team_scores_without_rotation(){var set=Started(1);set.ApplyPoint(MatchSide.Home,DateTimeOffset.UtcNow);Assert.Equal((byte)0,set.HomeRotationOffset);Assert.Equal(MatchSide.Home,set.CurrentServingSide);}
    [Fact] public void Receiving_team_wins_serve_and_rotates(){var set=Started(1);set.ApplyPoint(MatchSide.Away,DateTimeOffset.UtcNow);Assert.Equal((byte)1,set.AwayRotationOffset);Assert.Equal(MatchSide.Away,set.CurrentServingSide);}
    [Fact] public void Rotation_wraps_modulo_six(){var set=Started(1);for(var i=0;i<6;i++){set.ApplyPoint(MatchSide.Away,DateTimeOffset.UtcNow);set.ApplyPoint(MatchSide.Home,DateTimeOffset.UtcNow);}Assert.Equal((byte)0,set.HomeRotationOffset);Assert.Equal((byte)0,set.AwayRotationOffset);}
    [Theory][InlineData(LineupPosition.P1,0,LineupPosition.P1)][InlineData(LineupPosition.P1,2,LineupPosition.P5)][InlineData(LineupPosition.P3,2,LineupPosition.P1)][InlineData(LineupPosition.P6,1,LineupPosition.P5)]
    public void Logical_position_and_offset_map_to_physical(LineupPosition logical,byte offset,LineupPosition physical)=>Assert.Equal(physical,MatchCourtStateCalculator.ToPhysical(logical,offset));
    [Fact] public void Rebuilder_ignores_cancelled_point_and_reconstructs_serve_rotation()
    {
        var sheet=CreateSheet();var set=sheet.PrepareSet();set.Start(MatchSide.Home,DateTimeOffset.UtcNow);var p1=sheet.AddEvent(Guid.NewGuid(),MatchEventType.Point,set,MatchSide.Away,null,DateTimeOffset.UtcNow);sheet.AddEvent(Guid.NewGuid(),MatchEventType.Point,set,MatchSide.Away,null,DateTimeOffset.UtcNow);p1.Cancel();var state=MatchSetRebuilder.Rebuild(MatchSide.Home,sheet.Events);Assert.Equal(((short)0,(short)1,MatchSide.Away,(byte)0,(byte)1),state);
    }
    private static MatchSet Started(byte n){var set=new MatchSet(CreateSheet(),n);set.Start(MatchSide.Home,DateTimeOffset.UtcNow);return set;}
    private static MatchSheet CreateSheet(){var format=new CompetitionFormats.CompetitionFormat("T","Test",null,2,2);format.Phases.Add(new CompetitionFormats.FormatPhase("R","R",CompetitionFormats.PhaseType.RoundRobin,CompetitionFormats.PhaseRole.Regular,1,1,CompetitionFormats.FixtureMode.BalancedRandom));var c=new Competitions.Competition("C",new Seasons.Season(2026,"S",null,null),new Divisions.Division("D",1,Divisions.Gender.Female),format,Competitions.CompetitionPeriodType.Annual,null,null);var h=new TeamEntries.TeamEntry(c,new Teams.Team("H",Divisions.Gender.Female,null),null);var a=new TeamEntries.TeamEntry(c,new Teams.Team("A",Divisions.Gender.Female,null),null);var m=new Match(c,c.Phases[0],null,h,a,1,1);return new MatchSheet(m,DateTimeOffset.UtcNow);}
}
