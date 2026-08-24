using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class FixtureRepository(LigaVolleyDbContext db) : IFixtureRepository
{
    public Task<Match?> GetMatchAsync(int matchId,bool tracking,CancellationToken ct)
    {
        var query=db.Matches.Include(x=>x.Competition).Include(x=>x.Phase).Include(x=>x.PhaseGroup)
            .Include(x=>x.HomeTeamEntry).ThenInclude(x=>x!.Team)
            .Include(x=>x.AwayTeamEntry).ThenInclude(x=>x!.Team)
            .Include(x=>x.Venue).AsQueryable();
        if(!tracking) query=query.AsNoTracking();
        return query.SingleOrDefaultAsync(x=>x.MatchId==matchId,ct);
    }
    public Task<bool> GenerationExistsAsync(int competitionId,int phaseId,int? phaseGroupId,CancellationToken ct)=>db.FixtureGenerations.AnyAsync(x=>x.CompetitionId==competitionId&&x.PhaseId==phaseId&&x.PhaseGroupId==phaseGroupId,ct);
    public async Task<IReadOnlyList<FixtureGeneration>> ListGenerationsAsync(int competitionId,CancellationToken ct)=>await db.FixtureGenerations.AsNoTracking().Where(x=>x.CompetitionId==competitionId).ToListAsync(ct);
    public async Task<IReadOnlyList<Match>> ListMatchesAsync(int competitionId,CancellationToken ct)=>await db.Matches.AsNoTracking().Include(x=>x.HomeTeamEntry).ThenInclude(x=>x!.Team).Include(x=>x.AwayTeamEntry).ThenInclude(x=>x!.Team).Where(x=>x.CompetitionId==competitionId).OrderBy(x=>x.PhaseId).ThenBy(x=>x.RoundNumber).ThenBy(x=>x.MatchNumber).ToListAsync(ct);
    public void AddGeneration(FixtureGeneration generation)=>db.FixtureGenerations.Add(generation);
    public void AddMatches(IEnumerable<Match> matches)=>db.Matches.AddRange(matches);
}
