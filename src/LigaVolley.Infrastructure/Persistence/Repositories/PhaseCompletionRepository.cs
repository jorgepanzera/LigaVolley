using System.Data;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class PhaseCompletionRepository(LigaVolleyDbContext db) : IPhaseCompletionRepository
{
    public async Task<T> ExecuteExclusiveAsync<T>(int competitionId,int phaseId,Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_getapplock @Resource={"LigaVolley:PhaseCompletion:"+competitionId+":"+phaseId}, @LockMode={"Exclusive"}, @LockOwner={"Transaction"}, @LockTimeout={30000}",ct);
        db.ChangeTracker.Clear();
        var result=await action(ct); await transaction.CommitAsync(ct); return result;
    }
    public async Task<IReadOnlyList<Match>> ListPhaseMatchesAsync(int competitionId,int phaseId,CancellationToken ct)
        =>await db.Matches.Include(x=>x.Sets).Where(x=>x.CompetitionId==competitionId&&x.PhaseId==phaseId).OrderBy(x=>x.MatchId).ToListAsync(ct);
    public async Task<IReadOnlyList<PhaseGroupEntry>> ListGroupEntriesAsync(int competitionId,CancellationToken ct)
        =>await db.PhaseGroupEntries.Include(x=>x.TeamEntry).ThenInclude(x=>x.Team).Where(x=>x.CompetitionId==competitionId).ToListAsync(ct);
    public async Task<IReadOnlyList<TeamEntry>> ListTeamEntriesAsync(int competitionId,CancellationToken ct)
        =>await db.TeamEntries.Include(x=>x.Team).Where(x=>x.CompetitionId==competitionId).ToListAsync(ct);
    public void AddGroupEntries(IEnumerable<PhaseGroupEntry> entries)=>db.PhaseGroupEntries.AddRange(entries);
}
