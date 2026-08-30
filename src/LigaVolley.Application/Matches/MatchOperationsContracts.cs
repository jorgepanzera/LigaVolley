using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.Matches;

public sealed record MatchReadinessDto(int MatchId, MatchStatus MatchStatus, bool ReadyForScorer,
    MatchReadinessTeamDto Home, MatchReadinessTeamDto Away, MatchReadinessOfficialsDto Officials,
    MatchReadinessSheetDto MatchSheet, IReadOnlyList<MatchReadinessIssueDto> Blockers,
    IReadOnlyList<MatchReadinessIssueDto> Warnings);
public sealed record MatchReadinessTeamDto(int TeamEntryId, string TeamName, int? CompetitionRosterId,
    CompetitionRosterStatus? RosterStatus, int ActivePlayers, int MinimumPlayersRequired,
    int ActiveLiberos, int ActiveStaff);
public sealed record MatchReadinessOfficialsDto(bool HasFirstReferee, bool HasSecondReferee, bool HasScorer);
public sealed record MatchReadinessSheetDto(bool Exists, Guid? MatchSheetUuid, MatchSheetStatus? Status);
public sealed record MatchReadinessIssueDto(string Code, string Message, MatchSide? Side, int? Count = null);

public sealed record AdminMatchSheetDto(int MatchId, bool Exists, AdminMatchSheetSummaryDto? Sheet);
public sealed record AdminMatchSheetSummaryDto(Guid MatchSheetUuid, MatchSheetStatus Status,
    DateTimeOffset? OpenedAt, DateTimeOffset? ClosedAt, DateTimeOffset? LastOperationalUpdateAt,
    AdminMatchSheetSessionDto? Session, AdminMatchOperationalSummaryDto? OperationalState);
public sealed record AdminMatchSheetSessionDto(Guid SessionUuid, MatchSheetSessionStatus Status,
    string? DeviceId, long LastAcceptedSequence);
public sealed record AdminMatchOperationalSummaryDto(int? CurrentSetNumber, int HomeSets, int AwaySets,
    int? HomePoints, int? AwayPoints, MatchSide? ServingSide, bool MatchDecided, MatchSide? WinnerSide,
    IReadOnlyList<AdminSetSummaryDto> Sets);
public sealed record AdminSetSummaryDto(int SetNumber, MatchSetStatus Status, int HomePoints, int AwayPoints,
    MatchSide? WinnerSide);
