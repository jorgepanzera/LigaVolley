using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Clubs;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class ClubRepository(LigaVolleyDbContext db) : IClubRepository
{
    public async Task<(IReadOnlyList<Club> Items,int Total)> ListAsync(string? search,bool? active,int page,int pageSize,CancellationToken ct)
    {
        var query = db.Clubs.AsNoTracking();
        if (active.HasValue) query = query.Where(x => x.Active == active);
        if(!string.IsNullOrWhiteSpace(search))query=query.Where(x=>x.Name.Contains(search));var total=await query.CountAsync(ct);return(await query.OrderBy(x=>x.Name).ThenBy(x=>x.ClubId).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct),total);
    }

    public Task<Club?> GetAsync(int id, bool tracking, CancellationToken ct)
    {
        IQueryable<Club> query = db.Clubs;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.ClubId == id, ct);
    }

    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken ct)
        => db.Clubs.AnyAsync(x => x.Name == name && (!excludingId.HasValue || x.ClubId != excludingId), ct);

    public void Add(Club club) => db.Clubs.Add(club);
}
