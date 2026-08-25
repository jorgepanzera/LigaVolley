using LigaVolley.Domain.Fixtures;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface ICompetitionProgressionRepository
{
    Task<T> ExecuteExclusiveAsync<T>(int competitionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    Task<IReadOnlyList<Match>> ListMatchesAsync(int competitionId, CancellationToken cancellationToken);
}
