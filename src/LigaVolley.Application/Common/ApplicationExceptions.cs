namespace LigaVolley.Application.Common;

public sealed class ResourceNotFoundException(string resource, object id)
    : Exception($"{resource} '{id}' was not found.")
{
    public string Code { get; } = resource switch
    {
        "Person" => "person_not_found",
        "PersonAdditionalDocument" => "person_additional_document_not_found",
        "Player" => "player_not_found",
        "Coach" => "coach_not_found",
        "Referee" => "referee_not_found",
        "CompetitionRoster" => "competition_roster_not_found",
        "CompetitionRosterPlayer" => "competition_roster_player_not_found",
        "CompetitionRosterStaff" => "competition_roster_staff_not_found",
        "MatchOfficial" => "match_official_not_found",
        "MatchSheet" => "match_sheet_not_found",
        "MatchSheetSession" => "match_sheet_session_not_found",
        "MatchSet" => "match_set_not_found",
        "PublicCompetition" => "public_competition_not_found",
        "PublicMatch" => "public_match_not_found",
        "PublicLiveMatch" => "public_live_match_not_available",
        "Club" => "club_not_found",
        "ClubLogo" => "club_logo_not_found",
        "Team" => "team_not_found",
        "Venue" => "venue_not_found",
        _ => "not_found"
    };
}

public sealed class ResourceConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>();
}

public sealed class RequestValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>();
}
