using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface ICompetitionProgressionRepository
{
    Task<T> ExecuteExclusiveAsync<T>(int competitionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    Task<IReadOnlyList<Match>> ListMatchesAsync(int competitionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompetitionMovement>> ListMovementsAsync(int competitionId, CancellationToken cancellationToken);
    void AddMovement(CompetitionMovement movement);
    Task<TeamEntry> RequiredTeamEntryAsync(int competitionId, int teamEntryId, CancellationToken cancellationToken);
}
