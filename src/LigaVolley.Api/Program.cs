using System.Text.Json.Serialization;
using LigaVolley.Api.Endpoints.Admin;
using LigaVolley.Api.ErrorHandling;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Seasons;
using LigaVolley.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<DivisionService>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapSeasonEndpoints();
app.MapDivisionEndpoints();

app.Run();

public partial class Program;
