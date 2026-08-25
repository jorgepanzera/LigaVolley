using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Teams;
using LigaVolley.Domain.Venues;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.People;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence;

public sealed class LigaVolleyDbContext(DbContextOptions<LigaVolleyDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<CompetitionFormat> CompetitionFormats => Set<CompetitionFormat>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TeamEntry> TeamEntries => Set<TeamEntry>();
    public DbSet<FixtureGeneration> FixtureGenerations => Set<FixtureGeneration>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<PhaseGroupEntry> PhaseGroupEntries => Set<PhaseGroupEntry>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonAdditionalDocument> PersonAdditionalDocuments => Set<PersonAdditionalDocument>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<Referee> Referees => Set<Referee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LigaVolleyDbContext).Assembly);

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var message = exception.InnerException?.Message ?? exception.Message;
            var (code, detail) = message switch
            {
                var x when x.Contains("UX_PERSON_document", StringComparison.OrdinalIgnoreCase) => ("person_document_already_exists", "A person with this document already exists."),
                var x when x.Contains("UQ_PLAYER_person", StringComparison.OrdinalIgnoreCase) => ("player_profile_already_exists", "Player profile already exists."),
                var x when x.Contains("UQ_COACH_person", StringComparison.OrdinalIgnoreCase) => ("coach_profile_already_exists", "Coach profile already exists."),
                var x when x.Contains("UQ_REFEREE_person", StringComparison.OrdinalIgnoreCase) => ("referee_profile_already_exists", "Referee profile already exists."),
                _ => ("unique_constraint_conflict", "The operation conflicts with an existing resource.")
            };
            throw new ResourceConflictException(
                code,
                detail);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
