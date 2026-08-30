using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LigaVolley.Application.Common;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Competitions;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed class CompetitionFormatEditorEndpointsTests(LigaVolleyApiFactory factory):IClassFixture<LigaVolleyApiFactory>
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};

    [Fact]
    public async Task EmptyCompatibleDatabase_CreatesValidInactiveAggregate_AndValidateDoesNotPersist()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();var before=await Counts(db);
        var invalid=ValidDefinition() with{ScoringRules=[]};var validation=await factory.Client.PostAsJsonAsync("/api/admin/competition-formats/validate",new ValidateCompetitionFormatRequest(4,6,invalid,$"EMPTY_{suffix}","Empty"),Json);validation.EnsureSuccessStatusCode();Assert.False((await validation.Content.ReadFromJsonAsync<CompetitionFormatValidationDto>(Json))!.IsValid);Assert.Equal(before,await Counts(db));
        var response=await factory.Client.PostAsJsonAsync("/api/admin/competition-formats",new CreateCompetitionFormatRequest($"FIRST_{suffix}","First",null,4,6,ValidDefinition()),Json);Assert.Equal(HttpStatusCode.Created,response.StatusCode);var created=(await response.Content.ReadFromJsonAsync<CompetitionFormatDto>(Json))!;Assert.False(created.Active);Assert.False(created.Used);Assert.False(created.IsStructurallyLocked);Assert.Equal(3,created.Definition.ScoringRules.Count);
    }

    [Fact]
    public async Task CloneCreatesIndependentInactiveChildren_AndActivationRevalidates()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];var source=await Create($"CLONE_{suffix}",ValidDefinition());
        var cloneResponse=await factory.Client.PostAsJsonAsync($"/api/admin/competition-formats/{source.CompetitionFormatId}/clone",new CloneCompetitionFormatRequest($"CLONED_{suffix}","Clone",null),Json);Assert.Equal(HttpStatusCode.Created,cloneResponse.StatusCode);var clone=(await cloneResponse.Content.ReadFromJsonAsync<CompetitionFormatDto>(Json))!;Assert.False(clone.Active);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();var sourceChildren=await db.Set<FormatPhase>().Where(x=>x.CompetitionFormatId==source.CompetitionFormatId).Select(x=>x.FormatPhaseId).ToArrayAsync();var cloneChildren=await db.Set<FormatPhase>().Where(x=>x.CompetitionFormatId==clone.CompetitionFormatId).Select(x=>x.FormatPhaseId).ToArrayAsync();Assert.NotEmpty(sourceChildren);Assert.NotEmpty(cloneChildren);Assert.Empty(sourceChildren.Intersect(cloneChildren));
        var activate=await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{clone.CompetitionFormatId}/active",new SetActiveRequest(true),Json);activate.EnsureSuccessStatusCode();Assert.True((await activate.Content.ReadFromJsonAsync<CompetitionFormatDto>(Json))!.Active);
    }

    [Fact]
    public async Task DraftUsageWarnsWithoutSync_ThenOperationalUsageLocksStructureButNotMetadata()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];var format=await Create($"LOCK_{suffix}",ValidDefinition());await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}/active",new SetActiveRequest(true),Json);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();var persisted=await db.CompetitionFormats.Include(x=>x.Phases).SingleAsync(x=>x.CompetitionFormatId==format.CompetitionFormatId);var competition=new Competition($"Draft {suffix}",new Season((short)Random.Shared.Next(3000,30000),$"S {suffix}",null,null),new Division($"D {suffix}",(short)Random.Shared.Next(100,30000),Gender.Female),persisted,CompetitionPeriodType.Annual,null,null);db.Competitions.Add(competition);await db.SaveChangesAsync();var originalPhaseName=competition.Phases[0].Name;
        var changed=format.Definition with{Phases=[format.Definition.Phases[0] with{Name="Changed format phase"}]};var draftUpdate=await factory.Client.PutAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}",new UpdateCompetitionFormatRequest(format.Code,"Draft editable",null,4,6,changed),Json);draftUpdate.EnsureSuccessStatusCode();var draftDto=(await draftUpdate.Content.ReadFromJsonAsync<CompetitionFormatDto>(Json))!;Assert.Equal(1,draftDto.UsedByDraftCompetitionCount);Assert.False(draftDto.IsStructurallyLocked);db.ChangeTracker.Clear();Assert.Equal(originalPhaseName,(await db.Competitions.Include(x=>x.Phases).SingleAsync(x=>x.CompetitionId==competition.CompetitionId)).Phases[0].Name);
        var tracked=await db.Competitions.SingleAsync(x=>x.CompetitionId==competition.CompetitionId);tracked.MarkScheduledAfterInitialFixture();await db.SaveChangesAsync();
        var conflict=await factory.Client.PutAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}",new UpdateCompetitionFormatRequest(format.Code+"_X","Blocked",null,4,6,changed),Json);Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);Assert.Equal("competition_format_structurally_locked",(await conflict.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("code").GetString());
        var metadata=await factory.Client.PutAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}",new UpdateCompetitionFormatRequest(format.Code,"Metadata allowed","description",4,6,changed),Json);metadata.EnsureSuccessStatusCode();Assert.True((await metadata.Content.ReadFromJsonAsync<CompetitionFormatDto>(Json))!.IsStructurallyLocked);
        var deactivate=await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}/active",new SetActiveRequest(false),Json);deactivate.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InactiveFormatIsRejectedForBothCompetitionCreationModes()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];var format=await Create($"INACTIVE_{suffix}",ValidDefinition());await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();var persisted=await db.CompetitionFormats.SingleAsync(x=>x.CompetitionFormatId==format.CompetitionFormatId);var season=new Season((short)Random.Shared.Next(3000,30000),$"S {suffix}",null,null);var division=new Division($"D {suffix}",(short)Random.Shared.Next(100,30000),Gender.Female);var model=new Competition($"Model {suffix}",season,division,persisted,CompetitionPeriodType.Annual,null,null);db.Competitions.Add(model);await db.SaveChangesAsync();
        foreach(var source in new[]{new CompetitionStructureSourceDto(CompetitionStructureSourceType.Format,format.CompetitionFormatId,null),new CompetitionStructureSourceDto(CompetitionStructureSourceType.Competition,null,model.CompetitionId)}){var response=await factory.Client.PostAsJsonAsync("/api/admin/competitions",new CreateCompetitionRequest($"Rejected {Guid.NewGuid():N}",season.SeasonId,division.DivisionId,CompetitionPeriodType.Annual,null,null,source),Json);Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);Assert.Equal("competition_format_inactive",(await response.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("code").GetString());}
    }

    private async Task<CompetitionFormatDto> Create(string code,CompetitionFormatDefinitionDto definition){var response=await factory.Client.PostAsJsonAsync("/api/admin/competition-formats",new CreateCompetitionFormatRequest(code,code,null,4,6,definition),Json);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<CompetitionFormatDto>(Json))!;}
    private static CompetitionFormatDefinitionDto ValidDefinition()=>new([new("REGULAR","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom,[],[])],[],[new(3,0,3,0),new(3,1,3,0),new(3,2,2,1)],[new(1,TiebreakCriterion.TablePoints,SortDirection.Desc)],[]);
    private static async Task<(int Formats,int Phases,int Groups,int Series)> Counts(LigaVolleyDbContext db)=>new(await db.CompetitionFormats.CountAsync(),await db.Set<FormatPhase>().CountAsync(),await db.Set<FormatGroup>().CountAsync(),await db.Set<FormatPlayoffSeries>().CountAsync());
}
