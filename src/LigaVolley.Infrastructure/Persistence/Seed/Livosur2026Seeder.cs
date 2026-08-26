using System.Text.Json;
using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Teams;
using LigaVolley.Domain.Venues;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Seed;

public sealed record Livosur2026SeedResult(int Seasons, int Divisions, int Clubs, int Teams, int Venues, int Competitions, int TeamEntries);

public sealed class Livosur2026SeedException(string message) : Exception(message);

public sealed class Livosur2026Seeder(LigaVolleyDbContext db)
{
    private const string ResourceName = "LigaVolley.Infrastructure.Persistence.Seed.livosur-2026.json";

    public async Task<Livosur2026SeedResult> SeedAsync(CancellationToken ct = default)
    {
        var data = LoadDataset();
        ValidateDataset(data);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var formats = await LoadFormats(ct);

            var seasons = await SeedSeasons(data, ct);
            var divisions = await SeedDivisions(data, ct);
            var clubs = await SeedClubs(data, ct);
            var teams = await SeedTeams(data, clubs, ct);
            await SeedVenues(data, ct);
            var competitions = await SeedCompetitions(data, seasons, divisions, formats, ct);
            await SeedTeamEntries(data, competitions, teams, ct);

            await transaction.CommitAsync(ct);
            return new(data.Season.Count, data.Divisions.Count, data.Clubs.Count, data.Teams.Count, data.Venues.Count, data.Competitions.Count, data.TeamEntries.Count);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, CompetitionFormat>> LoadFormats(CancellationToken ct)
    {
        var formats = await db.CompetitionFormats
            .Include(x => x.Phases).ThenInclude(x => x.Groups)
            .Include(x => x.QualificationRules).ThenInclude(x => x.SourcePhase)
            .Include(x => x.QualificationRules).ThenInclude(x => x.TargetGroup)
            .Include(x => x.ScoringRules)
            .Include(x => x.TiebreakRules)
            .AsSplitQuery()
            .Where(x => x.Code == "ROUND_ROBIN" || x.Code == "SPLIT_STAGE")
            .ToDictionaryAsync(x => x.Code, ct);

        if (!formats.TryGetValue("ROUND_ROBIN", out var roundRobin))
        {
            roundRobin = CreateRoundRobinFormat();
            db.CompetitionFormats.Add(roundRobin);
            formats.Add("ROUND_ROBIN", roundRobin);
        }
        if (!formats.TryGetValue("SPLIT_STAGE", out var splitStage))
        {
            splitStage = CreateSplitStageFormat();
            db.CompetitionFormats.Add(splitStage);
            formats.Add("SPLIT_STAGE", splitStage);
        }
        EnsureStandingsRules(roundRobin);
        EnsureStandingsRules(splitStage);
        await db.SaveChangesAsync(ct);
        Require(roundRobin.Active && roundRobin.MinTeams <= 6 && roundRobin.MaxTeams >= 8,
            "CompetitionFormat 'ROUND_ROBIN' must be active and support 6..8 teams.");
        Require(splitStage.Active && splitStage.MinTeams <= 9 && splitStage.MaxTeams >= 10,
            "CompetitionFormat 'SPLIT_STAGE' must be active and support at least the dataset range 9..10.");

        var first = splitStage.Phases.OrderBy(x => x.Sequence).FirstOrDefault();
        Require(first is { PhaseType: PhaseType.RoundRobin, Rounds: 1, FixtureMode: FixtureMode.BalancedRandom },
            "CompetitionFormat 'SPLIT_STAGE' must begin with one BALANCED_RANDOM round-robin wheel.");
        var second = splitStage.Phases.OrderBy(x => x.Sequence).Skip(1).FirstOrDefault();
        var championship = second?.Groups.SingleOrDefault(x => x.GroupRole == GroupRole.Championship);
        var relegation = second?.Groups.SingleOrDefault(x => x.GroupRole == GroupRole.Relegation);
        Require(second?.PhaseType == PhaseType.GroupStage && championship is not null && relegation is not null,
            "CompetitionFormat 'SPLIT_STAGE' must have a second group stage with Championship and Relegation groups.");
        Require(splitStage.QualificationRules.Any(x => x.SourcePhase == first && x.SelectionMode == QualificationSelectionMode.TopHalf && x.TargetGroup == championship) &&
                splitStage.QualificationRules.Any(x => x.SourcePhase == first && x.SelectionMode == QualificationSelectionMode.BottomHalf && x.TargetGroup == relegation),
            "CompetitionFormat 'SPLIT_STAGE' must route TOP_HALF to Championship and BOTTOM_HALF to Relegation.");
        return formats;
    }

    private void EnsureStandingsRules(CompetitionFormat format)
    {
        var scoringRules = new[]
        {
            (WinnerSets: (byte)3, LoserSets: (byte)0),
            (WinnerSets: (byte)3, LoserSets: (byte)1),
            (WinnerSets: (byte)3, LoserSets: (byte)2)
        };

        foreach (var duplicate in format.ScoringRules
                     .GroupBy(x => (x.WinnerSets, x.LoserSets))
                     .SelectMany(x => x.Skip(1))
                     .ToArray())
        {
            format.ScoringRules.Remove(duplicate);
            db.Remove(duplicate);
        }

        foreach (var rule in format.ScoringRules.Where(x => !scoringRules.Contains((x.WinnerSets, x.LoserSets))).ToArray())
        {
            format.ScoringRules.Remove(rule);
            db.Remove(rule);
        }
        foreach (var definition in scoringRules)
        {
            var rule = format.ScoringRules.SingleOrDefault(x => x.WinnerSets == definition.WinnerSets && x.LoserSets == definition.LoserSets);
            if (rule is null)
                format.ScoringRules.Add(new FormatScoringRule(definition.WinnerSets, definition.LoserSets, 2, 1));
            else
                rule.UpdateTablePoints(2, 1);
        }

        var tiebreakRules = new[]
        {
            (Sequence: (short)1, Criterion: TiebreakCriterion.TablePoints),
            (Sequence: (short)2, Criterion: TiebreakCriterion.MatchWins),
            (Sequence: (short)3, Criterion: TiebreakCriterion.SetRatio),
            (Sequence: (short)4, Criterion: TiebreakCriterion.PointRatio),
            (Sequence: (short)5, Criterion: TiebreakCriterion.HeadToHead)
        };
        foreach (var duplicate in format.TiebreakRules
                     .GroupBy(x => x.Sequence)
                     .SelectMany(x => x.Skip(1))
                     .ToArray())
        {
            format.TiebreakRules.Remove(duplicate);
            db.Remove(duplicate);
        }
        foreach (var rule in format.TiebreakRules.Where(x => x.Sequence < 1 || x.Sequence > 5).ToArray())
        {
            format.TiebreakRules.Remove(rule);
            db.Remove(rule);
        }
        foreach (var definition in tiebreakRules)
        {
            var rule = format.TiebreakRules.SingleOrDefault(x => x.Sequence == definition.Sequence);
            if (rule is null)
                format.TiebreakRules.Add(new FormatTiebreakRule(definition.Sequence, definition.Criterion, SortDirection.Desc));
            else
                rule.UpdateConfiguration(definition.Sequence, definition.Criterion, SortDirection.Desc);
        }
    }

    private static CompetitionFormat CreateRoundRobinFormat()
    {
        var format = new CompetitionFormat("ROUND_ROBIN", "Round Robin", "Single round-robin competition for 6 to 8 teams.", 6, 8);
        format.Phases.Add(new FormatPhase("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom));
        return format;
    }

    private static CompetitionFormat CreateSplitStageFormat()
    {
        var format = new CompetitionFormat("SPLIT_STAGE", "Split Stage", "Single round robin followed by Championship and Relegation groups.", 9, short.MaxValue);
        var regular = new FormatPhase("REGULAR", "Regular", PhaseType.RoundRobin, PhaseRole.Regular, 1, 1, FixtureMode.BalancedRandom);
        var second = new FormatPhase("SECOND_STAGE", "Second Stage", PhaseType.GroupStage, PhaseRole.Championship, 2, null, null);
        var championship = new FormatGroup("CHAMPIONSHIP", "Championship", GroupRole.Championship, 1, 1, FixtureMode.BalancedRandom, CarryOverMode.None);
        var relegation = new FormatGroup("RELEGATION", "Relegation", GroupRole.Relegation, 2, 1, FixtureMode.BalancedRandom, CarryOverMode.None);
        second.Groups.Add(championship);
        second.Groups.Add(relegation);
        format.Phases.Add(regular);
        format.Phases.Add(second);
        format.QualificationRules.Add(new FormatQualificationRule(regular, null, QualificationSelectionMode.TopHalf, null, null,
            QualificationTargetType.Group, second, championship, null, null, 1));
        format.QualificationRules.Add(new FormatQualificationRule(regular, null, QualificationSelectionMode.BottomHalf, null, null,
            QualificationTargetType.Group, second, relegation, null, null, 2));
        return format;
    }

    internal static LivosurDataset LoadDataset()
    {
        using var stream = typeof(Livosur2026Seeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new Livosur2026SeedException($"Embedded seed resource '{ResourceName}' was not found.");
        return JsonSerializer.Deserialize<LivosurDataset>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new Livosur2026SeedException("The LIVOSUR seed resource is empty or invalid.");
    }

    private async Task<Dictionary<int, Season>> SeedSeasons(LivosurDataset data, CancellationToken ct)
    {
        var existing = await db.Seasons.Where(x => data.Season.Select(s => s.Year).Contains(x.Year)).ToListAsync(ct);
        var result = new Dictionary<int, Season>();
        foreach (var row in data.Season)
        {
            var entity = existing.SingleOrDefault(x => x.Year == row.Year);
            if (entity is null) { entity = new Season(row.Year, row.Name, null, null); entity.SetActive(row.Active); db.Seasons.Add(entity); }
            else Require(entity.Name == row.Name && entity.StartDate is null && entity.EndDate is null && entity.Active == row.Active, $"Season {row.Year} exists with incompatible data.");
            result.Add(row.SourceId, entity);
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<Dictionary<int, Division>> SeedDivisions(LivosurDataset data, CancellationToken ct)
    {
        var existing = await db.Divisions.ToListAsync(ct);
        var result = new Dictionary<int, Division>();
        foreach (var row in data.Divisions)
        {
            var gender = ParseGender(row.Gender);
            var entity = existing.SingleOrDefault(x => x.Name == row.Name && x.Gender == gender);
            var levelOwner = existing.SingleOrDefault(x => x.LevelOrder == row.LevelOrder && x.Gender == gender);
            if (entity is null && levelOwner is not null) throw new Livosur2026SeedException($"Division level {row.LevelOrder}/{gender} is already used by '{levelOwner.Name}'.");
            if (entity is null) { entity = new Division(row.Name, row.LevelOrder, gender); entity.SetActive(row.Active); db.Divisions.Add(entity); existing.Add(entity); }
            else Require(entity.LevelOrder == row.LevelOrder && entity.Active == row.Active, $"Division '{row.Name}'/{gender} exists with incompatible data.");
            result.Add(row.SourceId, entity);
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<Dictionary<int, Club>> SeedClubs(LivosurDataset data, CancellationToken ct)
    {
        var existing = await db.Clubs.ToListAsync(ct);
        var result = new Dictionary<int, Club>();
        foreach (var row in data.Clubs)
        {
            var entity = existing.SingleOrDefault(x => x.Name == row.Name);
            if (entity is null) { entity = new Club(row.Name, null); entity.SetActive(row.Active); db.Clubs.Add(entity); existing.Add(entity); }
            else Require(entity.ShortName is null && entity.Active == row.Active, $"Club '{row.Name}' exists with incompatible data.");
            result.Add(row.SourceId, entity);
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<Dictionary<int, Team>> SeedTeams(LivosurDataset data, IReadOnlyDictionary<int, Club> clubs, CancellationToken ct)
    {
        var existing = await db.Teams.Include(x => x.Club).ToListAsync(ct);
        var result = new Dictionary<int, Team>();
        foreach (var row in data.Teams)
        {
            var gender = ParseGender(row.Gender); var club = clubs[row.ClubSourceId];
            var entity = existing.SingleOrDefault(x => x.Name == row.Name && x.Gender == gender);
            if (entity is null) { entity = new Team(row.Name, gender, club); entity.SetActive(row.Active); db.Teams.Add(entity); existing.Add(entity); }
            else Require(entity.ClubId == club.ClubId && entity.Active == row.Active, $"Team '{row.Name}'/{gender} exists with incompatible club or active state.");
            result.Add(row.SourceId, entity);
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task SeedVenues(LivosurDataset data, CancellationToken ct)
    {
        var existing = await db.Venues.ToListAsync(ct);
        foreach (var row in data.Venues)
        {
            var entity = existing.SingleOrDefault(x => x.Name == row.Name);
            if (entity is null) { entity = new Venue(row.Name, null); entity.SetActive(row.Active); db.Venues.Add(entity); existing.Add(entity); }
            else Require(entity.Address is null && entity.Active == row.Active, $"Venue '{row.Name}' exists with incompatible data.");
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<int, Competition>> SeedCompetitions(LivosurDataset data, IReadOnlyDictionary<int, Season> seasons,
        IReadOnlyDictionary<int, Division> divisions, IReadOnlyDictionary<string, CompetitionFormat> formats, CancellationToken ct)
    {
        var entryCounts = data.TeamEntries.GroupBy(x => x.CompetitionSourceId).ToDictionary(x => x.Key, x => x.Count());
        foreach (var row in data.Competitions)
        {
            var count = entryCounts[row.SourceId]; var format = SelectFormat(count, formats);
            Require(format.MinTeams <= count && count <= format.MaxTeams,
                $"Competition '{row.Name}' has {count} entries, outside format '{format.Code}' range {format.MinTeams}..{format.MaxTeams}.");
        }

        var existing = await db.Competitions.Include(x => x.Phases).ToListAsync(ct);
        var result = new Dictionary<int, Competition>();
        foreach (var row in data.Competitions)
        {
            var season = seasons[row.SeasonSourceId]; var division = divisions[row.DivisionSourceId];
            var format = SelectFormat(entryCounts[row.SourceId], formats);
            var matches = existing.Where(x => x.Name == row.Name && x.SeasonId == season.SeasonId && x.DivisionId == division.DivisionId).ToArray();
            if (matches.Length > 1) throw new Livosur2026SeedException($"Multiple competitions match natural key '{row.Name}'/{season.Year}/{division.Name}.");
            var entity = matches.SingleOrDefault();
            if (entity is null) { entity = new Competition(row.Name, season, division, format, ParsePeriod(row.PeriodType), null, null); db.Competitions.Add(entity); existing.Add(entity); }
            else Require(entity.CompetitionFormatId == format.CompetitionFormatId && entity.PeriodType == ParsePeriod(row.PeriodType) && entity.StartDate is null && entity.EndDate is null,
                $"Competition '{row.Name}' exists with an incompatible format, period or dates.");
            result.Add(row.SourceId, entity);
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    private static CompetitionFormat SelectFormat(int teamCount, IReadOnlyDictionary<string, CompetitionFormat> formats) => teamCount switch
    {
        >= 6 and <= 8 => formats["ROUND_ROBIN"],
        >= 9 => formats["SPLIT_STAGE"],
        _ => throw new Livosur2026SeedException($"No LIVOSUR CompetitionFormat rule exists for {teamCount} teams.")
    };

    private async Task SeedTeamEntries(LivosurDataset data, IReadOnlyDictionary<int, Competition> competitions,
        IReadOnlyDictionary<int, Team> teams, CancellationToken ct)
    {
        var competitionIds = competitions.Values.Select(x => x.CompetitionId).ToArray();
        var existing = await db.TeamEntries.Where(x => competitionIds.Contains(x.CompetitionId)).ToListAsync(ct);
        foreach (var row in data.TeamEntries)
        {
            var competition = competitions[row.CompetitionSourceId]; var team = teams[row.TeamSourceId]; var status = ParseEntryStatus(row.Status);
            var entity = existing.SingleOrDefault(x => x.CompetitionId == competition.CompetitionId && x.TeamId == team.TeamId);
            if (entity is null) { entity = new TeamEntry(competition, team, null); entity.ChangeStatus(status); db.TeamEntries.Add(entity); existing.Add(entity); }
            else Require(entity.Seed is null && entity.Status == status, $"TeamEntry '{competition.Name}'/'{team.Name}' exists with incompatible seed or status.");
        }
        await db.SaveChangesAsync(ct);
    }

    private static void ValidateDataset(LivosurDataset data)
    {
        Require(data.Season.Count == 1 && data.Divisions.Count == 24 && data.Clubs.Count == 98 && data.Teams.Count == 211 &&
            data.Venues.Count == 55 && data.Competitions.Count == 24 && data.TeamEntries.Count == 211,
            "The embedded LIVOSUR dataset does not have the approved 1/24/98/211/55/24/211 counts.");
        Require(data.TeamEntries.Count(x => data.Teams.Single(t => t.SourceId == x.TeamSourceId).Name == "C.A JUAN E. MILLER") == 1 &&
            data.TeamEntries.Single(x => data.Teams.Single(t => t.SourceId == x.TeamSourceId).Name == "C.A JUAN E. MILLER").CompetitionSourceId == 258,
            "C.A JUAN E. MILLER must appear exactly once, in source Competition 258.");
        Require(data.Divisions.Select(x => x.SourceId).Distinct().Count() == data.Divisions.Count && data.Clubs.Select(x => x.SourceId).Distinct().Count() == data.Clubs.Count &&
            data.Teams.Select(x => x.SourceId).Distinct().Count() == data.Teams.Count && data.Competitions.Select(x => x.SourceId).Distinct().Count() == data.Competitions.Count,
            "The LIVOSUR dataset contains duplicated source identifiers.");
        Require(data.Teams.Select(x => (x.Name, x.Gender)).Distinct().Count() == data.Teams.Count,
            "The LIVOSUR dataset contains duplicated Team natural keys.");
        Require(data.TeamEntries.Select(x => (x.CompetitionSourceId, x.TeamSourceId)).Distinct().Count() == data.TeamEntries.Count,
            "The LIVOSUR dataset contains duplicated TeamEntry natural keys.");
        foreach (var team in data.Teams) Require(data.Clubs.Any(x => x.SourceId == team.ClubSourceId), $"Team source {team.SourceId} references an unknown club.");
        foreach (var competition in data.Competitions) { Require(data.Season.Any(x => x.SourceId == competition.SeasonSourceId), $"Competition source {competition.SourceId} references an unknown season."); Require(data.Divisions.Any(x => x.SourceId == competition.DivisionSourceId), $"Competition source {competition.SourceId} references an unknown division."); }
        foreach (var entry in data.TeamEntries) { Require(data.Competitions.Any(x => x.SourceId == entry.CompetitionSourceId), $"TeamEntry source {entry.SourceId} references an unknown competition."); Require(data.Teams.Any(x => x.SourceId == entry.TeamSourceId), $"TeamEntry source {entry.SourceId} references an unknown team."); }
    }

    private static Gender ParseGender(string value) => value switch { "Male" => Gender.Male, "Female" => Gender.Female, _ => throw new Livosur2026SeedException($"Unsupported gender '{value}'.") };
    private static CompetitionPeriodType ParsePeriod(string value) => value switch { "Clausura" => CompetitionPeriodType.Closing, _ => throw new Livosur2026SeedException($"Unsupported period type '{value}'.") };
    private static TeamEntryStatus ParseEntryStatus(string value) => value switch { "Registered" => TeamEntryStatus.Registered, "Active" => TeamEntryStatus.Active, "Withdrawn" => TeamEntryStatus.Withdrawn, "Disqualified" => TeamEntryStatus.Disqualified, _ => throw new Livosur2026SeedException($"Unsupported TeamEntry status '{value}'.") };
    private static void Require(bool condition, string message) { if (!condition) throw new Livosur2026SeedException(message); }
}

internal sealed record LivosurDataset(IReadOnlyList<SeedSeason> Season, IReadOnlyList<SeedDivision> Divisions, IReadOnlyList<SeedClub> Clubs,
    IReadOnlyList<SeedTeam> Teams, IReadOnlyList<SeedVenue> Venues, IReadOnlyList<SeedCompetition> Competitions, IReadOnlyList<SeedTeamEntry> TeamEntries);
internal sealed record SeedSeason(int SourceId, short Year, string Name, bool Active);
internal sealed record SeedDivision(int SourceId, string Name, short LevelOrder, string Gender, bool Active);
internal sealed record SeedClub(int SourceId, string Name, bool Active);
internal sealed record SeedTeam(int SourceId, int SourceTeamId, int ClubSourceId, string Name, string Gender, bool Active);
internal sealed record SeedVenue(int SourceId, string Name, bool Active);
internal sealed record SeedCompetition(int SourceId, string Name, int SeasonSourceId, int DivisionSourceId, string PeriodType);
internal sealed record SeedTeamEntry(int SourceId, int CompetitionSourceId, int TeamSourceId, string Status);
