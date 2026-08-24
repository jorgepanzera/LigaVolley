using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.TeamEntries;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class TeamEntryRepository(LigaVolleyDbContext db) : ITeamEntryRepository
{
    public async Task<IReadOnlyList<TeamEntry>> ListAsync(int competitionId, CancellationToken ct)
        => await db.TeamEntries.AsNoTracking().Include(x => x.Team).Where(x => x.CompetitionId == competitionId).OrderBy(x => x.Seed == null).ThenBy(x => x.Seed).ThenBy(x => x.Team.Name).ToListAsync(ct);

    public Task<TeamEntry?> GetAsync(int competitionId, int entryId, bool tracking, CancellationToken ct)
    {
        IQueryable<TeamEntry> query = db.TeamEntries.Include(x => x.Team);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.TeamEntryId == entryId, ct);
    }

    public Task<bool> TeamExistsAsync(int competitionId, int teamId, CancellationToken ct)
        => db.TeamEntries.AnyAsync(x => x.CompetitionId == competitionId && x.TeamId == teamId, ct);

    public Task<int> CountValidAsync(int competitionId, CancellationToken ct)
        => db.TeamEntries.CountAsync(x => x.CompetitionId == competitionId && (x.Status == TeamEntryStatus.Registered || x.Status == TeamEntryStatus.Active), ct);

    public void Add(TeamEntry entry) => db.TeamEntries.Add(entry);
    public void Remove(TeamEntry entry) => db.TeamEntries.Remove(entry);
}
