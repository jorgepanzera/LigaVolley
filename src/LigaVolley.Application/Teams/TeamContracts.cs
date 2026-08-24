using LigaVolley.Application.Clubs;
using LigaVolley.Domain.Divisions;
namespace LigaVolley.Application.Teams;
public sealed record CreateTeamRequest(string Name, Gender Gender, int? ClubId);
public sealed record UpdateTeamRequest(string Name, Gender Gender, int? ClubId);
public sealed record TeamDto(int TeamId, string Name, Gender Gender, ClubSummaryDto? Club, bool Active);
public sealed record TeamSummaryDto(int TeamId, string Name, Gender Gender, int? ClubId, string? ClubName, bool Active);
