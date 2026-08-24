using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Seasons;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class AdminCatalogEndpointsTests : IClassFixture<LigaVolleyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly LigaVolleyApiFactory factory;

    public AdminCatalogEndpointsTests(LigaVolleyApiFactory factory)
    {
        this.factory = factory;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public async Task CompetitionFormatEndpoints_ProvideAggregateLifecycle()
    {
        var definition = new CompetitionFormatDefinitionDto(
            [new("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 2, FixtureMode.MirroredHomeAway, [], [])], [], [], [], []);
        var validation = await factory.Client.PostAsJsonAsync("/api/admin/competition-formats/validate", new ValidateCompetitionFormatRequest(8, 8, definition));
        validation.EnsureSuccessStatusCode();
        Assert.True((await validation.Content.ReadFromJsonAsync<CompetitionFormatValidationDto>(JsonOptions))!.IsValid);

        var create = await factory.Client.PostAsJsonAsync("/api/admin/competition-formats", new CreateCompetitionFormatRequest("INT_RR8", "Integration RR8", null, 8, 8, definition));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!;
        Assert.Single(created.Definition.Phases);
        Assert.Equal("INT_RR8", (await factory.Client.GetFromJsonAsync<CompetitionFormatDto>($"/api/admin/competition-formats/{created.CompetitionFormatId}", JsonOptions))!.Code);
        var list = await factory.Client.GetFromJsonAsync<CompetitionFormatSummaryDto[]>("/api/admin/competition-formats?active=true&teamCount=8", JsonOptions);
        Assert.Contains(list!, x => x.CompetitionFormatId == created.CompetitionFormatId);

        var update = await factory.Client.PutAsJsonAsync($"/api/admin/competition-formats/{created.CompetitionFormatId}", new UpdateCompetitionFormatRequest("INT_RR8", "Updated RR8", "updated", 8, 10, definition));
        update.EnsureSuccessStatusCode();
        var cloneResponse = await factory.Client.PostAsJsonAsync($"/api/admin/competition-formats/{created.CompetitionFormatId}/clone", new CloneCompetitionFormatRequest("INT_RR8_CLONE", "Clone", null));
        Assert.Equal(HttpStatusCode.Created, cloneResponse.StatusCode);
        var clone = (await cloneResponse.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!;
        Assert.NotEqual(created.CompetitionFormatId, clone.CompetitionFormatId);

        var deactivate = await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{created.CompetitionFormatId}/active", new SetActiveRequest(false));
        deactivate.EnsureSuccessStatusCode();
        Assert.False((await deactivate.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!.Active);
    }

    [Fact]
    public async Task SeasonEndpoints_ProvideCrudWithoutDelete()
    {
        var createdResponse = await factory.Client.PostAsJsonAsync(
            "/api/admin/seasons",
            new CreateSeasonRequest(2031, "Season 2031", new DateOnly(2031, 1, 1), new DateOnly(2031, 12, 31)));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<SeasonDto>(JsonOptions);
        Assert.NotNull(created);

        var updateResponse = await factory.Client.PutAsJsonAsync(
            $"/api/admin/seasons/{created.SeasonId}",
            new UpdateSeasonRequest(2031, "Updated 2031", null, null));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var patchResponse = await factory.Client.PatchAsJsonAsync(
            $"/api/admin/seasons/{created.SeasonId}/active",
            new SetActiveRequest(false));
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var list = await factory.Client.GetFromJsonAsync<SeasonSummaryDto[]>("/api/admin/seasons?active=false&year=2031", JsonOptions);
        Assert.Contains(list!, item => item.SeasonId == created.SeasonId && !item.Active);
    }

    [Fact]
    public async Task DivisionEndpoints_ProvideCrudAndFilters()
    {
        var createdResponse = await factory.Client.PostAsJsonAsync(
            "/api/admin/divisions",
            new CreateDivisionRequest("Integration Female", 21, Gender.Female),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<DivisionDto>(JsonOptions);
        Assert.NotNull(created);

        var fetched = await factory.Client.GetFromJsonAsync<DivisionDto>($"/api/admin/divisions/{created.DivisionId}", JsonOptions);
        Assert.Equal(Gender.Female, fetched!.Gender);

        var list = await factory.Client.GetFromJsonAsync<DivisionSummaryDto[]>("/api/admin/divisions?gender=Female&active=true", JsonOptions);
        Assert.Contains(list!, item => item.DivisionId == created.DivisionId);
    }

    [Fact]
    public async Task DuplicateSeason_ReturnsConflictProblemDetails()
    {
        await factory.Client.PostAsJsonAsync("/api/admin/seasons", new CreateSeasonRequest(2032, "First", null, null));
        var response = await factory.Client.PostAsJsonAsync("/api/admin/seasons", new CreateSeasonRequest(2032, "Second", null, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("season_year_conflict", document!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidSeason_ReturnsBadRequestProblemDetails()
    {
        var response = await factory.Client.PostAsJsonAsync(
            "/api/admin/seasons",
            new CreateSeasonRequest(2033, "Invalid", new DateOnly(2033, 2, 1), new DateOnly(2033, 1, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MissingDivision_ReturnsNotFoundProblemDetails()
    {
        var response = await factory.Client.GetAsync("/api/admin/divisions/2147483647");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SqlUniqueViolation_IsTranslatedToApplicationConflict()
    {
        using (var firstScope = factory.Services.CreateScope())
        {
            var firstContext = firstScope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            firstContext.Seasons.Add(new Season(2034, "Concurrent A", null, null));
            await ((IUnitOfWork)firstContext).SaveChangesAsync();
        }

        using var secondScope = factory.Services.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        secondContext.Seasons.Add(new Season(2034, "Concurrent B", null, null));
        var unitOfWork = (IUnitOfWork)secondContext;
        var exception = await Assert.ThrowsAsync<ResourceConflictException>(() => unitOfWork.SaveChangesAsync());
        Assert.Equal("unique_constraint_conflict", exception.Code);
    }
}
