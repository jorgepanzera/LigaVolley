namespace LigaVolley.Application.Standings;

public sealed record StandingsDto(int CompetitionId, int PhaseId, string PhaseCode, string PhaseName,
    int? PhaseGroupId, string? PhaseGroupCode, string? PhaseGroupName, bool IsFinal,
    IReadOnlyList<StandingPositionDto> Positions);

public sealed record StandingPositionDto(int Position, int TeamEntryId, int TeamId, string TeamName,
    int Played, int Won, int Lost, int SetsWon, int SetsLost, decimal? SetRatio,
    int PointsWon, int PointsLost, decimal? PointRatio, int TablePoints, bool IsTied);
