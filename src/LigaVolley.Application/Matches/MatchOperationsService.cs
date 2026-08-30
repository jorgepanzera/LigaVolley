using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.MatchOfficials;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.Matches;

public sealed class MatchOperationsService(IFixtureRepository fixtures, ICompetitionRosterRepository rosters,
    IMatchOfficialRepository officials, IMatchSheetRepository sheets, MatchOpeningPrerequisiteEvaluator evaluator)
{
    public async Task<MatchReadinessDto> GetReadinessAsync(int id, CancellationToken ct)
    {
        var match = await fixtures.GetMatchAsync(id, false, ct) ?? throw new ResourceNotFoundException("Match", id);
        if (match.HomeTeamEntry is null || match.AwayTeamEntry is null)
            throw new ResourceConflictException("match_readiness_participants_unresolved", "Match participants are not resolved.");
        var home = await rosters.GetAsync(match.HomeTeamEntryId!.Value, false, ct);
        var away = await rosters.GetAsync(match.AwayTeamEntryId!.Value, false, ct);
        var assigned = await officials.ListAsync(id, false, ct);
        var sheet = await sheets.GetAsync(id, false, ct);
        var result = evaluator.Evaluate(match, home, away, assigned, sheet);
        return new(id, match.Status, result.Ready, Team(match.HomeTeamEntry.TeamEntryId, match.HomeTeamEntry.Team.Name, home),
            Team(match.AwayTeamEntry.TeamEntryId, match.AwayTeamEntry.Team.Name, away),
            new(Has(MatchOfficialRole.FirstReferee), Has(MatchOfficialRole.SecondReferee), Has(MatchOfficialRole.Scorer)),
            new(sheet is not null, sheet?.SheetUuid, sheet?.Status), result.Blockers, result.Warnings);
        bool Has(MatchOfficialRole role) => assigned.Any(x => x.Role == role);
    }

    public async Task<AdminMatchSheetDto> GetMatchSheetAsync(int id, CancellationToken ct)
    {
        _ = await fixtures.GetMatchAsync(id, false, ct) ?? throw new ResourceNotFoundException("Match", id);
        var sheet = await sheets.GetAsync(id, false, ct);
        if (sheet is null) return new(id, false, null);
        var session = sheet.Sessions.SingleOrDefault(x => x.Status == MatchSheetSessionStatus.Active)
            ?? sheet.Sessions.OrderByDescending(x => x.StartedAt).FirstOrDefault();
        var current = sheet.Sets.OrderByDescending(x => x.SetNumber).FirstOrDefault(x => x.Status != Domain.Fixtures.MatchSetStatus.Finished)
            ?? sheet.Sets.OrderByDescending(x => x.SetNumber).FirstOrDefault();
        var decided = sheet.HomeSets == 3 || sheet.AwaySets == 3;
        MatchSide? winner = sheet.HomeSets == 3 ? MatchSide.Home : sheet.AwaySets == 3 ? MatchSide.Away : null;
        var state = new AdminMatchOperationalSummaryDto(current?.SetNumber, sheet.HomeSets, sheet.AwaySets,
            current?.HomePoints, current?.AwayPoints, current?.CurrentServingSide, decided, winner,
            sheet.Sets.OrderBy(x => x.SetNumber).Select(x => new AdminSetSummaryDto(x.SetNumber, x.Status, x.HomePoints, x.AwayPoints, x.WinnerSide)).ToArray());
        return new(id, true, new(sheet.SheetUuid, sheet.Status, sheet.OpenedAt, sheet.EndedAt,
            sheet.LastOperationalUpdateAt, session is null ? null : new(session.SessionUuid, session.Status, session.DeviceId,
                session.LastAcceptedSequence), state));
    }

    private static MatchReadinessTeamDto Team(int entryId, string name, CompetitionRoster? roster)
    {
        var active = roster?.Players.Where(x => x.Status == CompetitionRosterMemberStatus.Active).ToArray() ?? [];
        return new(entryId, name, roster?.CompetitionRosterId, roster?.Status, active.Length,
            MatchOpeningPrerequisiteEvaluator.MinimumPlayers, active.Count(x => x.Role == PlayerRole.Libero),
            roster?.Staff.Count(x => x.Status == CompetitionRosterMemberStatus.Active) ?? 0);
    }
}
