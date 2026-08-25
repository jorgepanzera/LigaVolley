using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Domain.Standings;

public sealed record StandingsTeam(int TeamEntryId, int TeamId, string TeamName);
public sealed record StandingsSet(byte SetNumber, short HomePoints, short AwayPoints);
public sealed record StandingsMatch(int MatchId, int HomeTeamEntryId, int AwayTeamEntryId, byte? HomeSets, byte? AwaySets, int? WinnerTeamEntryId, IReadOnlyList<StandingsSet> Sets);
public sealed record StandingsScoringRule(byte WinnerSets, byte LoserSets, short WinnerTablePoints, short LoserTablePoints);
public sealed record StandingsTiebreakRule(short Sequence, TiebreakCriterion Criterion, SortDirection SortDirection);
public sealed record StandingPosition(int Position, int TeamEntryId, int TeamId, string TeamName, int Played, int Won, int Lost, int SetsWon, int SetsLost, decimal? SetRatio, int PointsWon, int PointsLost, decimal? PointRatio, int TablePoints, bool IsTied);

public sealed class StandingsCalculationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class StandingsCalculator
{
    public IReadOnlyList<StandingPosition> Calculate(IReadOnlyList<StandingsTeam> teams, IReadOnlyList<StandingsMatch> matches,
        IReadOnlyList<StandingsScoringRule> scoringRules, IReadOnlyList<StandingsTiebreakRule> tiebreakRules)
    {
        ValidateTiebreaks(tiebreakRules);
        var stats = teams.ToDictionary(x => x.TeamEntryId, x => new MutableStanding(x));
        var headToHead = new Dictionary<(int Winner, int Loser), int>();
        var scoring = scoringRules.GroupBy(x => (x.WinnerSets, x.LoserSets)).ToDictionary(x => x.Key, x => x.ToArray());

        foreach (var match in matches)
        {
            ValidateMatch(match, stats);
            var homeWon = match.WinnerTeamEntryId == match.HomeTeamEntryId;
            var winnerSets = homeWon ? match.HomeSets!.Value : match.AwaySets!.Value;
            var loserSets = homeWon ? match.AwaySets!.Value : match.HomeSets!.Value;
            if (!scoring.TryGetValue((winnerSets, loserSets), out var candidates) || candidates.Length != 1)
                throw new StandingsCalculationException("standings_scoring_rule_missing", $"Match '{match.MatchId}' score {winnerSets}-{loserSets} has no unique scoring rule.");

            var home = stats[match.HomeTeamEntryId]; var away = stats[match.AwayTeamEntryId];
            home.Played++; away.Played++;
            home.SetsWon += match.HomeSets!.Value; home.SetsLost += match.AwaySets!.Value;
            away.SetsWon += match.AwaySets!.Value; away.SetsLost += match.HomeSets!.Value;
            home.PointsWon += match.Sets.Sum(x => x.HomePoints); home.PointsLost += match.Sets.Sum(x => x.AwayPoints);
            away.PointsWon += match.Sets.Sum(x => x.AwayPoints); away.PointsLost += match.Sets.Sum(x => x.HomePoints);
            var winner = homeWon ? home : away; var loser = homeWon ? away : home;
            winner.Won++; loser.Lost++; winner.TablePoints += candidates[0].WinnerTablePoints; loser.TablePoints += candidates[0].LoserTablePoints;
            headToHead[(winner.Team.TeamEntryId, loser.Team.TeamEntryId)] = headToHead.GetValueOrDefault((winner.Team.TeamEntryId, loser.Team.TeamEntryId)) + 1;
        }

        var groups = new List<List<MutableStanding>> { stats.Values.ToList() };
        foreach (var rule in tiebreakRules.OrderBy(x => x.Sequence))
            groups = groups.SelectMany(group => ApplyRule(group, rule, headToHead)).ToList();

        var result = new List<StandingPosition>(); var nextPosition = 1;
        foreach (var group in groups)
        {
            foreach (var row in group.OrderBy(x => x.Team.TeamEntryId))
                result.Add(row.ToPosition(nextPosition, group.Count > 1));
            nextPosition += group.Count;
        }
        return result;
    }

    private static IEnumerable<List<MutableStanding>> ApplyRule(List<MutableStanding> group, StandingsTiebreakRule rule, Dictionary<(int Winner, int Loser), int> headToHead)
    {
        if (group.Count <= 1) return [group];
        if (rule.Criterion == TiebreakCriterion.HeadToHead && group.Count != 2) return [group];
        int Compare(MutableStanding a, MutableStanding b)
        {
            var value = rule.Criterion switch
            {
                TiebreakCriterion.TablePoints => a.TablePoints.CompareTo(b.TablePoints),
                TiebreakCriterion.MatchWins => a.Won.CompareTo(b.Won),
                TiebreakCriterion.SetRatio => CompareRatio(a.SetsWon, a.SetsLost, b.SetsWon, b.SetsLost),
                TiebreakCriterion.PointRatio => CompareRatio(a.PointsWon, a.PointsLost, b.PointsWon, b.PointsLost),
                TiebreakCriterion.HeadToHead => headToHead.GetValueOrDefault((a.Team.TeamEntryId, b.Team.TeamEntryId)).CompareTo(headToHead.GetValueOrDefault((b.Team.TeamEntryId, a.Team.TeamEntryId))),
                _ => throw InvalidTiebreak()
            };
            return rule.SortDirection == SortDirection.Desc ? -value : value;
        }
        var ordered = group.OrderBy(x => x, Comparer<MutableStanding>.Create(Compare)).ToList();
        var partitions = new List<List<MutableStanding>>();
        foreach (var row in ordered)
        {
            if (partitions.Count == 0 || Compare(partitions[^1][0], row) != 0) partitions.Add([]);
            partitions[^1].Add(row);
        }
        return partitions;
    }

    private static int CompareRatio(int an, int ad, int bn, int bd)
    {
        if (ad == 0 || bd == 0) return ad == 0 && bd == 0 ? 0 : ad == 0 ? 1 : -1;
        return ((long)an * bd).CompareTo((long)bn * ad);
    }

    private static void ValidateMatch(StandingsMatch match, Dictionary<int, MutableStanding> stats)
    {
        if (!stats.ContainsKey(match.HomeTeamEntryId) || !stats.ContainsKey(match.AwayTeamEntryId) || match.HomeTeamEntryId == match.AwayTeamEntryId ||
            match.HomeSets is null || match.AwaySets is null || match.WinnerTeamEntryId is null ||
            match.WinnerTeamEntryId != match.HomeTeamEntryId && match.WinnerTeamEntryId != match.AwayTeamEntryId ||
            match.Sets.Count != match.HomeSets + match.AwaySets || match.Sets.Select(x => x.SetNumber).Distinct().Count() != match.Sets.Count ||
            match.Sets.Any(x => x.HomePoints == x.AwayPoints) || match.Sets.Count(x => x.HomePoints > x.AwayPoints) != match.HomeSets ||
            match.Sets.Count(x => x.AwayPoints > x.HomePoints) != match.AwaySets ||
            (match.HomeSets > match.AwaySets ? match.HomeTeamEntryId : match.AwayTeamEntryId) != match.WinnerTeamEntryId)
            throw new StandingsCalculationException("standings_match_result_invalid", $"Match '{match.MatchId}' has an inconsistent result.");
    }

    private static void ValidateTiebreaks(IReadOnlyList<StandingsTiebreakRule> rules)
    {
        if (rules.Any(x => x.Sequence <= 0 || !Enum.IsDefined(x.Criterion) || !Enum.IsDefined(x.SortDirection)) || rules.GroupBy(x => x.Sequence).Any(x => x.Count() > 1))
            throw InvalidTiebreak();
    }
    private static StandingsCalculationException InvalidTiebreak() => new("standings_tiebreak_configuration_invalid", "Tiebreak rule configuration is invalid.");

    private sealed class MutableStanding(StandingsTeam team)
    {
        public StandingsTeam Team { get; } = team;
        public int Played, Won, Lost, SetsWon, SetsLost, PointsWon, PointsLost, TablePoints;
        public StandingPosition ToPosition(int position, bool tied) => new(position, Team.TeamEntryId, Team.TeamId, Team.TeamName, Played, Won, Lost, SetsWon, SetsLost,
            SetsLost == 0 ? null : (decimal)SetsWon / SetsLost, PointsWon, PointsLost, PointsLost == 0 ? null : (decimal)PointsWon / PointsLost, TablePoints, tied);
    }
}
