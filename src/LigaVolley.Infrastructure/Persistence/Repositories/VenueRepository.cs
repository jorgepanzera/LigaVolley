using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Venues;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class VenueRepository(LigaVolleyDbContext db) : IVenueRepository
{
    public async Task<(IReadOnlyList<Venue> Items,int Total)> ListAsync(string? search,bool? active,int page,int pageSize,CancellationToken ct)
    {
        var query = db.Venues.AsNoTracking();
        if (active.HasValue) query = query.Where(x => x.Active == active);
        if(!string.IsNullOrWhiteSpace(search))query=query.Where(x=>x.Name.Contains(search));var total=await query.CountAsync(ct);return(await query.OrderBy(x=>x.Name).ThenBy(x=>x.VenueId).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct),total);
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
