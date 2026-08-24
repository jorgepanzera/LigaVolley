using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence;

public sealed class LigaVolleyDbContext(DbContextOptions<LigaVolleyDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Division> Divisions => Set<Division>();

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
