using LigaVolley.Domain.Competitions;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface ICompetitionRepository
{
    Task<IReadOnlyList<Competition>> ListAsync(int? seasonId, int? divisionId, CompetitionStatus? status, CancellationToken ct);
    Task<Competition?> GetAsync(int id, bool tracking, CancellationToken ct);
    void Add(Competition competition);
}
