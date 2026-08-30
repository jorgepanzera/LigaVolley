using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.PhaseCompletion;
using LigaVolley.Application.PlayoffProgression;
using LigaVolley.Application.Seasons;
using LigaVolley.Application.TeamEntries;
using LigaVolley.Application.Teams;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class PlayoffProgressionTests : IClassFixture<LigaVolleyApiFactory>
{
    private readonly LigaVolleyApiFactory factory;
    private static readonly JsonSerializerOptions Json = Options();

    public PlayoffProgressionTests(LigaVolleyApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task ProgressionIsIdempotentConcurrentAndPropagatesWinnerAndLoser()
    {
        var setup = await CreateCompetitionAsync();
        var (sf1Match1, sf2Match1) = await FinishInitialSemifinalMatches(setup.CompetitionId);

        var sameMatchResults = await Task.WhenAll(Process(sf1Match1), Process(sf1Match1));
        Assert.All(sameMatchResults, x => Assert.Equal(PlayoffSeriesStatus.InProgress, x.SeriesStatus));

        int sf1Match2;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var sf1 = await db.Set<CompetitionPlayoffSeries>().SingleAsync(x => x.CompetitionId == setup.CompetitionId && x.Code == "SF1");
            var matches = await db.Matches.Where(x => x.SeriesId == sf1.PlayoffSeriesId).OrderBy(x => x.MatchNumber).ToListAsync();
            Assert.Equal(2, matches.Count);
            Assert.Equal(matches[0].AwayTeamEntryId, matches[1].HomeTeamEntryId);
            Assert.Equal(matches[0].HomeTeamEntryId, matches[1].AwayTeamEntryId);
            Assert.Null(matches[1].MatchDate);
            Assert.Null(matches[1].VenueId);
            Assert.Equal(MatchStatus.Pending, matches[1].Status);
            sf1Match2 = matches[1].MatchId;
        }

        await FinishMatch(sf1Match2, winnerIsHome: false);
        var concurrentSemifinals = await Task.WhenAll(Process(sf1Match2), Process(sf2Match1));
        Assert.All(concurrentSemifinals, x => Assert.Equal(PlayoffSeriesStatus.Finished, x.SeriesStatus));

        using var verificationScope = factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var phases = await verification.Set<CompetitionPhase>().Include(x => x.Series)
            .Where(x => x.CompetitionId == setup.CompetitionId).ToListAsync();
        var semifinal = phases.Single(x => x.Code == "SEMIS");
        var final = phases.Single(x => x.Code == "FINAL").Series.Single();
        var third = phases.Single(x => x.Code == "THIRD").Series.Single();

        Assert.Equal(CompetitionPhaseStatus.Finished, semifinal.Status);
        Assert.Equal(PlayoffSeriesStatus.Ready, final.Status);
        Assert.Equal(PlayoffSeriesStatus.Ready, third.Status);
        Assert.NotNull(final.Team1EntryId);
        Assert.NotNull(final.Team2EntryId);
        Assert.NotNull(third.Team1EntryId);
        Assert.NotNull(third.Team2EntryId);
        Assert.NotEqual(final.Team1EntryId, final.Team2EntryId);
        Assert.NotEqual(third.Team1EntryId, third.Team2EntryId);
        Assert.Single(await verification.Matches.Where(x => x.SeriesId == final.PlayoffSeriesId).ToListAsync());
        Assert.Single(await verification.Matches.Where(x => x.SeriesId == third.PlayoffSeriesId).ToListAsync());
    }

    private async Task<(int CompetitionId, int SourcePhaseId)> CreateCompetitionAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var season = await Create<SeasonDto>("/api/admin/seasons", new CreateSeasonRequest(2086, $"Playoff {suffix}", null, null));
        var division = await Create<DivisionDto>("/api/admin/divisions", new CreateDivisionRequest($"Playoff {suffix}", 86, Gender.Female));
        var sf1 = new FormatPlayoffSeriesInputDto("SF1", "SF1", 1, 2, 1, 0, []);
        var sf2 = new FormatPlayoffSeriesInputDto("SF2", "SF2", 2, 2, 1, 0, []);
        var phases = new FormatPhaseInputDto[]
        {
            new("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom, [], []),
            new("SEMIS", "Semifinals", PhaseType.Playoff, PhaseRole.Semifinal, 2, null, FixtureMode.Playoff, [], [sf1, sf2]),
            new("THIRD", "Third Place", PhaseType.Playoff, PhaseRole.ThirdPlace, 3, null, FixtureMode.Playoff, [],
                [new("THIRD", "Third Place", 1, 1, 0, 0, [new(1, SeriesParticipantSourceType.SeriesLoser, "SF1"), new(2, SeriesParticipantSourceType.SeriesLoser, "SF2")])]),
            new("FINAL", "Final", PhaseType.Playoff, PhaseRole.Final, 4, null, FixtureMode.Playoff, [],
                [new("FINAL", "Final", 1, 1, 0, 0, [new(1, SeriesParticipantSourceType.SeriesWinner, "SF1"), new(2, SeriesParticipantSourceType.SeriesWinner, "SF2")])])
        };
        var rules = new FormatQualificationRuleInputDto[]
        {
            Rule(1, "SF1", 1, 1), Rule(4, "SF1", 2, 2), Rule(2, "SF2", 1, 3), Rule(3, "SF2", 2, 4)
        };
        var definition = new CompetitionFormatDefinitionDto(phases, rules,
            [new(3, 0, 2, 1), new(3, 1, 2, 1), new(3, 2, 2, 1)],
            [new(1, TiebreakCriterion.MatchWins, SortDirection.Desc), new(2, TiebreakCriterion.PointRatio, SortDirection.Desc)], []);
        var format = await Create<CompetitionFormatDto>("/api/admin/competition-formats", new CreateCompetitionFormatRequest($"PP_{suffix}", $"Playoff {suffix}", null, 4, 4, definition));await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}/active",new{active=true},Json);
        var competition = await Create<CompetitionDto>("/api/admin/competitions", new CreateCompetitionRequest($"Playoff {suffix}", season.SeasonId, division.DivisionId, CompetitionPeriodType.Annual, null, null, new(CompetitionStructureSourceType.Format, format.CompetitionFormatId, null)));
        var club=await Create<ClubDto>("/api/admin/clubs",new CreateClubRequest($"Playoff Club {suffix}",null));
        for (var i = 1; i <= 4; i++)
        {
            var team = await Create<TeamDto>("/api/admin/teams", new CreateTeamRequest($"Playoff {suffix} {i}", Gender.Female, club.ClubId));
            var entry=await Create<TeamEntryDto>($"/api/admin/competitions/{competition.CompetitionId}/entries", new AddTeamEntryRequest(team.TeamId, (short)i));
            (await factory.Client.PatchAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/entries/{entry.TeamEntryId}/status",new ChangeTeamEntryStatusRequest(TeamEntryStatus.Active),Json)).EnsureSuccessStatusCode();
        }
        (await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/fixture/generate", new GenerateFixtureRequest(321))).EnsureSuccessStatusCode();
        (await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/schedule",null)).EnsureSuccessStatusCode();

        int sourcePhaseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var phase = await db.Set<CompetitionPhase>().SingleAsync(x => x.CompetitionId == competition.CompetitionId && x.Code == "REGULAR");
            sourcePhaseId = phase.CompetitionPhaseId;
            phase.MarkInProgress();
            var matches = await db.Matches.Include(x => x.HomeTeamEntry).Include(x => x.AwayTeamEntry).Where(x => x.PhaseId == sourcePhaseId).ToListAsync();
            foreach (var match in matches)
            {
                var homeWins = match.HomeTeamEntryId < match.AwayTeamEntryId;
                match.Finish(homeWins ? (byte)3 : (byte)0, homeWins ? (byte)0 : (byte)3,
                    homeWins ? match.HomeTeamEntry! : match.AwayTeamEntry!,
                    homeWins ? [new(1, 25, 10), new(2, 25, 11), new(3, 25, 12)] : [new(1, 10, 25), new(2, 11, 25), new(3, 12, 25)]);
            }
            await db.SaveChangesAsync();
        }
        (await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/phases/{sourcePhaseId}/complete", null)).EnsureSuccessStatusCode();
        return (competition.CompetitionId, sourcePhaseId);
    }

    private async Task<(int Sf1Match, int Sf2Match)> FinishInitialSemifinalMatches(int competitionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var matches = await db.Matches.Include(x => x.Series).Include(x => x.HomeTeamEntry).Include(x => x.AwayTeamEntry)
            .Where(x => x.CompetitionId == competitionId && x.SeriesId != null && (x.Series!.Code == "SF1" || x.Series.Code == "SF2")).ToListAsync();
        var sf1 = matches.Single(x => x.Series!.Code == "SF1");
        var sf2 = matches.Single(x => x.Series!.Code == "SF2");
        sf1.Finish(0, 3, sf1.AwayTeamEntry!, [new(1, 10, 25), new(2, 11, 25), new(3, 12, 25)]);
        sf2.Finish(3, 0, sf2.HomeTeamEntry!, [new(1, 25, 10), new(2, 25, 11), new(3, 25, 12)]);
        await db.SaveChangesAsync();
        return (sf1.MatchId, sf2.MatchId);
    }

    private async Task FinishMatch(int matchId, bool winnerIsHome)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var match = await db.Matches.Include(x => x.HomeTeamEntry).Include(x => x.AwayTeamEntry).SingleAsync(x => x.MatchId == matchId);
        match.Finish(winnerIsHome ? (byte)3 : (byte)0, winnerIsHome ? (byte)0 : (byte)3,
            winnerIsHome ? match.HomeTeamEntry! : match.AwayTeamEntry!,
            winnerIsHome ? [new(1, 25, 10), new(2, 25, 11), new(3, 25, 12)] : [new(1, 10, 25), new(2, 11, 25), new(3, 12, 25)]);
        await db.SaveChangesAsync();
    }

    private async Task<PlayoffProgressionResult> Process(int matchId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlayoffProgressionService>().ProcessFinishedMatchAsync(matchId);
    }

    private static FormatQualificationRuleInputDto Rule(short position, string series, byte side, short sequence) =>
        new("REGULAR", null, QualificationSelectionMode.PositionRange, position, position, QualificationTargetType.Series, "SEMIS", null, series, side, sequence);

    private async Task<T> Create<T>(string url, object body)
    {
        var response = await factory.Client.PostAsJsonAsync(url, body, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
