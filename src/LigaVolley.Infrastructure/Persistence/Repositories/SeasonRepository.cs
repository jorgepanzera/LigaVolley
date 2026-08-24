using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Seasons;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class SeasonRepository(LigaVolleyDbContext dbContext) : ISeasonRepository
{
    public async Task<IReadOnlyList<Season>> ListAsync(bool? active, short? year, CancellationToken cancellationToken)
    {
        var query = dbContext.Seasons.AsNoTracking();
        if (active.HasValue)
        {
            query = query.Where(x => x.Active == active);
        }

        if (year.HasValue)
        {
            query = query.Where(x => x.Year == year);
        }

        return await query.OrderByDescending(x => x.Year).ToListAsync(cancellationToken);
    }

    public Task<Season?> GetAsync(int id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? dbContext.Seasons : dbContext.Seasons.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.SeasonId == id, cancellationToken);
    }

    public Task<bool> YearExistsAsync(short year, int? excludingId, CancellationToken cancellationToken)
        => dbContext.Seasons.AnyAsync(x => x.Year == year && (!excludingId.HasValue || x.SeasonId != excludingId), cancellationToken);

    public void Add(Season season) => dbContext.Seasons.Add(season);
}
