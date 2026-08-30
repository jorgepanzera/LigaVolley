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
using LigaVolley.Application.Competitions;
using LigaVolley.Domain.Competitions;
using LigaVolley.Application.Clubs;
using LigaVolley.Application.Teams;
using LigaVolley.Application.Venues;
using LigaVolley.Application.TeamEntries;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.Matches;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Infrastructure.Persistence;
using LigaVolley.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

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
    public async Task Livosur2026Seeder_LoadsApprovedCountsAndIsIdempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var roundRobin = new CompetitionFormat("ROUND_ROBIN", "Round robin", null, 6, 8);
        roundRobin.Phases.Add(new FormatPhase("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom));
        var splitStage = new CompetitionFormat("SPLIT_STAGE", "Split stage", null, 9, 24);
        var regular = new FormatPhase("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom);
        var second = new FormatPhase("SECOND_STAGE", "Second stage", PhaseType.GroupStage, PhaseRole.Championship, 2, null, null);
        var championship = new FormatGroup("CHAMPIONSHIP", "Championship", GroupRole.Championship, 1, 1, FixtureMode.BalancedRandom, CarryOverMode.None);
        var relegation = new FormatGroup("RELEGATION", "Relegation", GroupRole.Relegation, 2, 1, FixtureMode.BalancedRandom, CarryOverMode.None);
        second.Groups.Add(championship); second.Groups.Add(relegation); splitStage.Phases.Add(regular); splitStage.Phases.Add(second);
        splitStage.QualificationRules.Add(new FormatQualificationRule(regular, null, QualificationSelectionMode.TopHalf, null, null, QualificationTargetType.Group, second, championship, null, null, 1));
        splitStage.QualificationRules.Add(new FormatQualificationRule(regular, null, QualificationSelectionMode.BottomHalf, null, null, QualificationTargetType.Group, second, relegation, null, null, 2));
        db.CompetitionFormats.AddRange(roundRobin, splitStage);
        await db.SaveChangesAsync();

        var seeder = new Livosur2026Seeder(db);
        var first = await seeder.SeedAsync();
        var countsAfterFirst = await SeedCounts(db);
        var secondRun = await seeder.SeedAsync();
        var countsAfterSecond = await SeedCounts(db);

        Assert.Equal(new Livosur2026SeedResult(1, 24, 98, 211, 55, 24, 211), first);
        Assert.Equal(first, secondRun);
        Assert.Equal(countsAfterFirst, countsAfterSecond);
        Assert.All(await db.Competitions.Include(x=>x.CompetitionFormat).Where(x => x.Season.Year == 2026 && db.TeamEntries.Count(e => e.CompetitionId == x.CompetitionId) <= 8).ToListAsync(), x => Assert.Equal("ROUND_ROBIN", x.CompetitionFormat.Code));
        Assert.All(await db.Competitions.Include(x=>x.CompetitionFormat).Where(x => x.Season.Year == 2026 && db.TeamEntries.Count(e => e.CompetitionId == x.CompetitionId) >= 9).ToListAsync(), x => Assert.Equal("SPLIT_STAGE", x.CompetitionFormat.Code));
        Assert.Single(await db.TeamEntries.Where(x => x.Team.Name == "C.A JUAN E. MILLER").ToListAsync());
        Assert.Equal("CLAUSURA 2026 - MASCULINO C", (await db.TeamEntries.Include(x => x.Team).Include(x => x.Competition).SingleAsync(x => x.Team.Name == "C.A JUAN E. MILLER")).Competition.Name);
    }

    private static async Task<(int Seasons, int Divisions, int Clubs, int Teams, int Venues, int Competitions, int Entries)> SeedCounts(LigaVolleyDbContext db) =>
        (await db.Seasons.CountAsync(), await db.Divisions.CountAsync(), await db.Clubs.CountAsync(), await db.Teams.CountAsync(),
         await db.Venues.CountAsync(), await db.Competitions.CountAsync(), await db.TeamEntries.CountAsync());

    [Fact]
    public async Task FixtureEndpoints_GenerateAndReturnOnlyInitialPersistedMatches()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var seasonResponse=await factory.Client.PostAsJsonAsync("/api/admin/seasons",new CreateSeasonRequest(2043,$"Fixture {suffix}",null,null));seasonResponse.EnsureSuccessStatusCode();var season=(await seasonResponse.Content.ReadFromJsonAsync<SeasonDto>(JsonOptions))!;
        var divisionResponse=await factory.Client.PostAsJsonAsync("/api/admin/divisions",new CreateDivisionRequest($"Fixture {suffix}",43,Gender.Female),JsonOptions);divisionResponse.EnsureSuccessStatusCode();var division=(await divisionResponse.Content.ReadFromJsonAsync<DivisionDto>(JsonOptions))!;
        var definition=new CompetitionFormatDefinitionDto([new("REGULAR","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom,[],[])],[],[],[],[]);
        var formatResponse=await factory.Client.PostAsJsonAsync("/api/admin/competition-formats",new CreateCompetitionFormatRequest($"FIX_{suffix}",$"Fixture {suffix}",null,5,5,definition));formatResponse.EnsureSuccessStatusCode();var format=(await formatResponse.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!;(await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}/active",new SetActiveRequest(true),JsonOptions)).EnsureSuccessStatusCode();
        var competitionResponse=await factory.Client.PostAsJsonAsync("/api/admin/competitions",new CreateCompetitionRequest($"Fixture {suffix}",season.SeasonId,division.DivisionId,CompetitionPeriodType.Annual,null,null,new(CompetitionStructureSourceType.Format,format.CompetitionFormatId,null)),JsonOptions);competitionResponse.EnsureSuccessStatusCode();var competition=(await competitionResponse.Content.ReadFromJsonAsync<CompetitionDto>(JsonOptions))!;
        var fixtureClubResponse=await factory.Client.PostAsJsonAsync("/api/admin/clubs",new CreateClubRequest($"Fixture Club {suffix}",null),JsonOptions);fixtureClubResponse.EnsureSuccessStatusCode();var fixtureClub=(await fixtureClubResponse.Content.ReadFromJsonAsync<ClubDto>(JsonOptions))!;for(var i=1;i<=5;i++){var teamResponse=await factory.Client.PostAsJsonAsync("/api/admin/teams",new CreateTeamRequest($"Fixture {suffix} {i}",Gender.Female,fixtureClub.ClubId),JsonOptions);teamResponse.EnsureSuccessStatusCode();var team=(await teamResponse.Content.ReadFromJsonAsync<TeamDto>(JsonOptions))!;var entryResponse=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/entries",new AddTeamEntryRequest(team.TeamId,null));entryResponse.EnsureSuccessStatusCode();var entry=(await entryResponse.Content.ReadFromJsonAsync<TeamEntryDto>(JsonOptions))!;(await factory.Client.PatchAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/entries/{entry.TeamEntryId}/status",new ChangeTeamEntryStatusRequest(TeamEntryStatus.Active),JsonOptions)).EnsureSuccessStatusCode();}
        var generate=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/fixture/generate",new GenerateFixtureRequest(12345));Assert.Equal(HttpStatusCode.OK,generate.StatusCode);var response=(await generate.Content.ReadFromJsonAsync<GenerateFixtureResponse>(JsonOptions))!;Assert.Equal(10,response.MatchesCreated);
        var fixture=(await factory.Client.GetFromJsonAsync<CompetitionFixtureDto>($"/api/admin/competitions/{competition.CompetitionId}/fixture",JsonOptions))!;var phase=Assert.Single(fixture.Phases);Assert.True(phase.Generated);Assert.Equal(10,phase.Matches.Count);Assert.All(phase.Matches,x=>{Assert.Null(x.MatchDate);Assert.Null(x.VenueId);});
        var matchId=phase.Matches[0].MatchId;
        var match=await factory.Client.GetFromJsonAsync<MatchAdminDto>($"/api/admin/matches/{matchId}",JsonOptions);Assert.NotNull(match);Assert.Equal(matchId,match.MatchId);Assert.Equal(MatchStatus.Pending,match.Status);
        var venueResponse=await factory.Client.PostAsJsonAsync("/api/admin/venues",new CreateVenueRequest($"Fixture venue {suffix}","Address"));venueResponse.EnsureSuccessStatusCode();var venue=(await venueResponse.Content.ReadFromJsonAsync<VenueDto>(JsonOptions))!;
        var date=new DateTimeOffset(2043,9,12,19,30,0,TimeSpan.FromHours(-3));var schedule=await factory.Client.PutAsJsonAsync($"/api/admin/matches/{matchId}/schedule",new ScheduleMatchRequest(date,venue.VenueId),JsonOptions);Assert.Equal(HttpStatusCode.OK,schedule.StatusCode);var scheduled=(await schedule.Content.ReadFromJsonAsync<MatchAdminDto>(JsonOptions))!;Assert.Equal(MatchStatus.Scheduled,scheduled.Status);Assert.Equal(date.UtcDateTime,scheduled.MatchDate!.Value.UtcDateTime);Assert.Equal(venue.VenueId,scheduled.Venue!.VenueId);
        var clear=await factory.Client.PutAsJsonAsync($"/api/admin/matches/{matchId}/schedule",new ScheduleMatchRequest(null,null),JsonOptions);clear.EnsureSuccessStatusCode();var pending=(await clear.Content.ReadFromJsonAsync<MatchAdminDto>(JsonOptions))!;Assert.Equal(MatchStatus.Pending,pending.Status);Assert.Null(pending.MatchDate);Assert.Null(pending.Venue);
        var duplicate=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/fixture/generate",new GenerateFixtureRequest(12345));Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
    }

    [Fact]
    public async Task TeamEntryEndpoints_EnforceUniquenessAndSupportDraftLifecycle()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var seasonResponse=await factory.Client.PostAsJsonAsync("/api/admin/seasons",new CreateSeasonRequest(2042,$"Entry {suffix}",null,null)); seasonResponse.EnsureSuccessStatusCode(); var season=(await seasonResponse.Content.ReadFromJsonAsync<SeasonDto>(JsonOptions))!;
        var divisionResponse=await factory.Client.PostAsJsonAsync("/api/admin/divisions",new CreateDivisionRequest($"Entry {suffix}",42,Gender.Female),JsonOptions); divisionResponse.EnsureSuccessStatusCode(); var division=(await divisionResponse.Content.ReadFromJsonAsync<DivisionDto>(JsonOptions))!;
        var definition=new CompetitionFormatDefinitionDto([new("REGULAR","Regular",PhaseType.RoundRobin,PhaseRole.Regular,1,1,FixtureMode.BalancedRandom,[],[])],[],[],[],[]);
        var formatResponse=await factory.Client.PostAsJsonAsync("/api/admin/competition-formats",new CreateCompetitionFormatRequest($"ENTRY_{suffix}",$"Entry {suffix}",null,2,2,definition)); formatResponse.EnsureSuccessStatusCode(); var format=(await formatResponse.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!;(await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}/active",new SetActiveRequest(true),JsonOptions)).EnsureSuccessStatusCode();
        var competitionResponse=await factory.Client.PostAsJsonAsync("/api/admin/competitions",new CreateCompetitionRequest($"Entry {suffix}",season.SeasonId,division.DivisionId,CompetitionPeriodType.Annual,null,null,new(CompetitionStructureSourceType.Format,format.CompetitionFormatId,null)),JsonOptions); competitionResponse.EnsureSuccessStatusCode(); var competition=(await competitionResponse.Content.ReadFromJsonAsync<CompetitionDto>(JsonOptions))!;
        var entryClubResponse=await factory.Client.PostAsJsonAsync("/api/admin/clubs",new CreateClubRequest($"Entry Club {suffix}",null),JsonOptions);entryClubResponse.EnsureSuccessStatusCode();var entryClub=(await entryClubResponse.Content.ReadFromJsonAsync<ClubDto>(JsonOptions))!;var teamResponse=await factory.Client.PostAsJsonAsync("/api/admin/teams",new CreateTeamRequest($"Entry {suffix}",Gender.Female,entryClub.ClubId),JsonOptions); teamResponse.EnsureSuccessStatusCode(); var team=(await teamResponse.Content.ReadFromJsonAsync<TeamDto>(JsonOptions))!;
        var add=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/entries",new AddTeamEntryRequest(team.TeamId,1)); Assert.Equal(HttpStatusCode.Created,add.StatusCode); var entry=(await add.Content.ReadFromJsonAsync<TeamEntryDto>(JsonOptions))!;
        var duplicate=await factory.Client.PostAsJsonAsync($"/api/admin/competitions/{competition.CompetitionId}/entries",new AddTeamEntryRequest(team.TeamId,null)); Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        var list=await factory.Client.GetFromJsonAsync<TeamEntryDto[]>($"/api/admin/competitions/{competition.CompetitionId}/entries",JsonOptions); Assert.Single(list!);
        var delete=await factory.Client.DeleteAsync($"/api/admin/competitions/{competition.CompetitionId}/entries/{entry.TeamEntryId}"); Assert.Equal(HttpStatusCode.NoContent,delete.StatusCode);
    }

    [Fact]
    public async Task ClubTeamVenueEndpoints_ProvideCatalogLifecyclesAndRelations()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var clubResponse=await factory.Client.PostAsJsonAsync("/api/admin/clubs",new CreateClubRequest($"Club {suffix}",suffix));
        Assert.Equal(HttpStatusCode.Created,clubResponse.StatusCode); var club=(await clubResponse.Content.ReadFromJsonAsync<ClubDto>(JsonOptions))!;
        var teamResponse=await factory.Client.PostAsJsonAsync("/api/admin/teams",new CreateTeamRequest($"Team {suffix}",Gender.Female,club.ClubId),JsonOptions);
        Assert.Equal(HttpStatusCode.Created,teamResponse.StatusCode); var team=(await teamResponse.Content.ReadFromJsonAsync<TeamDto>(JsonOptions))!; Assert.Equal(club.ClubId,team.Club!.ClubId);
        var teams=await factory.Client.GetFromJsonAsync<LigaVolley.Application.Common.PagedResult<TeamSummaryDto>>($"/api/admin/teams?clubId={club.ClubId}&gender=Female&active=true",JsonOptions); Assert.Contains(teams!.Items,x=>x.TeamId==team.TeamId);
        var venueResponse=await factory.Client.PostAsJsonAsync("/api/admin/venues",new CreateVenueRequest($"Venue {suffix}","Address"));
        Assert.Equal(HttpStatusCode.Created,venueResponse.StatusCode); var venue=(await venueResponse.Content.ReadFromJsonAsync<VenueDto>(JsonOptions))!;
        var deactivate=await factory.Client.PatchAsJsonAsync($"/api/admin/venues/{venue.VenueId}/active",new SetActiveRequest(false)); deactivate.EnsureSuccessStatusCode(); Assert.False((await deactivate.Content.ReadFromJsonAsync<VenueDto>(JsonOptions))!.Active);
    }

    [Fact]
    public async Task ClubLogo_UploadPublicReadReplaceAndDeleteAreSupported()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];var created=await factory.Client.PostAsJsonAsync("/api/admin/clubs",new CreateClubRequest($"Logo {suffix}",null),JsonOptions);created.EnsureSuccessStatusCode();var club=(await created.Content.ReadFromJsonAsync<ClubDto>(JsonOptions))!;
        static MultipartFormDataContent Logo(){var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");var body=new MultipartFormDataContent();var file=new ByteArrayContent(bytes);file.Headers.ContentType=new("image/png");body.Add(file,"file","logo.png");return body;}
        using(var first=Logo()){var upload=await factory.Client.PutAsync($"/api/admin/clubs/{club.ClubId}/logo",first);upload.EnsureSuccessStatusCode();var dto=(await upload.Content.ReadFromJsonAsync<ClubLogoDto>(JsonOptions))!;Assert.Contains($"/api/public/clubs/{club.ClubId}/logo?v=",dto.LogoUrl);}
        var asset=await factory.Client.GetAsync($"/api/public/clubs/{club.ClubId}/logo?v=1");asset.EnsureSuccessStatusCode();Assert.Equal("image/png",asset.Content.Headers.ContentType?.MediaType);
        using(var second=Logo()){(await factory.Client.PutAsync($"/api/admin/clubs/{club.ClubId}/logo",second)).EnsureSuccessStatusCode();}
        Assert.Equal(HttpStatusCode.NoContent,(await factory.Client.DeleteAsync($"/api/admin/clubs/{club.ClubId}/logo")).StatusCode);Assert.Equal(HttpStatusCode.NoContent,(await factory.Client.DeleteAsync($"/api/admin/clubs/{club.ClubId}/logo")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await factory.Client.GetAsync($"/api/public/clubs/{club.ClubId}/logo?v=2")).StatusCode);
    }

    [Fact]
    public async Task CompetitionEndpoints_CreateFromFormatAndFromCompetitionWithIndependentStructures()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var seasonResponse = await factory.Client.PostAsJsonAsync("/api/admin/seasons", new CreateSeasonRequest(2041, $"Season {suffix}", null, null));
        seasonResponse.EnsureSuccessStatusCode();
        var season = (await seasonResponse.Content.ReadFromJsonAsync<SeasonDto>(JsonOptions))!;
        var divisionResponse = await factory.Client.PostAsJsonAsync("/api/admin/divisions", new CreateDivisionRequest($"Competition {suffix}", 41, Gender.Female), JsonOptions);
        divisionResponse.EnsureSuccessStatusCode();
        var division = (await divisionResponse.Content.ReadFromJsonAsync<DivisionDto>(JsonOptions))!;
        var definition = new CompetitionFormatDefinitionDto([new("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 2, FixtureMode.MirroredHomeAway, [], [])], [], [], [], []);
        var formatResponse = await factory.Client.PostAsJsonAsync("/api/admin/competition-formats", new CreateCompetitionFormatRequest($"CF_{suffix}", $"Format {suffix}", null, 4, 8, definition));
        formatResponse.EnsureSuccessStatusCode();
        var format = (await formatResponse.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!;
        (await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{format.CompetitionFormatId}/active",new SetActiveRequest(true),JsonOptions)).EnsureSuccessStatusCode();

        var fromFormatResponse = await factory.Client.PostAsJsonAsync("/api/admin/competitions", new CreateCompetitionRequest($"Opening {suffix}", season.SeasonId, division.DivisionId, CompetitionPeriodType.Opening, null, null, new(CompetitionStructureSourceType.Format, format.CompetitionFormatId, null)), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, fromFormatResponse.StatusCode);
        var fromFormat = (await fromFormatResponse.Content.ReadFromJsonAsync<CompetitionDto>(JsonOptions))!;
        var firstStructure = (await factory.Client.GetFromJsonAsync<CompetitionStructureDto>($"/api/admin/competitions/{fromFormat.CompetitionId}/structure", JsonOptions))!;

        var fromCompetitionResponse = await factory.Client.PostAsJsonAsync("/api/admin/competitions", new CreateCompetitionRequest($"Closing {suffix}", season.SeasonId, division.DivisionId, CompetitionPeriodType.Closing, null, null, new(CompetitionStructureSourceType.Competition, null, fromFormat.CompetitionId)), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, fromCompetitionResponse.StatusCode);
        var fromCompetition = (await fromCompetitionResponse.Content.ReadFromJsonAsync<CompetitionDto>(JsonOptions))!;
        var secondStructure = (await factory.Client.GetFromJsonAsync<CompetitionStructureDto>($"/api/admin/competitions/{fromCompetition.CompetitionId}/structure", JsonOptions))!;

        Assert.Equal(format.CompetitionFormatId, fromFormat.Format.CompetitionFormatId);
        Assert.Equal(fromFormat.Format.CompetitionFormatId, fromCompetition.Format.CompetitionFormatId);
        Assert.Single(firstStructure.Phases); Assert.Single(secondStructure.Phases);
        Assert.NotEqual(firstStructure.Phases[0].PhaseId, secondStructure.Phases[0].PhaseId);
    }

    [Fact]
    public async Task CompetitionFormatEndpoints_ProvideAggregateLifecycle()
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var definition = new CompetitionFormatDefinitionDto(
            [new("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 2, FixtureMode.MirroredHomeAway, [], [])], [], [new(3,0,3,0),new(3,1,3,0),new(3,2,2,1)], [new(1,TiebreakCriterion.TablePoints,SortDirection.Desc)], []);
        var validation = await factory.Client.PostAsJsonAsync("/api/admin/competition-formats/validate", new ValidateCompetitionFormatRequest(8, 8, definition));
        validation.EnsureSuccessStatusCode();
        Assert.True((await validation.Content.ReadFromJsonAsync<CompetitionFormatValidationDto>(JsonOptions))!.IsValid);

        var create = await factory.Client.PostAsJsonAsync("/api/admin/competition-formats", new CreateCompetitionFormatRequest($"INT_RR8_{suffix}", "Integration RR8", null, 8, 8, definition));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!;
        Assert.Single(created.Definition.Phases);
        Assert.False(created.Active);
        Assert.Equal($"INT_RR8_{suffix}", (await factory.Client.GetFromJsonAsync<CompetitionFormatDto>($"/api/admin/competition-formats/{created.CompetitionFormatId}", JsonOptions))!.Code);
        var list = await factory.Client.GetFromJsonAsync<CompetitionFormatSummaryDto[]>("/api/admin/competition-formats?active=true&teamCount=8", JsonOptions);
        Assert.DoesNotContain(list!, x => x.CompetitionFormatId == created.CompetitionFormatId);
        var activate=await factory.Client.PatchAsJsonAsync($"/api/admin/competition-formats/{created.CompetitionFormatId}/active",new SetActiveRequest(true));activate.EnsureSuccessStatusCode();

        var update = await factory.Client.PutAsJsonAsync($"/api/admin/competition-formats/{created.CompetitionFormatId}", new UpdateCompetitionFormatRequest($"INT_RR8_{suffix}", "Updated RR8", "updated", 8, 10, definition));
        update.EnsureSuccessStatusCode();
        var cloneResponse = await factory.Client.PostAsJsonAsync($"/api/admin/competition-formats/{created.CompetitionFormatId}/clone", new CloneCompetitionFormatRequest($"INT_RR8_CLONE_{suffix}", "Clone", null));
        Assert.Equal(HttpStatusCode.Created, cloneResponse.StatusCode);
        var clone = (await cloneResponse.Content.ReadFromJsonAsync<CompetitionFormatDto>(JsonOptions))!;
        Assert.NotEqual(created.CompetitionFormatId, clone.CompetitionFormatId);
        Assert.False(clone.Active);

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
