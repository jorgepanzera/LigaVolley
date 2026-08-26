using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Infrastructure.Persistence;
using LigaVolley.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class Livosur2026SeederTests(LigaVolleyApiFactory factory) : IClassFixture<LigaVolleyApiFactory>
{
    [Fact]
    public async Task SeedSynchronizesOnlyLivosurFormatRules()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<Livosur2026Seeder>();

        await seeder.SeedAsync();
        var roundRobin = await db.CompetitionFormats
            .Include(x => x.ScoringRules)
            .Include(x => x.TiebreakRules)
            .SingleAsync(x => x.Code == "ROUND_ROBIN");
        var splitStage = await db.CompetitionFormats
            .Include(x => x.ScoringRules)
            .Include(x => x.TiebreakRules)
            .SingleAsync(x => x.Code == "SPLIT_STAGE");
        var custom = new CompetitionFormat("CUSTOM", "Custom", null, 2, 12);
        custom.TiebreakRules.Add(new FormatTiebreakRule(1, TiebreakCriterion.PointRatio, SortDirection.Asc));
        db.CompetitionFormats.Add(custom);
        await db.SaveChangesAsync();

        roundRobin.ScoringRules.Single(x => x.WinnerSets == 3 && x.LoserSets == 0).UpdateTablePoints(9, 0);
        roundRobin.TiebreakRules.Single(x => x.Sequence == 2).UpdateConfiguration(2, TiebreakCriterion.SetRatio, SortDirection.Asc);
        roundRobin.TiebreakRules.Add(new FormatTiebreakRule(6, TiebreakCriterion.HeadToHead, SortDirection.Asc));
        await db.SaveChangesAsync();

        await seeder.SeedAsync();

        var rules = await db.CompetitionFormats
            .Include(x => x.ScoringRules)
            .Include(x => x.TiebreakRules)
            .ToDictionaryAsync(x => x.Code);
        foreach (var code in new[] { "ROUND_ROBIN", "SPLIT_STAGE" })
        {
            var format = rules[code];
            Assert.Equal<(byte WinnerSets, byte LoserSets, short WinnerTablePoints, short LoserTablePoints)>(
                new[] { ((byte)3, (byte)0, (short)2, (short)1), ((byte)3, (byte)1, (short)2, (short)1), ((byte)3, (byte)2, (short)2, (short)1) },
                format.ScoringRules.OrderBy(x => x.WinnerSets).ThenBy(x => x.LoserSets)
                    .Select(x => (x.WinnerSets, x.LoserSets, x.WinnerTablePoints, x.LoserTablePoints)).ToArray());
            Assert.Equal<(short Sequence, TiebreakCriterion Criterion, SortDirection SortDirection)>(
                new[]
                {
                    ((short)1, TiebreakCriterion.TablePoints, SortDirection.Desc),
                    ((short)2, TiebreakCriterion.MatchWins, SortDirection.Desc),
                    ((short)3, TiebreakCriterion.SetRatio, SortDirection.Desc),
                    ((short)4, TiebreakCriterion.PointRatio, SortDirection.Desc),
                    ((short)5, TiebreakCriterion.HeadToHead, SortDirection.Desc)
                },
                format.TiebreakRules.OrderBy(x => x.Sequence)
                    .Select(x => (x.Sequence, x.Criterion, x.SortDirection)).ToArray());
        }

        var unchangedCustom = rules["CUSTOM"];
        var customRule = Assert.Single(unchangedCustom.TiebreakRules);
        Assert.Equal(TiebreakCriterion.PointRatio, customRule.Criterion);
        Assert.Equal(SortDirection.Asc, customRule.SortDirection);
    }
}
