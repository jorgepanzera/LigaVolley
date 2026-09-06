using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;

namespace LigaVolley.Domain.Tests;

public sealed class CompetitionTests
{
    [Fact]
    public void Match_rules_resolve_nullable_overrides_without_mutating_frozen_snapshot()
    {
        var competition = Create();
        Assert.Equal(6, competition.EffectiveMatchRules().MaxSubstitutionsPerSet);
        competition.ConfigureMatchRules(8, null);
        var frozen = competition.EffectiveMatchRules();
        Assert.Equal(8, frozen.MaxSubstitutionsPerSet);
        Assert.Equal(2, frozen.MaxTimeoutsPerSet);
        competition.ConfigureMatchRules(null, 3);
        Assert.Equal(6, competition.EffectiveMatchRules().MaxSubstitutionsPerSet);
        Assert.Equal(3, competition.EffectiveMatchRules().MaxTimeoutsPerSet);
        Assert.Equal(8, frozen.MaxSubstitutionsPerSet);
        Assert.Throws<DomainValidationException>(() => competition.ConfigureMatchRules(0, null));
        Assert.Throws<DomainValidationException>(() => competition.ConfigureMatchRules(null, 100));
    }
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
