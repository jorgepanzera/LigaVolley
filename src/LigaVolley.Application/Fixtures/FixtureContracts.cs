using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.Fixtures;

public sealed record GenerateFixtureRequest(int? RandomSeed);
public sealed record GenerateFixtureResponse(int CompetitionId, int MatchesCreated, int RandomSeed, IReadOnlyList<GeneratedFixturePhaseDto> Phases);
public sealed record GeneratedFixturePhaseDto(int PhaseId, string PhaseCode, int MatchesCreated);
public sealed record CompetitionFixtureDto(int CompetitionId, IReadOnlyList<FixturePhaseDto> Phases);
public sealed record FixtureGenerationDto(int FixtureGenerationId, int RandomSeed, DateTime GeneratedAt);
public sealed record FixtureTeamEntryDto(int TeamEntryId, int TeamId, string TeamName, TeamEntryStatus Status);
public sealed record FixtureMatchDto(int MatchId, short RoundNumber, short MatchNumber, FixtureTeamEntryDto HomeTeam, FixtureTeamEntryDto AwayTeam, DateTime? MatchDate, int? VenueId, MatchStatus Status);
public sealed record FixtureGroupDto(int PhaseGroupId, string Code, string Name, FixtureMode FixtureMode, short Rounds, bool Generated, FixtureGenerationDto? Generation, IReadOnlyList<FixtureMatchDto> Matches);
public sealed record FixtureSeriesDto(int PlayoffSeriesId, string Code, string Name, bool Generated, IReadOnlyList<FixtureMatchDto> Matches);
public sealed record FixturePhaseDto(int PhaseId, string Code, string Name, PhaseType PhaseType, FixtureMode? FixtureMode, short? Rounds, bool Generated, FixtureGenerationDto? Generation, IReadOnlyList<FixtureMatchDto> Matches, IReadOnlyList<FixtureGroupDto> Groups, IReadOnlyList<FixtureSeriesDto> Series);
