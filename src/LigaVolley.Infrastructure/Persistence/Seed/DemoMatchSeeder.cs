using LigaVolley.Application.Fixtures;
using LigaVolley.Application.Matches;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchOfficials;
using LigaVolley.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Seed;

public sealed record DemoMatchSeedResult(
    int CompetitionId,
    string CompetitionName,
    int MatchId,
    string HomeTeam,
    string AwayTeam,
    string Venue,
    string ScorerPath,
    string PublicCompetitionPath,
    string PublicMatchPath);

public sealed class DemoMatchSeedException(string message) : Exception(message);

public sealed class DemoMatchSeeder(
    LigaVolleyDbContext db,
    Livosur2026Seeder livosur,
    FixtureService fixtures,
    MatchAdminService matches)
{
    private const string DemoDocumentType = "DEMO";
    private const string DocumentPrefix = "LV-DEMO-";

    public async Task<DemoMatchSeedResult> SeedAsync(CancellationToken ct = default)
    {
        await livosur.SeedAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var competition = await SelectCompetitionAsync(ct);
            if (!await db.Matches.AnyAsync(x => x.CompetitionId == competition.CompetitionId, ct))
                await fixtures.GenerateInitialAsync(competition.CompetitionId, new GenerateFixtureRequest(2026), ct);

            var match = await SelectMatchAsync(competition.CompetitionId, ct);
            var venue = await db.Venues.AsNoTracking().Where(x => x.Active).OrderBy(x => x.VenueId).FirstOrDefaultAsync(ct)
                ?? throw new DemoMatchSeedException("The LIVOSUR dataset does not contain an active Venue.");

            if (match.Status == MatchStatus.Pending || match.VenueId != venue.VenueId || match.MatchDate is null)
            {
                await matches.ScheduleAsync(match.MatchId, new ScheduleMatchRequest(
                    match.MatchDate is DateTime existingDate
                        ? new DateTimeOffset(DateTime.SpecifyKind(existingDate, DateTimeKind.Utc))
                        : DateTimeOffset.UtcNow.AddDays(1),
                    venue.VenueId), ct);
            }

            var homePlayers = await EnsurePlayersAsync("HOME", match.HomeTeamEntry!.Team.Name, ct);
            var awayPlayers = await EnsurePlayersAsync("AWAY", match.AwayTeamEntry!.Team.Name, ct);
            var homeCoach = await EnsureCoachAsync("HOME", ct);
            var awayCoach = await EnsureCoachAsync("AWAY", ct);
            var referees = await EnsureRefereesAsync(ct);

            await EnsureRosterAsync(match.HomeTeamEntry, homePlayers, homeCoach, ct);
            await EnsureRosterAsync(match.AwayTeamEntry, awayPlayers, awayCoach, ct);
            await EnsureOfficialsAsync(match, referees, ct);

            await transaction.CommitAsync(ct);
            return new(
                competition.CompetitionId,
                competition.Name,
                match.MatchId,
                match.HomeTeamEntry.Team.Name,
                match.AwayTeamEntry.Team.Name,
                venue.Name,
                $"/?matchId={match.MatchId}",
                $"/competitions/{competition.CompetitionId}",
                $"/matches/{match.MatchId}");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Competition> SelectCompetitionAsync(CancellationToken ct)
    {
        var existingDemoCompetitionId = await db.MatchOfficials
            .Where(x => x.Referee.Person.DocumentType == DemoDocumentType && x.Referee.Person.DocumentNumber == $"{DocumentPrefix}REF-01")
            .Select(x => (int?)x.Match.CompetitionId)
            .FirstOrDefaultAsync(ct);

        var query = db.Competitions
            .Include(x => x.CompetitionFormat)
            .Include(x => x.Phases)
            .Where(x => x.CompetitionFormat.Code == "ROUND_ROBIN" &&
                        (db.TeamEntries.Count(e => e.CompetitionId == x.CompetitionId &&
                             (e.Status == Domain.TeamEntries.TeamEntryStatus.Registered || e.Status == Domain.TeamEntries.TeamEntryStatus.Active)) == 7 ||
                         db.TeamEntries.Count(e => e.CompetitionId == x.CompetitionId &&
                             (e.Status == Domain.TeamEntries.TeamEntryStatus.Registered || e.Status == Domain.TeamEntries.TeamEntryStatus.Active)) == 8));
        var competition = existingDemoCompetitionId.HasValue
            ? await query.SingleOrDefaultAsync(x => x.CompetitionId == existingDemoCompetitionId.Value, ct)
            : await query.OrderBy(x => x.CompetitionId).FirstOrDefaultAsync(ct);
        return competition ?? throw new DemoMatchSeedException("No LIVOSUR ROUND_ROBIN Competition with 7 or 8 operational TeamEntry records was found.");
    }

    private async Task<Match> SelectMatchAsync(int competitionId, CancellationToken ct)
    {
        var allMatches = db.Matches
            .Include(x => x.HomeTeamEntry).ThenInclude(x => x!.Team)
            .Include(x => x.AwayTeamEntry).ThenInclude(x => x!.Team)
            .Where(x => x.CompetitionId == competitionId);
        var marked = await allMatches.FirstOrDefaultAsync(x => db.MatchOfficials.Any(o => o.MatchId == x.MatchId &&
            o.Referee.Person.DocumentType == DemoDocumentType && o.Referee.Person.DocumentNumber == $"{DocumentPrefix}REF-01"), ct);
        if (marked is not null) return marked;
        return await allMatches.Where(x => !db.MatchSheets.Any(s => s.MatchId == x.MatchId))
                   .Where(x => x.Status == MatchStatus.Pending || x.Status == MatchStatus.Scheduled)
                   .Where(x => !db.MatchOfficials.Any(o => o.MatchId == x.MatchId))
                   .OrderBy(x => x.RoundNumber).ThenBy(x => x.MatchNumber).FirstOrDefaultAsync(ct)
               ?? throw new DemoMatchSeedException("No pending/scheduled fixture Match without MatchSheet or officials is available for the demo.");
    }

    private async Task<IReadOnlyList<Player>> EnsurePlayersAsync(string side, string teamName, CancellationToken ct)
    {
        var result = new List<Player>();
        for (var i = 1; i <= 8; i++)
        {
            var person = await EnsurePersonAsync($"{DocumentPrefix}{side}-P{i:00}", $"{side} {i:00}", $"Demo {teamName}", ct);
            var player = await db.Players.SingleOrDefaultAsync(x => x.PersonId == person.PersonId, ct);
            if (player is null) { player = new Player(person); db.Players.Add(player); await db.SaveChangesAsync(ct); }
            result.Add(player);
        }
        return result;
    }

    private async Task<Coach> EnsureCoachAsync(string side, CancellationToken ct)
    {
        var person = await EnsurePersonAsync($"{DocumentPrefix}{side}-COACH", "Coach", $"Demo {side}", ct);
        var coach = await db.Coaches.SingleOrDefaultAsync(x => x.PersonId == person.PersonId, ct);
        if (coach is null) { coach = new Coach(person); db.Coaches.Add(coach); await db.SaveChangesAsync(ct); }
        return coach;
    }

    private async Task<IReadOnlyList<Referee>> EnsureRefereesAsync(CancellationToken ct)
    {
        var result = new List<Referee>();
        for (var i = 1; i <= 3; i++)
        {
            var person = await EnsurePersonAsync($"{DocumentPrefix}REF-{i:00}", "Referee", $"Demo {i:00}", ct);
            var referee = await db.Referees.SingleOrDefaultAsync(x => x.PersonId == person.PersonId, ct);
            if (referee is null) { referee = new Referee(person); db.Referees.Add(referee); await db.SaveChangesAsync(ct); }
            result.Add(referee);
        }
        return result;
    }

    private async Task<Person> EnsurePersonAsync(string documentNumber, string firstName, string lastName, CancellationToken ct)
    {
        var person = await db.People.SingleOrDefaultAsync(x => x.DocumentType == DemoDocumentType && x.DocumentNumber == documentNumber, ct);
        if (person is not null) return person;
        person = new Person(DemoDocumentType, documentNumber, firstName, lastName, new DateOnly(2000, 1, 1), null, null, null);
        db.People.Add(person); await db.SaveChangesAsync(ct); return person;
    }

    private async Task EnsureRosterAsync(Domain.TeamEntries.TeamEntry entry, IReadOnlyList<Player> players, Coach coach, CancellationToken ct)
    {
        var roster = await db.CompetitionRosters.Include(x => x.Players).Include(x => x.Staff)
            .SingleOrDefaultAsync(x => x.TeamEntryId == entry.TeamEntryId, ct);
        if (roster is null) { roster = new CompetitionRoster(entry); db.CompetitionRosters.Add(roster); await db.SaveChangesAsync(ct); }
        if (roster.Status == CompetitionRosterStatus.Closed) throw new DemoMatchSeedException($"Roster for TeamEntry {entry.TeamEntryId} is CLOSED.");
        for (var i = 0; i < players.Count; i++)
            if (roster.Players.All(x => x.PlayerId != players[i].PlayerId))
                roster.AddPlayer(players[i], (short)(i + 1), i == players.Count - 1 ? PlayerRole.Libero : PlayerRole.Setter);
        if (roster.Staff.All(x => x.CoachId != coach.CoachId)) roster.AddStaff(coach);
        if (roster.Status == CompetitionRosterStatus.Draft) roster.Activate();
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureOfficialsAsync(Match match, IReadOnlyList<Referee> referees, CancellationToken ct)
    {
        var roles = new[] { MatchOfficialRole.FirstReferee, MatchOfficialRole.SecondReferee, MatchOfficialRole.Scorer };
        var existing = await db.MatchOfficials.Where(x => x.MatchId == match.MatchId).ToListAsync(ct);
        if (existing.Count != 0 && (existing.Count != 3 || existing.Any(x => !roles.Contains(x.Role))))
            throw new DemoMatchSeedException("The selected Match has incompatible official assignments.");
        for (var i = 0; i < roles.Length; i++)
        {
            var official = existing.SingleOrDefault(x => x.Role == roles[i]);
            if (official is null) db.MatchOfficials.Add(new MatchOfficial(match, referees[i], roles[i]));
            else if (official.RefereeId != referees[i].RefereeId) throw new DemoMatchSeedException($"Role {roles[i]} is assigned to a non-demo Referee.");
        }
        await db.SaveChangesAsync(ct);
    }
}
