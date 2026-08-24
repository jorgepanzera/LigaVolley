using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Teams;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class TeamRepository(LigaVolleyDbContext db) : ITeamRepository
{
    public async Task<IReadOnlyList<Team>> ListAsync(int? clubId, Gender? gender, bool? active, CancellationToken ct)
    {
        IQueryable<Team> query = db.Teams.AsNoTracking().Include(x => x.Club);
        if (clubId.HasValue) query = query.Where(x => x.ClubId == clubId);
        if (gender.HasValue) query = query.Where(x => x.Gender == gender);
        if (active.HasValue) query = query.Where(x => x.Active == active);
        return await query.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public Task<Team?> GetAsync(int id, bool tracking, CancellationToken ct)
    {
        IQueryable<Team> query = db.Teams.Include(x => x.Club);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.TeamId == id, ct);
    }

    public Task<bool> NameGenderExistsAsync(string name, Gender gender, int? excludingId, CancellationToken ct)
        => db.Teams.AnyAsync(x => x.Name == name && x.Gender == gender && (!excludingId.HasValue || x.TeamId != excludingId), ct);

    public void Add(Team team) => db.Teams.Add(team);
}
