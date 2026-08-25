using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface IStandingsRepository
{
    Task<bool> PhaseGroupExistsAsync(int phaseGroupId, CancellationToken ct);
    Task<IReadOnlyList<TeamEntry>> ListPhaseParticipantsAsync(int competitionId, CancellationToken ct);
    Task<IReadOnlyList<TeamEntry>> ListGroupParticipantsAsync(int competitionId, int phaseGroupId, CancellationToken ct);
    Task<IReadOnlyList<Match>> ListScopeMatchesAsync(int competitionId, int phaseId, int? phaseGroupId, CancellationToken ct);
}
