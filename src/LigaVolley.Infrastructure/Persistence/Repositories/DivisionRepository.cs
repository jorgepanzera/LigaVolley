using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Divisions;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class DivisionRepository(LigaVolleyDbContext dbContext) : IDivisionRepository
{
    public async Task<IReadOnlyList<Division>> ListAsync(Gender? gender, bool? active, CancellationToken cancellationToken)
    {
        var query = dbContext.Divisions.AsNoTracking();
        if (gender.HasValue)
        {
            query = query.Where(x => x.Gender == gender);
        }

        if (active.HasValue)
        {
            query = query.Where(x => x.Active == active);
        }

        return await query.OrderBy(x => x.Gender).ThenBy(x => x.LevelOrder).ToListAsync(cancellationToken);
    }

    public Task<Division?> GetAsync(int id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? dbContext.Divisions : dbContext.Divisions.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.DivisionId == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Gender gender, int? excludingId, CancellationToken cancellationToken)
        => dbContext.Divisions.AnyAsync(
            x => x.Name == name && x.Gender == gender && (!excludingId.HasValue || x.DivisionId != excludingId),
            cancellationToken);

    public Task<bool> LevelExistsAsync(short levelOrder, Gender gender, int? excludingId, CancellationToken cancellationToken)
        => dbContext.Divisions.AnyAsync(
            x => x.LevelOrder == levelOrder && x.Gender == gender && (!excludingId.HasValue || x.DivisionId != excludingId),
            cancellationToken);

    public void Add(Division division) => dbContext.Divisions.Add(division);
}
