using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Infrastructure.Persistence;
using LigaVolley.Infrastructure.Persistence.Repositories;
using LigaVolley.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LigaVolley")
            ?? throw new InvalidOperationException("Connection string 'LigaVolley' is not configured.");

        services.AddDbContext<LigaVolleyDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IDivisionRepository, DivisionRepository>();
        services.AddScoped<ICompetitionFormatRepository, CompetitionFormatRepository>();
        services.AddScoped<ICompetitionRepository, CompetitionRepository>();
        services.AddScoped<IClubRepository, ClubRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<ITeamEntryRepository, TeamEntryRepository>();
        services.AddScoped<IFixtureRepository, FixtureRepository>();
        services.AddScoped<IStandingsRepository, StandingsRepository>();
        services.AddScoped<IPhaseCompletionRepository, PhaseCompletionRepository>();
        services.AddScoped<IPlayoffProgressionRepository, PlayoffProgressionRepository>();
        services.AddScoped<ICompetitionProgressionRepository, CompetitionProgressionRepository>();
        services.AddScoped<IPeopleRepository, PeopleRepository>();
        services.AddScoped<ICompetitionRosterRepository, CompetitionRosterRepository>();
        services.AddScoped<IMatchOfficialRepository, MatchOfficialRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<LigaVolleyDbContext>());
        services.AddScoped<Livosur2026Seeder>();
        return services;
    }
}
