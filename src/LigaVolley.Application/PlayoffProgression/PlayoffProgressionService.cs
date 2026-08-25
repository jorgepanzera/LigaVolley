using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.PhaseCompletion;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.PlayoffProgression;

public sealed class PlayoffProgressionService(IPlayoffProgressionRepository repository, IUnitOfWork unit)
{
    public Task<PlayoffProgressionResult> ProcessFinishedMatchAsync(int matchId, CancellationToken cancellationToken = default) =>
        repository.ExecuteExclusiveAsync(matchId, ct => ProcessLockedAsync(matchId, ct), cancellationToken);

    private async Task<PlayoffProgressionResult> ProcessLockedAsync(int matchId, CancellationToken ct)
    {
        var trigger = await repository.GetMatchAsync(matchId, ct)
            ?? throw Conflict("playoff_series_not_found", "The playoff match was not found.");
        if (trigger.SeriesId is null || trigger.Status != MatchStatus.Finished)
            throw Conflict("playoff_series_match_invalid", "The match must be FINISHED and belong to a playoff series.");

        var competition = await repository.GetCompetitionAsync(trigger.CompetitionId, ct)
            ?? throw Conflict("playoff_series_not_found", "The playoff series competition was not found.");
        var series = competition.Phases.SelectMany(x => x.Series).SingleOrDefault(x => x.PlayoffSeriesId == trigger.SeriesId)
            ?? throw Conflict("playoff_series_not_found", "The playoff series was not found in the match competition.");
        var entries = (await repository.ListTeamEntriesAsync(competition.CompetitionId, ct)).ToDictionary(x => x.TeamEntryId);
        var allMatches = await repository.ListSeriesMatchesAsync(competition.CompetitionId, ct);
        var generated = new List<Match>();
        var updated = new HashSet<CompetitionPlayoffSeries>();
        var finishedPhases = new List<int>();

        ValidateConfiguration(series);
        if (series.Status == PlayoffSeriesStatus.Cancelled)
            throw Conflict("playoff_series_status_invalid", "A cancelled playoff series cannot progress automatically.");
        if (series.Status == PlayoffSeriesStatus.Pending)
            throw Conflict("playoff_series_status_invalid", "A pending playoff series cannot have a finished real match.");
        if (!series.Team1EntryId.HasValue || !series.Team2EntryId.HasValue)
            throw Conflict("playoff_series_configuration_invalid", "A progressing playoff series requires both participants.");

        var sourceMatches = allMatches.Where(x => x.SeriesId == series.PlayoffSeriesId).OrderBy(x => x.MatchNumber).ToArray();
        ValidateMatches(series, sourceMatches);
        var finishedMatches = sourceMatches.Where(x => x.Status == MatchStatus.Finished).ToArray();
        var wins = PlayoffSeriesResultCalculator.Calculate(series.WinsRequired, series.Team1InitialWins, series.Team2InitialWins,
            series.Team1EntryId.Value, series.Team2EntryId.Value, finishedMatches.Select(x => x.WinnerTeamEntryId!.Value));
        var team1Wins = wins.Team1Wins;
        var team2Wins = wins.Team2Wins;
        var team1Won = wins.WinnerSide == 1;
        var team2Won = wins.WinnerSide == 2;

        Match? nextMatch = null;
        if (!team1Won && !team2Won)
        {
            if (series.Status == PlayoffSeriesStatus.Finished || series.WinnerTeamEntryId.HasValue)
                throw Conflict("playoff_series_result_inconsistent", "The persisted series winner contradicts its finished matches.");
            series.MarkInProgress();
            updated.Add(series);
            nextMatch = EnsureNextMatch(competition, series, sourceMatches, entries, generated);
        }
        else
        {
            var winnerId = team1Won ? series.Team1EntryId!.Value : series.Team2EntryId!.Value;
            var loserId = team1Won ? series.Team2EntryId!.Value : series.Team1EntryId!.Value;
            if (sourceMatches.Any(x => x.Status != MatchStatus.Finished))
                throw Conflict("playoff_series_result_inconsistent", "A decided series contains a future non-finished match.");
            if (series.Status == PlayoffSeriesStatus.Finished && series.WinnerTeamEntryId != winnerId)
                throw Conflict("playoff_series_result_inconsistent", "The persisted series winner is missing or contradicts its results.");
            series.Finish(entries[winnerId]);
            updated.Add(series);
            Propagate(competition, series, winnerId, loserId, entries, allMatches, generated, updated);
        }

        repository.AddMatches(generated);
        foreach (var phase in competition.Phases.Where(x => x.PhaseType == PhaseType.Playoff && x.Series.Count > 0 && x.Series.All(s => s.Status == PlayoffSeriesStatus.Finished)))
        {
            if (phase.Status != CompetitionPhaseStatus.Finished)
            {
                phase.FinishPlayoff();
                finishedPhases.Add(phase.CompetitionPhaseId);
            }
        }

        await unit.SaveChangesAsync(ct);
        return new(
            series.PlayoffSeriesId,
            series.Status,
            team1Wins,
            team2Wins,
            series.WinnerTeamEntryId,
            nextMatch?.MatchId,
            updated.OrderBy(x => x.PlayoffSeriesId).Select(ToResolved).ToArray(),
            generated.Select(x => x.MatchId).ToArray(),
            finishedPhases);
    }

    private static Match EnsureNextMatch(Competition competition, CompetitionPlayoffSeries series, IReadOnlyList<Match> matches,
        IReadOnlyDictionary<int, TeamEntry> entries, ICollection<Match> generated)
    {
        var unfinished = matches.Where(x => x.Status != MatchStatus.Finished && x.Status != MatchStatus.Cancelled).ToArray();
        if (unfinished.Length > 1)
            throw Conflict("playoff_series_match_conflict", "More than one future real match exists for the series.");
        if (unfinished.Length == 1)
            return unfinished[0];
        if (matches.Any(x => x.Status == MatchStatus.Cancelled))
            throw Conflict("playoff_series_status_invalid", "Cancelled playoff matches have no automatic sporting consequence.");

        var nextNumber = checked((short)(matches.Count + 1));
        var phase = competition.Phases.Single(x => x.CompetitionPhaseId == series.CompetitionPhaseId);
        var team1 = entries[series.Team1EntryId!.Value];
        var team2 = entries[series.Team2EntryId!.Value];
        var next = nextNumber % 2 == 1
            ? new Match(competition, phase, series, team1, team2, nextNumber)
            : new Match(competition, phase, series, team2, team1, nextNumber);
        generated.Add(next);
        return next;
    }

    private static void Propagate(Competition competition, CompetitionPlayoffSeries source, int winnerId, int loserId,
        IReadOnlyDictionary<int, TeamEntry> entries, IReadOnlyList<Match> matches, ICollection<Match> generated,
        ISet<CompetitionPlayoffSeries> updated)
    {
        foreach (var target in competition.Phases.SelectMany(x => x.Series))
        {
            foreach (var dependency in target.ParticipantSources.Where(x => x.SourcePlayoffSeriesId == source.PlayoffSeriesId))
            {
                var sourcePhase = competition.Phases.Single(x => x.CompetitionPhaseId == source.CompetitionPhaseId);
                var targetPhase = competition.Phases.Single(x => x.CompetitionPhaseId == target.CompetitionPhaseId);
                if (target.CompetitionId != source.CompetitionId || targetPhase.Sequence <= sourcePhase.Sequence)
                    throw Conflict("playoff_series_dependency_invalid", "The target series dependency is outside the valid competition progression.");
                if (target.Status == PlayoffSeriesStatus.Cancelled)
                    throw Conflict("playoff_series_status_invalid", "A cancelled target series cannot receive automatic participants.");
                var participantId = dependency.SourceType == SeriesParticipantSourceType.SeriesWinner ? winnerId : loserId;
                var current = dependency.TargetSide == 1 ? target.Team1EntryId : target.Team2EntryId;
                if (current.HasValue && current != participantId)
                    throw Conflict("playoff_series_participant_conflict", "A target series side is occupied by another participant.");
                try { target.AssignParticipant(dependency.TargetSide, entries[participantId]); }
                catch (Exception ex) { throw Conflict("playoff_series_participant_conflict", ex.Message); }
                updated.Add(target);
            }

            if (target.Status == PlayoffSeriesStatus.Ready && updated.Contains(target))
            {
                var targetMatches = matches.Where(x => x.SeriesId == target.PlayoffSeriesId).OrderBy(x => x.MatchNumber).ToArray();
                ValidateMatches(target, targetMatches);
                var first = EnsureNextMatch(competition, target, targetMatches, entries, generated);
                if (first.MatchNumber != 1)
                    throw Conflict("playoff_series_match_conflict", "A newly ready series must start with Match #1.");
            }
        }
    }

    private static void ValidateConfiguration(CompetitionPlayoffSeries series)
    {
        if (series.WinsRequired <= 0 || series.Team1InitialWins < 0 || series.Team2InitialWins < 0 ||
            series.Team1InitialWins >= series.WinsRequired || series.Team2InitialWins >= series.WinsRequired)
            throw Conflict("playoff_series_configuration_invalid", "The playoff series wins configuration is invalid.");
    }

    private static void ValidateMatches(CompetitionPlayoffSeries series, IReadOnlyList<Match> matches)
    {
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var expectedNumber = index + 1;
            var expectedHome = expectedNumber % 2 == 1 ? series.Team1EntryId : series.Team2EntryId;
            var expectedAway = expectedNumber % 2 == 1 ? series.Team2EntryId : series.Team1EntryId;
            if (match.MatchNumber != expectedNumber || match.HomeTeamEntryId != expectedHome || match.AwayTeamEntryId != expectedAway)
                throw Conflict("playoff_series_match_conflict", "Series match numbering or participants contradict the series.");
            if (match.Status == MatchStatus.Finished && (!match.WinnerTeamEntryId.HasValue ||
                match.WinnerTeamEntryId != match.HomeTeamEntryId && match.WinnerTeamEntryId != match.AwayTeamEntryId))
                throw Conflict("playoff_series_match_invalid", "A FINISHED series match must have a valid winner.");
        }
    }

    private static ResolvedSeriesDto ToResolved(CompetitionPlayoffSeries series) =>
        new(series.PlayoffSeriesId, series.Code, series.Team1EntryId, series.Team2EntryId, series.Status);

    private static ResourceConflictException Conflict(string code, string message) => new(code, message);
}
