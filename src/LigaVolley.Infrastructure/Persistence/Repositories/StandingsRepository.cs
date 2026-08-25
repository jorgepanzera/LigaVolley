using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class StandingsRepository(LigaVolleyDbContext db) : IStandingsRepository
{
    public Task<bool> PhaseGroupExistsAsync(int phaseGroupId, CancellationToken ct)
        => db.Set<LigaVolley.Domain.Competitions.CompetitionPhaseGroup>().AnyAsync(x => x.PhaseGroupId == phaseGroupId, ct);

    public async Task<IReadOnlyList<TeamEntry>> ListPhaseParticipantsAsync(int competitionId, CancellationToken ct)
        => await db.TeamEntries.AsNoTracking().Include(x => x.Team)
            .Where(x => x.CompetitionId == competitionId && (x.Status == TeamEntryStatus.Registered || x.Status == TeamEntryStatus.Active))
            .OrderBy(x => x.TeamEntryId).ToListAsync(ct);

    public async Task<IReadOnlyList<TeamEntry>> ListGroupParticipantsAsync(int competitionId, int phaseGroupId, CancellationToken ct)
        => await db.PhaseGroupEntries.AsNoTracking().Where(x => x.CompetitionId == competitionId && x.PhaseGroupId == phaseGroupId)
            .OrderBy(x => x.TeamEntryId).Select(x => x.TeamEntry).Include(x => x.Team).ToListAsync(ct);

    public async Task<IReadOnlyList<Match>> ListScopeMatchesAsync(int competitionId, int phaseId, int? phaseGroupId, CancellationToken ct)
        => await db.Matches.AsNoTracking().Include(x => x.Sets)
            .Where(x => x.CompetitionId == competitionId && x.PhaseId == phaseId && x.PhaseGroupId == phaseGroupId)
            .OrderBy(x => x.MatchNumber).ToListAsync(ct);
}
