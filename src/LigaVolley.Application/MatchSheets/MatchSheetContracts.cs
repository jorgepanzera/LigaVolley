using LigaVolley.Domain.CompetitionRosters;using LigaVolley.Domain.Fixtures;using LigaVolley.Domain.MatchOfficials;using LigaVolley.Domain.MatchSheets;using LigaVolley.Domain.People;
namespace LigaVolley.Application.MatchSheets;
public sealed record OpenMatchTeamRequest(IReadOnlyList<int> CompetitionRosterPlayerIds,int? CaptainCompetitionRosterPlayerId,IReadOnlyList<int> LiberoCompetitionRosterPlayerIds,IReadOnlyList<int> CompetitionRosterStaffIds);
public sealed record OpenMatchSheetRequest(Guid ClientRequestId,string DeviceId,OpenMatchTeamRequest Home,OpenMatchTeamRequest Away)
{ public bool TrackSubstitutions{get;init;}=true;public bool TrackLiberoReplacements{get;init;}=true; }
public sealed record OpenMatchSheetResponse(bool AlreadyOpen,MatchSheetSnapshotDto MatchSheet);
public sealed record OpenMatchContextDto(MatchContextDto Match,CompetitionContextDto Competition,OpenMatchTeamContextDto Home,OpenMatchTeamContextDto Away,IReadOnlyList<MatchSheetOfficialDto> MatchOfficials,IReadOnlyList<string> Warnings,MatchSheetSummaryDto? ExistingMatchSheet);
public sealed record MatchContextDto(int MatchId,MatchStatus Status,DateTime? MatchDate,short RoundNumber,short MatchNumber,int HomeTeamEntryId,int AwayTeamEntryId,string? VenueName);
public sealed record CompetitionContextDto(int CompetitionId,string CompetitionName,string Season,string Division,string Phase,string? PhaseGroup,string? PlayoffSeries);
public sealed record OpenMatchTeamContextDto(int TeamEntryId,int TeamId,string TeamName,int CompetitionRosterId,CompetitionRosterStatus RosterStatus,IReadOnlyList<OpenRosterPlayerDto> Players,IReadOnlyList<OpenRosterStaffDto> Staff);
public sealed record OpenRosterPlayerDto(int CompetitionRosterPlayerId,int PlayerId,int PersonId,string DisplayName,short? JerseyNumber,PlayerRole Role,HealthCardStatus HealthCardStatus);
public sealed record OpenRosterStaffDto(int CompetitionRosterStaffId,int CoachId,int PersonId,string DisplayName);
public sealed record MatchSheetSummaryDto(int MatchSheetId,Guid SheetUuid,MatchSheetStatus Status,DateTimeOffset OpenedAt);
public sealed record MatchSheetSnapshotDto(MatchSheetSummaryDto Sheet,MatchContextDto Match,CompetitionContextDto Competition,MatchSheetTeamDto Home,MatchSheetTeamDto Away,IReadOnlyList<MatchSheetOfficialDto> Officials,MatchSheetCurrentStateDto CurrentState,MatchSheetSessionDto Session,MatchSheetSyncDto Synchronization,ScorerOperationalStateDto OperationalState);
public sealed record MatchSheetTeamDto(int MatchTeamId,Guid MatchTeamUuid,MatchSide Side,int TeamEntryId,int TeamId,string TeamName,int CompetitionRosterId,IReadOnlyList<MatchSheetPlayerDto> Players,IReadOnlyList<MatchSheetStaffDto> Staff,IReadOnlyList<MatchSheetLiberoDto> Liberos);
public sealed record MatchSheetPlayerDto(int MatchPlayerId,Guid MatchPlayerUuid,int CompetitionRosterPlayerId,int PlayerId,int PersonId,string DisplayName,short? JerseyNumber,PlayerRole Role,bool IsMatchCaptain,MatchPlayerStatus Status,HealthCardStatus HealthCardStatus);
public sealed record MatchSheetStaffDto(int MatchTeamStaffId,Guid MatchTeamStaffUuid,int CompetitionRosterStaffId,int CoachId,int PersonId,string DisplayName,MatchTeamStaffStatus Status);
public sealed record MatchSheetLiberoDto(int MatchLiberoId,Guid MatchLiberoUuid,int MatchPlayerId,byte LiberoOrder);
public sealed record MatchSheetOfficialDto(int MatchOfficialId,MatchOfficialRole Role,int RefereeId,int PersonId,string DisplayName,HealthCardStatus HealthCardStatus);
public sealed record MatchSheetCurrentStateDto(byte? CurrentSetNumber,byte HomeSets,byte AwaySets,short HomePoints,short AwayPoints,MatchSide? ServingSide,int? ServerMatchPlayerId,short HomeRotationOffset,short AwayRotationOffset,byte HomeTimeouts,byte AwayTimeouts);
public sealed record MatchSheetSessionDto(int MatchSheetSessionId,Guid SessionUuid,int MatchOfficialId,string DeviceId,MatchSheetSessionStatus Status,long LastAcceptedSequence,DateTimeOffset StartedAt,DateTimeOffset? EndedAt);
public sealed record MatchSheetSyncDto(Guid SheetUuid,Guid SessionUuid,long ServerVersion);
public sealed record ScorerOperationalStateDto(string Status,byte HomeSets,byte AwaySets,byte? CurrentSetNumber,IReadOnlyList<ScorerSetStateDto> Sets,bool MatchDecided,bool Closed,bool CloseConfirmed);
public sealed record ScorerSetStateDto(byte SetNumber,string Status,short HomePoints,short AwayPoints,string? InitialServingSide,string? ServingSide,byte HomeRotationOffset,byte AwayRotationOffset,byte HomeTimeouts,byte AwayTimeouts,string? WinnerSide,IReadOnlyDictionary<string,int[]> Lineups,IReadOnlyList<ScorerSubstitutionStateDto> Substitutions,IReadOnlyList<ScorerLiberoStateDto> LiberoReplacements,IReadOnlyList<string> Points,string? LastSportingEvent);
public sealed record ScorerSubstitutionStateDto(string Side,int Position,int PlayerOutMatchPlayerId,int PlayerInMatchPlayerId);
public sealed record ScorerLiberoStateDto(string Side,int Position,int LiberoMatchPlayerId,int ReplacedMatchPlayerId,bool Active);
