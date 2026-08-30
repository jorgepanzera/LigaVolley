using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.CompetitionProgression;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.Seasons;
using LigaVolley.Application.TeamEntries;
using LigaVolley.Application.Teams;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class CompetitionProgressionEndpointsTests : IClassFixture<LigaVolleyApiFactory>
{
    private readonly LigaVolleyApiFactory factory;
    private static readonly JsonSerializerOptions Json = Options();
    public CompetitionProgressionEndpointsTests(LigaVolleyApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task ProgressionPreviewAndCompleteRemainConsistentAndIdempotent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var season = await Create<SeasonDto>("/api/admin/seasons", new CreateSeasonRequest(2085, $"Completion {suffix}", null, null));
        var top = await Create<DivisionDto>("/api/admin/divisions", new CreateDivisionRequest($"Top {suffix}", 84, Gender.Female));
        var sourceDivision = await Create<DivisionDto>("/api/admin/divisions", new CreateDivisionRequest($"Source {suffix}", 85, Gender.Female));
        var phase = new FormatPhaseInputDto("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom, [], []);
        var movement = new FormatMovementRuleInputDto(MovementType.Promotion, MovementSourceType.PhasePosition,
            "REGULAR", null, null, 1, 1, -1, false);
        var definition = new CompetitionFormatDefinitionDto([phase], [],
            [new(3, 0, 2, 1), new(3, 1, 2, 1), new(3, 2, 2, 1)],
            [new(1, TiebreakCriterion.MatchWins, SortDirection.Desc)], [movement]);
        var format = await Create<CompetitionFormatDto>("/api/admin/competition-formats", new CreateCompetitionFormatRequest($"CC_{suffix}", $"Completion {suffix}", null, 2, 2, definition));
        var competition = await Create<CompetitionDto>("/api/admin/competitions", new CreateCompetitionRequest($"Completion {suffix}", season.SeasonId,
            sourceDivision.DivisionId, CompetitionPeriodType.Annual, null, null, new(CompetitionStructureSourceType.Format, format.CompetitionFormatId, null)));

        var draftPreview = await factory.Client.GetFromJsonAsync<CompetitionCompletionPreviewDto>($"/api/admin/competitions/{competition.CompetitionId}/completion-preview", Json);
        Assert.False(draftPreview!.CanComplete);
        Assert.Contains(draftPreview.Blockers, x => x.Code == "competition_not_in_progress");
        var blockedComplete = await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, blockedComplete.StatusCode);
        using (var blockedProblem = await blockedComplete.Content.ReadFromJsonAsync<JsonDocument>())
        {
            Assert.Equal("competition_cannot_complete", blockedProblem!.RootElement.GetProperty("code").GetString());
            Assert.True(blockedProblem.RootElement.GetProperty("blockers").GetArrayLength() > 0);
        }

        var club=await Create<ClubDto>("/api/admin/clubs",new CreateClubRequest($"Completion Club {suffix}",null));
        for (var i = 1; i <= 2; i++)
        {
            var team = await Create<TeamDto>("/api/admin/teams", new CreateTeamRequest($"Completion {suffix} {i}", Gender.Female, club.ClubId));
            var entry=await Create<TeamEntryDto>($"/api/admin/competitions/{competition.CompetitionId}/entries", new AddTeamEntryRequest(team.TeamId, (short)i));
            (await factory.Client.PatchAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/entries/{entry.TeamEntryId}/status",new ChangeTeamEntryStatusRequest(TeamEntryStatus.Active),Json)).EnsureSuccessStatusCode();
        }
        (await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/fixture/generate", new GenerateFixtureRequest(12))).EnsureSuccessStatusCode();
        (await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/schedule",null)).EnsureSuccessStatusCode();

        int winnerEntryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var entity = await db.Competitions.Include(x => x.Phases).SingleAsync(x => x.CompetitionId == competition.CompetitionId);
            entity.MarkInProgressAfterMatchStart();
            var sourcePhase = entity.Phases.Single();
            sourcePhase.MarkInProgress();
            var match = await db.Matches.Include(x => x.HomeTeamEntry).Include(x => x.AwayTeamEntry).SingleAsync(x => x.CompetitionId == competition.CompetitionId);
            winnerEntryId = match.HomeTeamEntryId!.Value;
            match.Finish(3, 0, match.HomeTeamEntry!, [new(1, 25, 10), new(2, 25, 11), new(3, 25, 12)]);
            sourcePhase.Complete();
            await db.SaveChangesAsync();
        }

        var progression = await factory.Client.GetFromJsonAsync<CompetitionProgressionDto>($"/api/admin/competitions/{competition.CompetitionId}/progression", Json);
        Assert.NotNull(progression);
        Assert.Equal(1, progression.Matches.Total);
        Assert.Equal(1, progression.Matches.Finished);
        Assert.Single(progression.Phases);
        Assert.Equal(CompetitionPhaseStatus.Finished, progression.Phases[0].Status);

        var preview = await factory.Client.GetFromJsonAsync<CompetitionCompletionPreviewDto>($"/api/admin/competitions/{competition.CompetitionId}/completion-preview", Json);
        Assert.True(preview!.CanComplete);
        var projectedMovement = Assert.Single(preview.Movements);
        Assert.Equal(winnerEntryId, projectedMovement.TeamEntryId);
        Assert.Equal(MovementResultStatus.Applied, projectedMovement.Status);
        Assert.Equal(top.DivisionId, projectedMovement.TargetDivisionId);

        int entriesBefore;
        int matchesBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            entriesBefore = await db.TeamEntries.CountAsync(x => x.CompetitionId == competition.CompetitionId);
            matchesBefore = await db.Matches.CountAsync(x => x.CompetitionId == competition.CompetitionId);
        }

        var responses = await Task.WhenAll(
            factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/complete", null),
            factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/complete", null));
        Assert.All(responses, x => x.EnsureSuccessStatusCode());
        var results = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<CompetitionCompletionResultDto>(Json)));
        Assert.Equal(1, results.Count(x => x!.AlreadyCompleted));
        Assert.Equal(1, results.Count(x => !x!.AlreadyCompleted));
        Assert.All(results, x => Assert.Equal(CompetitionStatus.Finished, x!.Status));
        Assert.All(results, x => Assert.NotNull(x!.CompletedAt));
        Assert.Equal(results[0]!.CompletedAt, results[1]!.CompletedAt);
        Assert.Equal(preview.Movements, results[0]!.Movements);

        var repeatedResponse = await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/complete", null);
        repeatedResponse.EnsureSuccessStatusCode();
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<CompetitionCompletionResultDto>(Json);
        Assert.True(repeated!.AlreadyCompleted);
        Assert.Equal(results[0]!.CompletedAt, repeated.CompletedAt);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            Assert.Equal(entriesBefore, await db.TeamEntries.CountAsync(x => x.CompetitionId == competition.CompetitionId));
            Assert.Equal(matchesBefore, await db.Matches.CountAsync(x => x.CompetitionId == competition.CompetitionId));
        }

        var patch = await factory.Client.PatchAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/status",
            new ChangeCompetitionStatusRequest(CompetitionStatus.Finished), Json);
        Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentsProgressionAndCompletionEndpoints()
    {
        using var document = await factory.Client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json");
        var paths = document!.RootElement.GetProperty("paths");
        const string prefix = "/api/admin/competitions/{competitionId}";
        Assert.True(paths.GetProperty($"{prefix}/progression").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty($"{prefix}/completion-preview").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty($"{prefix}/complete").TryGetProperty("post", out _));
    }

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
