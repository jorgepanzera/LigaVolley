using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface IPlayoffProgressionRepository
{
    Task<T> ExecuteExclusiveAsync<T>(int matchId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    Task<Match?> GetMatchAsync(int matchId, CancellationToken cancellationToken);
    Task<Competition?> GetCompetitionAsync(int competitionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Match>> ListSeriesMatchesAsync(int competitionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TeamEntry>> ListTeamEntriesAsync(int competitionId, CancellationToken cancellationToken);
    void AddMatches(IEnumerable<Match> matches);
}
