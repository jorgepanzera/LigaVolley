using System.Text.Json.Serialization;
using LigaVolley.Api.Endpoints.Admin;
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
using LigaVolley.Infrastructure;
using LigaVolley.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

var runLivosurSeed = args.Contains("--seed-livosur-2026", StringComparer.OrdinalIgnoreCase);
if (runLivosurSeed)
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
builder.Services.AddScoped<ClubService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<VenueService>();
builder.Services.AddScoped<TeamEntryService>();
builder.Services.AddScoped<FixtureService>();
builder.Services.AddScoped<MatchAdminService>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

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
app.MapClubEndpoints();
app.MapTeamEndpoints();
app.MapVenueEndpoints();
app.MapTeamEntryEndpoints();
app.MapFixtureEndpoints();
app.MapMatchEndpoints();

app.Run();

public partial class Program;
