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
using LigaVolley.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<DivisionService>();
builder.Services.AddScoped<CompetitionFormatService>();
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<ClubService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<VenueService>();
builder.Services.AddScoped<TeamEntryService>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapSeasonEndpoints();
app.MapDivisionEndpoints();
app.MapCompetitionFormatEndpoints();
app.MapCompetitionEndpoints();
app.MapClubEndpoints();
app.MapTeamEndpoints();
app.MapVenueEndpoints();
app.MapTeamEntryEndpoints();

app.Run();

public partial class Program;
