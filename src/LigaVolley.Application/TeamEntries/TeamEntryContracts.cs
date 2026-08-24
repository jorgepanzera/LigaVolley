using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.TeamEntries;

public sealed record AddTeamEntryRequest(int TeamId, short? Seed);
public sealed record SetTeamEntrySeedRequest(short? Seed);
public sealed record ChangeTeamEntryStatusRequest(TeamEntryStatus Status);
public sealed record TeamEntryDto(int TeamEntryId, int TeamId, string TeamName, short? Seed, TeamEntryStatus Status);
public sealed record TeamEntryRangeValidationDto(int CompetitionId, int ValidTeamEntries, short MinTeams, short MaxTeams, bool IsWithinLoadLimit, bool IsReadyForFixture);
