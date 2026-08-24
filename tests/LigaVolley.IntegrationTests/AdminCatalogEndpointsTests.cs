using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Seasons;
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
