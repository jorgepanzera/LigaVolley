using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Venues;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class VenueRepository(LigaVolleyDbContext db) : IVenueRepository
{
    public async Task<IReadOnlyList<Venue>> ListAsync(bool? active, CancellationToken ct)
    {
        var query = db.Venues.AsNoTracking();
        if (active.HasValue) query = query.Where(x => x.Active == active);
        return await query.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public Task<Venue?> GetAsync(int id, bool tracking, CancellationToken ct)
    {
        IQueryable<Venue> query = db.Venues;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.VenueId == id, ct);
    }

    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken ct)
        => db.Venues.AnyAsync(x => x.Name == name && (!excludingId.HasValue || x.VenueId != excludingId), ct);

    public void Add(Venue venue) => db.Venues.Add(venue);
}
