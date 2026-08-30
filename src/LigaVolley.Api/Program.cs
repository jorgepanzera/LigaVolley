using System.Text.Json.Serialization;
using LigaVolley.Api.Endpoints.Admin;
using LigaVolley.Api.Endpoints.Scorer;
using LigaVolley.Api.Endpoints.Public;
using LigaVolley.Api.ErrorHandling;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Seasons;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Competitions;
using LigaVolley.Application.Clubs;
using LigaVolley.Application.Teams;
using LigaVolley.Application.Venues;
using LigaVolley.Application.TeamEntries;
using LigaVolley.Application.Fixtures;
using LigaVolley.Application.Matches;
using LigaVolley.Application.Standings;
using LigaVolley.Domain.Standings;
using LigaVolley.Application.PhaseCompletion;
using LigaVolley.Application.PlayoffProgression;
using LigaVolley.Application.CompetitionProgression;
using LigaVolley.Application.People;
using LigaVolley.Application.CompetitionRosters;
using LigaVolley.Application.MatchOfficials;
using LigaVolley.Application.MatchSheets;
using LigaVolley.Application.PublicQueries;
using LigaVolley.Infrastructure;
using LigaVolley.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

var runLivosurSeed = args.Contains("--seed-livosur-2026", StringComparer.OrdinalIgnoreCase);
var runDemoMatchSeed = args.Contains("--seed-demo-match", StringComparer.OrdinalIgnoreCase);
if (builder.Environment.IsDevelopment() || runLivosurSeed || runDemoMatchSeed)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<DivisionService>();
builder.Services.AddScoped<CompetitionFormatService>();
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<CompetitionSchedulingService>();
builder.Services.AddScoped<ClubService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<VenueService>();
builder.Services.AddScoped<TeamEntryService>();
builder.Services.AddScoped<FixtureService>();
builder.Services.AddScoped<MatchAdminService>();
builder.Services.AddScoped<MatchOpeningPrerequisiteEvaluator>();
builder.Services.AddScoped<MatchOperationsService>();
builder.Services.AddScoped<StandingsService>();
builder.Services.AddSingleton<StandingsCalculator>();
builder.Services.AddScoped<PhaseCompletionService>();
builder.Services.AddScoped<PlayoffProgressionService>();
builder.Services.AddScoped<CompetitionProgressionService>();
builder.Services.AddScoped<PeopleService>();
builder.Services.AddScoped<CompetitionRosterService>();
builder.Services.AddScoped<MatchOfficialService>();
builder.Services.AddScoped<MatchSheetService>();
builder.Services.AddScoped<MatchEngineService>();
builder.Services.AddScoped<OfflineSyncService>();
builder.Services.AddScoped<PublicQueryService>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (runLivosurSeed && runDemoMatchSeed)
    throw new InvalidOperationException("Choose either --seed-livosur-2026 or --seed-demo-match.");

if (runDemoMatchSeed)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("The demo Match seed can only run in the Development environment.");

    await using var scope = app.Services.CreateAsyncScope();
    var result = await scope.ServiceProvider.GetRequiredService<DemoMatchSeeder>().SeedAsync();
    app.Logger.LogInformation(
        "Demo Match ready. CompetitionId={CompetitionId} ({CompetitionName}); MatchId={MatchId}; HOME={Home}; AWAY={Away}; Venue={Venue}",
        result.CompetitionId, result.CompetitionName, result.MatchId, result.HomeTeam, result.AwayTeam, result.Venue);
    app.Logger.LogInformation("Scorer: {ScorerPath}", result.ScorerPath);
    app.Logger.LogInformation("Public Competition: {PublicCompetitionPath}", result.PublicCompetitionPath);
    app.Logger.LogInformation("Public Match: {PublicMatchPath}", result.PublicMatchPath);
    return;
}

if (runLivosurSeed)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("The LIVOSUR 2026 seed can only run in the Development environment.");

    await using var scope = app.Services.CreateAsyncScope();
    var result = await scope.ServiceProvider.GetRequiredService<Livosur2026Seeder>().SeedAsync();
    app.Logger.LogInformation(
        "LIVOSUR 2026 seed completed: {Seasons} season, {Divisions} divisions, {Clubs} clubs, {Teams} teams, {Venues} venues, {Competitions} competitions and {TeamEntries} entries.",
        result.Seasons, result.Divisions, result.Clubs, result.Teams, result.Venues, result.Competitions, result.TeamEntries);
    return;
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.MapSeasonEndpoints();
app.MapDivisionEndpoints();
app.MapCompetitionFormatEndpoints();
app.MapCompetitionEndpoints();
app.MapCompetitionProgressionEndpoints();
app.MapClubEndpoints();
app.MapTeamEndpoints();
app.MapVenueEndpoints();
app.MapTeamEntryEndpoints();
app.MapFixtureEndpoints();
app.MapMatchEndpoints();
app.MapStandingsEndpoints();
app.MapPhaseCompletionEndpoints();
app.MapPeopleEndpoints();
app.MapCompetitionRosterEndpoints();
app.MapMatchOfficialEndpoints();
app.MapScorerMatchOfficialEndpoints();
app.MapScorerMatchSheetEndpoints();
app.MapScorerMatchEngineEndpoints();
app.MapScorerOfflineSyncEndpoints();
app.MapPublicQueryEndpoints();

app.Run();

public partial class Program;
