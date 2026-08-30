using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.PublicQueries;
using LigaVolley.Application.Seasons;
using LigaVolley.Application.TeamEntries;
using LigaVolley.Application.Teams;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.IntegrationTests;

public sealed class PublicQueryEndpointsTests(LigaVolleyApiFactory factory):IClassFixture<LigaVolleyApiFactory>
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};

    [Fact]
    public async Task Draft_is_transitively_hidden_and_scheduled_competition_is_navigable()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];var year=(short)Random.Shared.Next(25000,30000);var season=await Create<SeasonDto>("/api/admin/seasons",new CreateSeasonRequest(year,$"Public {suffix}",null,null));var division=await Create<DivisionDto>("/api/admin/divisions",new CreateDivisionRequest($"Public {suffix}",(short)Random.Shared.Next(1000,30000),Gender.Female));
        var definition=new CompetitionFormatDefinitionDto([new("REG","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom,[],[])],[],[new(3,0,3,0),new(3,1,3,0),new(3,2,2,1)],[new(1,TiebreakCriterion.TablePoints,SortDirection.Desc)],[]);var format=await Create<CompetitionFormatDto>("/api/admin/competition-formats",new CreateCompetitionFormatRequest($"PUB_{suffix}","Public",null,2,2,definition));await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}/active",new{active=true},Json);var competition=await Create<CompetitionDto>("/api/admin/competitions",new CreateCompetitionRequest($"Public {suffix}",season.SeasonId,division.DivisionId,CompetitionPeriodType.Annual,null,null,new(CompetitionStructureSourceType.Format,format.CompetitionFormatId,null)));
        Assert.Equal(HttpStatusCode.NotFound,(await factory.Client.GetAsync($"/api/public/competitions/{competition.CompetitionId}")).StatusCode);Assert.DoesNotContain((await factory.Client.GetFromJsonAsync<PublicSeasonDto[]>("/api/public/seasons",Json))!,x=>x.SeasonId==season.SeasonId);
        var club=await Create<ClubDto>("/api/admin/clubs",new CreateClubRequest($"Public Club {suffix}",null));for(var i=1;i<=2;i++){var team=await Create<TeamDto>("/api/admin/teams",new CreateTeamRequest($"Public {suffix} {i}",Gender.Female,club.ClubId));var entry=await Create<TeamEntryDto>($"/api/admin/competitions/{competition.CompetitionId}/entries",new AddTeamEntryRequest(team.TeamId,(short)i));(await factory.Client.PatchAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/entries/{entry.TeamEntryId}/status",new ChangeTeamEntryStatusRequest(TeamEntryStatus.Active),Json)).EnsureSuccessStatusCode();}var generated=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/fixture/generate",new GenerateFixtureRequest(42),Json);generated.EnsureSuccessStatusCode();(await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/schedule",null)).EnsureSuccessStatusCode();
        var seasons=(await factory.Client.GetFromJsonAsync<PublicSeasonDto[]>("/api/public/seasons",Json))!;Assert.Contains(seasons,x=>x.SeasonId==season.SeasonId);Assert.Equal(seasons.OrderByDescending(x=>x.Year).Select(x=>x.SeasonId),seasons.Select(x=>x.SeasonId));var detail=await factory.Client.GetFromJsonAsync<PublicCompetitionDto>($"/api/public/competitions/{competition.CompetitionId}",Json);Assert.Equal(2,detail!.Teams.Count);Assert.Single(detail.Phases);var fixture=await factory.Client.GetFromJsonAsync<PublicCompetitionFixtureDto>($"/api/public/competitions/{competition.CompetitionId}/fixture",Json);Assert.Single(Assert.Single(fixture!.Phases).Rounds);
        var invalid=await factory.Client.GetAsync($"/api/public/competitions/{competition.CompetitionId}/standings?phaseGroupId=1");Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);using var problem=JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());Assert.Equal("public_invalid_standings_scope",problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Swagger_documents_complete_public_catalog()
    {
        using var doc=JsonDocument.Parse(await factory.Client.GetStringAsync("/swagger/v1/swagger.json"));var paths=doc.RootElement.GetProperty("paths");foreach(var path in new[]{"/api/public/seasons","/api/public/competitions","/api/public/competitions/{competitionId}","/api/public/competitions/{competitionId}/fixture","/api/public/competitions/{competitionId}/standings","/api/public/matches/{matchId}","/api/public/matches/{matchId}/live"})Assert.True(paths.TryGetProperty(path,out _),path);
    }
    private async Task<T> Create<T>(string url,object body){var response=await factory.Client.PostAsJsonAsync(url,body,Json);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>(Json))!;}
}
