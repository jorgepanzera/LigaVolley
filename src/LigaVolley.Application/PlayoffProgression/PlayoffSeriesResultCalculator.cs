using LigaVolley.Application.Common;

namespace LigaVolley.Application.PlayoffProgression;

public sealed record PlayoffSeriesWins(int Team1Wins, int Team2Wins, byte? WinnerSide);

public static class PlayoffSeriesResultCalculator
{
    public static PlayoffSeriesWins Calculate(short winsRequired, short team1InitialWins, short team2InitialWins,
        int team1EntryId, int team2EntryId, IEnumerable<int> realMatchWinnerIds)
    {
        if (winsRequired <= 0 || team1InitialWins < 0 || team2InitialWins < 0 ||
            team1InitialWins >= winsRequired || team2InitialWins >= winsRequired)
            throw new ResourceConflictException("playoff_series_configuration_invalid", "The playoff series wins configuration is invalid.");

        var winners = realMatchWinnerIds.ToArray();
        if (winners.Any(x => x != team1EntryId && x != team2EntryId))
            throw new ResourceConflictException("playoff_series_match_invalid", "A real match winner is not a series participant.");

        var team1Wins = team1InitialWins + winners.Count(x => x == team1EntryId);
        var team2Wins = team2InitialWins + winners.Count(x => x == team2EntryId);
        var team1Won = team1Wins >= winsRequired;
        var team2Won = team2Wins >= winsRequired;
        if (team1Won && team2Won)
            throw new ResourceConflictException("playoff_series_result_inconsistent", "Both participants reached winsRequired.");

        return new(team1Wins, team2Wins, team1Won ? (byte)1 : team2Won ? (byte)2 : null);
    }
}
