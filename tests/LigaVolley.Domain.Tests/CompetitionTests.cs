using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;

namespace LigaVolley.Domain.Tests;

public sealed class CompetitionTests
{
    [Fact]
    public void Constructor_RequiresValidDatesAndStartsInDraft()
    {
        var competition = Create();
        Assert.Equal(CompetitionStatus.Draft, competition.Status);
        Assert.Throws<DomainValidationException>(() => new Competition("Bad", new Season(2026,"2026",null,null), new Division("A",1,Gender.Female), Format(), CompetitionPeriodType.Annual, new(2026,2,1), new(2026,1,1)));
    }

    [Fact]
    public void ChangeStatus_AllowsInitialCancellationButNotSportsTransitions()
    {
        var competition = Create();
        Assert.Throws<DomainValidationException>(() => competition.ChangeStatus(CompetitionStatus.Scheduled));
        competition.ChangeStatus(CompetitionStatus.Cancelled);
        Assert.Equal(CompetitionStatus.Cancelled, competition.Status);
    }

    private static Competition Create() => new("League", new Season(2026,"2026",null,null), new Division("A",1,Gender.Female), Format(), CompetitionPeriodType.Annual, null, null);
    private static CompetitionFormat Format() { var f = new CompetitionFormat("RR", "Round robin", null, 4, 8); f.Phases.Add(new FormatPhase("REG", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom)); return f; }
}
