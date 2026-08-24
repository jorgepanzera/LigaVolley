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
            throw new ResourceConflictException(
                "unique_constraint_conflict",
                "The operation conflicts with an existing resource.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
