using LigaVolley.Application.PhaseCompletion;
using LigaVolley.Domain.Competitions;

namespace LigaVolley.Application.PlayoffProgression;

public sealed record PlayoffProgressionResult(
    int SeriesId,
    PlayoffSeriesStatus SeriesStatus,
    int Team1Wins,
    int Team2Wins,
    int? WinnerTeamEntryId,
    int? NextMatchId,
    IReadOnlyList<ResolvedSeriesDto> UpdatedSeries,
    IReadOnlyList<int> GeneratedMatchIds,
    IReadOnlyList<int> FinishedPhaseIds);
