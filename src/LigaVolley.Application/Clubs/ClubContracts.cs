namespace LigaVolley.Application.Clubs;
public sealed record CreateClubRequest(string Name, string? ShortName);
public sealed record UpdateClubRequest(string Name, string? ShortName);
public sealed record ClubDto(int ClubId, string Name, string? ShortName, bool Active);
public sealed record ClubSummaryDto(int ClubId, string Name, string? ShortName, bool Active);
