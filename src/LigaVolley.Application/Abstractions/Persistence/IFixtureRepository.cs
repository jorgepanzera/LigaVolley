using LigaVolley.Domain.Fixtures;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface IFixtureRepository
{
    Task<Match?> GetMatchAsync(int matchId, bool tracking, CancellationToken ct);
    Task<bool> GenerationExistsAsync(int competitionId, int phaseId, int? phaseGroupId, CancellationToken ct);
    Task<IReadOnlyList<FixtureGeneration>> ListGenerationsAsync(int competitionId, CancellationToken ct);
    Task<IReadOnlyList<Match>> ListMatchesAsync(int competitionId, CancellationToken ct);
    void AddGeneration(FixtureGeneration generation);
    void AddMatches(IEnumerable<Match> matches);
}
