namespace LigaVolley.Application.Seasons;

public sealed record CreateSeasonRequest(short Year, string Name, DateOnly? StartDate, DateOnly? EndDate);
public sealed record UpdateSeasonRequest(short Year, string Name, DateOnly? StartDate, DateOnly? EndDate);
public sealed record SeasonDto(int SeasonId, short Year, string Name, DateOnly? StartDate, DateOnly? EndDate, bool Active);
public sealed record SeasonSummaryDto(int SeasonId, short Year, string Name, bool Active);
