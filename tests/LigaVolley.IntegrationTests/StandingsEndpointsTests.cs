using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.Seasons;
using LigaVolley.Application.Standings;
using LigaVolley.Application.TeamEntries;
using LigaVolley.Application.Teams;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class StandingsEndpointsTests : IClassFixture<LigaVolleyApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = Options();
    private readonly LigaVolleyApiFactory factory;
    public StandingsEndpointsTests(LigaVolleyApiFactory factory) => this.factory=factory;

    [Fact]
    public async Task StandingsEndpointCalculatesConfiguredResultAndDocumentsErrors()
    {
        var openApi=await factory.Client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json");
        Assert.True(openApi!.RootElement.GetProperty("paths").TryGetProperty("/api/admin/competitions/{competitionId}/phases/{phaseId}/standings",out var documentedPath));
        Assert.True(documentedPath.TryGetProperty("get",out _));
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var season=await Create<SeasonDto>("/api/admin/seasons",new CreateSeasonRequest(2088,$"Standings {suffix}",null,null));
        var division=await Create<DivisionDto>("/api/admin/divisions",new CreateDivisionRequest($"Standings {suffix}",88,Gender.Female));
        var definition=new CompetitionFormatDefinitionDto(
            [new("REGULAR","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom,[],[])],[],
            [new(3,0,2,1),new(3,1,2,1),new(3,2,2,1)],
            [new(1,TiebreakCriterion.TablePoints,SortDirection.Desc),new(2,TiebreakCriterion.MatchWins,SortDirection.Desc)],[]);
        var format=await Create<CompetitionFormatDto>("/api/admin/competition-formats",new CreateCompetitionFormatRequest($"ST_{suffix}",$"Standings {suffix}",null,2,2,definition));
        var competition=await Create<CompetitionDto>("/api/admin/competitions",new CreateCompetitionRequest($"Standings {suffix}",season.SeasonId,division.DivisionId,CompetitionPeriodType.Annual,null,null,new(CompetitionStructureSourceType.Format,format.CompetitionFormatId,null)));
        for(var i=1;i<=2;i++)
        {
            var team=await Create<TeamDto>("/api/admin/teams",new CreateTeamRequest($"Standings {suffix} {i}",Gender.Female,null));
            await Create<TeamEntryDto>($"/api/admin/competitions/{competition.CompetitionId}/entries",new AddTeamEntryRequest(team.TeamId,(short)i));
        }
        var generateResponse=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/fixture/generate",new GenerateFixtureRequest(123)); generateResponse.EnsureSuccessStatusCode();
        var fixture=(await factory.Client.GetFromJsonAsync<CompetitionFixtureDto>($"/api/admin/competitions/{competition.CompetitionId}/fixture",JsonOptions))!;
        var phase=Assert.Single(fixture.Phases); var matchId=Assert.Single(phase.Matches).MatchId;

        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var match=await db.Matches.Include(x=>x.HomeTeamEntry).Include(x=>x.AwayTeamEntry).SingleAsync(x=>x.MatchId==matchId);
            match.Finish(3,2,match.HomeTeamEntry!,[new(1,25,20),new(2,20,25),new(3,25,21),new(4,22,25),new(5,15,10)]);
            await db.SaveChangesAsync();
        }

        var response=await factory.Client.GetAsync($"/api/admin/competitions/{competition.CompetitionId}/phases/{phase.PhaseId}/standings");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode); var standings=(await response.Content.ReadFromJsonAsync<StandingsDto>(JsonOptions))!;
        Assert.True(standings.IsFinal); Assert.Equal(2,standings.Positions[0].TablePoints); Assert.Equal(1,standings.Positions[1].TablePoints);
        Assert.Equal((3,2,107,101),(standings.Positions[0].SetsWon,standings.Positions[0].SetsLost,standings.Positions[0].PointsWon,standings.Positions[0].PointsLost));

        var invalid=await factory.Client.GetAsync($"/api/admin/competitions/{competition.CompetitionId}/phases/{phase.PhaseId}/standings?phaseGroupId=999999");
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode); var problem=await invalid.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("standings_group_not_allowed",problem!.RootElement.GetProperty("code").GetString());
    }

    private async Task<T> Create<T>(string url,object body)
    {
        var response=await factory.Client.PostAsJsonAsync(url,body,JsonOptions); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
    private static JsonSerializerOptions Options(){var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);options.Converters.Add(new JsonStringEnumConverter());return options;}
}
