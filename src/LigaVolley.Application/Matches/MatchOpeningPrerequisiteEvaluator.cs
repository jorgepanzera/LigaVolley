using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchOfficials;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Application.People;

namespace LigaVolley.Application.Matches;

public sealed record MatchOpeningPrerequisiteResult(IReadOnlyList<MatchReadinessIssueDto> Blockers,
    IReadOnlyList<MatchReadinessIssueDto> Warnings) { public bool Ready => Blockers.Count == 0; }

public sealed class MatchOpeningPrerequisiteEvaluator
{
    public const int MinimumPlayers = 6;
    public MatchOpeningPrerequisiteResult Evaluate(Match match, CompetitionRoster? home,
        CompetitionRoster? away, IReadOnlyList<MatchOfficial> officials, MatchSheet? sheet)
    {
        var blockers = new List<MatchReadinessIssueDto>();
        var warnings = new List<MatchReadinessIssueDto>();
        if (match.Status != MatchStatus.Scheduled)
            blockers.Add(new("match_readiness_match_not_scheduled", "Match must be Scheduled.", null));
        Team(home, MatchSide.Home, blockers, warnings);
        Team(away, MatchSide.Away, blockers, warnings);
        Official(MatchOfficialRole.FirstReferee, "match_readiness_first_referee_missing", "First Referee is missing.");
        Official(MatchOfficialRole.SecondReferee, "match_readiness_second_referee_missing", "Second Referee is missing.");
        Official(MatchOfficialRole.Scorer, "match_readiness_scorer_missing", "Scorer is missing.");
        if (sheet is not null) blockers.Add(new("match_readiness_match_sheet_already_exists", "MatchSheet already exists.", null));
        if (!match.MatchDate.HasValue) warnings.Add(new("match_readiness_match_date_missing", "Match date is missing.", null));
        if (!match.VenueId.HasValue) warnings.Add(new("match_readiness_venue_missing", "Venue is missing.", null));
        return new(blockers, warnings);

        void Official(MatchOfficialRole role, string code, string message)
        { if (officials.All(x => x.Role != role)) blockers.Add(new(code, message, null)); }
    }

    private static void Team(CompetitionRoster? roster, MatchSide side, List<MatchReadinessIssueDto> blockers,
        List<MatchReadinessIssueDto> warnings)
    {
        if (roster is null) { blockers.Add(new("match_readiness_roster_missing", "Competition roster is missing.", side)); return; }
        if (roster.Status != CompetitionRosterStatus.Active)
            blockers.Add(new("match_readiness_roster_not_active", "Competition roster must be Active.", side));
        var active = roster.Players.Where(x => x.Status == CompetitionRosterMemberStatus.Active).ToArray();
        if (active.Length < MinimumPlayers)
            blockers.Add(new("match_readiness_insufficient_active_players", $"At least {MinimumPlayers} active players are required.", side));
        var missing = active.Count(x => HealthCardEvaluator.Evaluate(x.Player.Person).Status == Domain.People.HealthCardStatus.Missing);
        var expired = active.Count(x => HealthCardEvaluator.Evaluate(x.Player.Person).Status == Domain.People.HealthCardStatus.Expired);
        if (missing > 0) warnings.Add(new("match_readiness_health_card_missing", $"{missing} active player(s) have no Health Card.", side, missing));
        if (expired > 0) warnings.Add(new("match_readiness_health_card_expired", $"{expired} active player(s) have an expired Health Card.", side, expired));
    }
}
