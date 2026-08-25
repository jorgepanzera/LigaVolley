using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface IPhaseCompletionRepository
{
    Task<T> ExecuteExclusiveAsync<T>(int competitionId, int phaseId, Func<CancellationToken,Task<T>> action, CancellationToken ct);
    Task<IReadOnlyList<Match>> ListPhaseMatchesAsync(int competitionId, int phaseId, CancellationToken ct);
    Task<IReadOnlyList<PhaseGroupEntry>> ListGroupEntriesAsync(int competitionId, CancellationToken ct);
    Task<IReadOnlyList<TeamEntry>> ListTeamEntriesAsync(int competitionId, CancellationToken ct);
    void AddGroupEntries(IEnumerable<PhaseGroupEntry> entries);
}
