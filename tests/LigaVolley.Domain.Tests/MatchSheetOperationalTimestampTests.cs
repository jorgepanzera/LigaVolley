using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;

namespace LigaVolley.Domain.Tests;

public sealed class MatchSheetOperationalTimestampTests
{
    [Fact]
    public void Operational_timestamp_is_server_mutation_metadata_and_starts_null()
    {
        var format=new CompetitionFormat("TS","Timestamp",null,2,2);format.Phases.Add(new FormatPhase("R","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom));
        var competition=new Competition("Test",new Season(2026,"2026",null,null),new Division("A",1,Gender.Female),format,CompetitionPeriodType.Annual,null,null);var phase=competition.Phases.Single();var home=new TeamEntry(competition,new Team("Home",Gender.Female,null),1);var away=new TeamEntry(competition,new Team("Away",Gender.Female,null),2);var match=new Match(competition,phase,(CompetitionPhaseGroup?)null,home,away,1,1);var sheet=new MatchSheet(match,DateTimeOffset.UtcNow);
        Assert.Null(sheet.LastOperationalUpdateAt);var at=DateTimeOffset.UtcNow;sheet.TouchOperationalState(at);Assert.Equal(at,sheet.LastOperationalUpdateAt);var eventAt=at.AddSeconds(1);sheet.AddEvent(Guid.NewGuid(),MatchEventType.PrepareSet,null,null,null,eventAt);Assert.Equal(eventAt,sheet.LastOperationalUpdateAt);
    }
}
