using System.Data;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class PlayoffProgressionRepository(LigaVolleyDbContext db) : IPlayoffProgressionRepository
{
    public async Task<T> ExecuteExclusiveAsync<T>(int matchId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var competitionId = await db.Matches.AsNoTracking().Where(x => x.MatchId == matchId)
            .Select(x => (int?)x.CompetitionId).SingleOrDefaultAsync(ct);
        if (!competitionId.HasValue)
            return await action(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC sp_getapplock @Resource={"LigaVolley:PlayoffProgression:" + competitionId.Value}, @LockMode={"Exclusive"}, @LockOwner={"Transaction"}, @LockTimeout={30000}", ct);
        db.ChangeTracker.Clear();
        var result = await action(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public Task<Match?> GetMatchAsync(int matchId, CancellationToken ct) =>
        db.Matches.SingleOrDefaultAsync(x => x.MatchId == matchId, ct);

    public Task<Competition?> GetCompetitionAsync(int competitionId, CancellationToken ct) =>
        db.Competitions.AsSplitQuery()
            .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.Team1Entry)
            .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.Team2Entry)
            .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.WinnerTeamEntry)
            .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.ParticipantSources).ThenInclude(x => x.SourceSeries)
            .SingleOrDefaultAsync(x => x.CompetitionId == competitionId, ct);

    public async Task<IReadOnlyList<Match>> ListSeriesMatchesAsync(int competitionId, CancellationToken ct) =>
        await db.Matches.Where(x => x.CompetitionId == competitionId && x.SeriesId != null)
            .OrderBy(x => x.SeriesId).ThenBy(x => x.MatchNumber).ToListAsync(ct);

    public async Task<IReadOnlyList<TeamEntry>> ListTeamEntriesAsync(int competitionId, CancellationToken ct) =>
        await db.TeamEntries.Where(x => x.CompetitionId == competitionId).ToListAsync(ct);

    public void AddMatches(IEnumerable<Match> matches) => db.Matches.AddRange(matches);
}
