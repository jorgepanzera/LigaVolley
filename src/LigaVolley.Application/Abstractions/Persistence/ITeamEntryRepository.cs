using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface ITeamEntryRepository
{
    Task<IReadOnlyList<TeamEntry>> ListAsync(int competitionId, CancellationToken ct);
    Task<TeamEntry?> GetAsync(int competitionId, int entryId, bool tracking, CancellationToken ct);
    Task<bool> TeamExistsAsync(int competitionId, int teamId, CancellationToken ct);
    Task<int> CountValidAsync(int competitionId, CancellationToken ct);
    void Add(TeamEntry entry);
    void Remove(TeamEntry entry);
}
