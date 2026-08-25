using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.PhaseCompletion;
using LigaVolley.Application.Seasons;
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

public sealed class PhaseCompletionEndpointsTests:IClassFixture<LigaVolleyApiFactory>
{
    private readonly LigaVolleyApiFactory factory; private static readonly JsonSerializerOptions Json=Options();
    public PhaseCompletionEndpointsTests(LigaVolleyApiFactory factory)=>this.factory=factory;

    [Fact]
    public async Task PreviewIsReadOnlyAndCompleteIsTransactionalIncrementalAndIdempotent()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var season=await Create<SeasonDto>("/api/admin/seasons",new CreateSeasonRequest(2087,$"Completion {suffix}",null,null));
        var division=await Create<DivisionDto>("/api/admin/divisions",new CreateDivisionRequest($"Completion {suffix}",87,Gender.Female));
        var phases=new FormatPhaseInputDto[]{
            new("REGULAR","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom,[],[]),
            new("GROUPS","Groups",PhaseType.GroupStage,PhaseRole.Championship,2,null,null,[new("CHAMP","Championship",GroupRole.Championship,1,1,FixtureMode.BalancedRandom,CarryOverMode.None)],[]),
            new("PLAYOFF","Playoff",PhaseType.Playoff,PhaseRole.Semifinal,3,null,FixtureMode.Playoff,[],[new("SF1","Semifinal",1,2,1,0,[])])};
        var rules=new FormatQualificationRuleInputDto[]{
            new("REGULAR",null,QualificationSelectionMode.PositionRange,1,4,QualificationTargetType.Group,"GROUPS","CHAMP",null,null,1),
            new("REGULAR",null,QualificationSelectionMode.PositionRange,1,1,QualificationTargetType.Series,"PLAYOFF",null,"SF1",1,2),
            new("REGULAR",null,QualificationSelectionMode.PositionRange,2,2,QualificationTargetType.Series,"PLAYOFF",null,"SF1",2,3)};
        var definition=new CompetitionFormatDefinitionDto(phases,rules,[new(3,0,2,1),new(3,1,2,1),new(3,2,2,1)],[new(1,TiebreakCriterion.MatchWins,SortDirection.Desc),new(2,TiebreakCriterion.PointRatio,SortDirection.Desc)],[]);
        var format=await Create<CompetitionFormatDto>("/api/admin/competition-formats",new CreateCompetitionFormatRequest($"PC_{suffix}",$"Completion {suffix}",null,4,4,definition));
        var competition=await Create<CompetitionDto>("/api/admin/competitions",new CreateCompetitionRequest($"Completion {suffix}",season.SeasonId,division.DivisionId,CompetitionPeriodType.Annual,null,null,new(CompetitionStructureSourceType.Format,format.CompetitionFormatId,null)));
        for(var i=1;i<=4;i++){var team=await Create<TeamDto>("/api/admin/teams",new CreateTeamRequest($"Completion {suffix} {i}",Gender.Female,null));await Create<TeamEntryDto>($"/api/admin/competitions/{competition.CompetitionId}/entries",new AddTeamEntryRequest(team.TeamId,(short)i));}
        var generate=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/fixture/generate",new GenerateFixtureRequest(777));generate.EnsureSuccessStatusCode();
        int sourcePhaseId;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();var comp=await db.Competitions.Include(x=>x.Phases).SingleAsync(x=>x.CompetitionId==competition.CompetitionId);var source=comp.Phases.Single(x=>x.Code=="REGULAR");sourcePhaseId=source.CompetitionPhaseId;source.MarkInProgress();
            var matches=await db.Matches.Include(x=>x.HomeTeamEntry).Include(x=>x.AwayTeamEntry).Where(x=>x.PhaseId==sourcePhaseId).ToListAsync();
            foreach(var match in matches){var home=match.HomeTeamEntry!;var away=match.AwayTeamEntry!;var homeWins=home.TeamEntryId<away.TeamEntryId;var winner=homeWins?home:away;match.Finish(homeWins?(byte)3:(byte)0,homeWins?(byte)0:(byte)3,winner,homeWins?[new(1,25,10),new(2,25,11),new(3,25,12)]:[new(1,10,25),new(2,11,25),new(3,12,25)]);}await db.SaveChangesAsync();
        }
        var preview=await factory.Client.GetFromJsonAsync<PhaseCompletionPreviewDto>($"/api/admin/competitions/{competition.CompetitionId}/phases/{sourcePhaseId}/completion-preview",Json);
        Assert.NotNull(preview);Assert.True(preview.CanComplete);Assert.Equal(6,preview.Qualifications.Count);Assert.Contains(preview.GeneratedFixtures,x=>x.PhaseGroupId.HasValue&&x.MatchesCreated==6);Assert.Contains(preview.GeneratedFixtures,x=>x.SeriesId.HasValue&&x.MatchesCreated==1);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();Assert.Empty(await db.PhaseGroupEntries.Where(x=>x.CompetitionId==competition.CompetitionId).ToListAsync());Assert.Equal(6,await db.Matches.CountAsync(x=>x.CompetitionId==competition.CompetitionId));}
        var completeResponse=await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/phases/{sourcePhaseId}/complete",null);completeResponse.EnsureSuccessStatusCode();var complete=(await completeResponse.Content.ReadFromJsonAsync<PhaseCompletionResultDto>(Json))!;Assert.False(complete.AlreadyCompleted);Assert.Equal(CompetitionPhaseStatus.Finished,complete.Status);
        int groupEntries,matchCount;
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();groupEntries=await db.PhaseGroupEntries.CountAsync(x=>x.CompetitionId==competition.CompetitionId);matchCount=await db.Matches.CountAsync(x=>x.CompetitionId==competition.CompetitionId);Assert.Equal(4,groupEntries);Assert.Equal(13,matchCount);Assert.All(await db.PhaseGroupEntries.Where(x=>x.CompetitionId==competition.CompetitionId).ToListAsync(),x=>{Assert.NotNull(x.SourcePosition);Assert.Null(x.Seed);});var series=await db.Set<CompetitionPlayoffSeries>().SingleAsync(x=>x.Code=="SF1"&&x.CompetitionId==competition.CompetitionId);Assert.Equal(PlayoffSeriesStatus.Ready,series.Status);Assert.NotNull(series.Team1EntryId);Assert.NotNull(series.Team2EntryId);Assert.Single(await db.Matches.Where(x=>x.SeriesId==series.PlayoffSeriesId).ToListAsync());}
        var repeated=(await (await factory.Client.PostAsync($"/api/admin/competitions/{competition.CompetitionId}/phases/{sourcePhaseId}/complete",null)).Content.ReadFromJsonAsync<PhaseCompletionResultDto>(Json))!;Assert.True(repeated.AlreadyCompleted);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();Assert.Equal(groupEntries,await db.PhaseGroupEntries.CountAsync(x=>x.CompetitionId==competition.CompetitionId));Assert.Equal(matchCount,await db.Matches.CountAsync(x=>x.CompetitionId==competition.CompetitionId));}
    }

    [Fact]
    public async Task MissingCompetitionIsNotFound()
    {
        var response=await factory.Client.GetAsync("/api/admin/competitions/2147483647/phases/1/completion-preview");Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentsBothPhaseCompletionOperations()
    {
        using var document = await factory.Client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json");
        var paths = document!.RootElement.GetProperty("paths");
        const string prefix = "/api/admin/competitions/{competitionId}/phases/{phaseId}";

        Assert.True(paths.GetProperty($"{prefix}/completion-preview").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty($"{prefix}/complete").TryGetProperty("post", out _));
    }
    private async Task<T>Create<T>(string url,object body){var response=await factory.Client.PostAsJsonAsync(url,body,Json);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>(Json))!;}
    private static JsonSerializerOptions Options(){var x=new JsonSerializerOptions(JsonSerializerDefaults.Web);x.Converters.Add(new JsonStringEnumConverter());return x;}
}
