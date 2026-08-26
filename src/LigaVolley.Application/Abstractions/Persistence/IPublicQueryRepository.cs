using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface IPublicQueryRepository
{
    Task<IReadOnlyList<Season>> ListSeasonsAsync(CancellationToken ct);
    Task<IReadOnlyList<Competition>> ListCompetitionsAsync(int? seasonId, int? divisionId, Gender? gender, CompetitionStatus? status, CancellationToken ct);
    Task<Competition?> GetCompetitionAsync(int competitionId, CancellationToken ct);
    Task<IReadOnlyList<TeamEntry>> ListTeamsAsync(int competitionId, CancellationToken ct);
    Task<IReadOnlyList<Match>> ListMatchesAsync(int competitionId, CancellationToken ct);
    Task<Match?> GetMatchAsync(int matchId, CancellationToken ct);
    Task<MatchSheet?> GetMatchSheetAsync(int matchId, CancellationToken ct);
}
