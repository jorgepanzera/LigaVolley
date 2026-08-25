using System.Data;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class CompetitionProgressionRepository(LigaVolleyDbContext db) : ICompetitionProgressionRepository
{
    public async Task<T> ExecuteExclusiveAsync<T>(int competitionId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC sp_getapplock @Resource={"LigaVolley:PlayoffProgression:" + competitionId}, @LockMode={"Exclusive"}, @LockOwner={"Transaction"}, @LockTimeout={30000}", ct);
        db.ChangeTracker.Clear();
        var result = await action(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<Match>> ListMatchesAsync(int competitionId, CancellationToken ct) =>
        await db.Matches.AsNoTracking().Where(x => x.CompetitionId == competitionId).OrderBy(x => x.MatchId).ToListAsync(ct);
}
