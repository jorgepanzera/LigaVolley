using LigaVolley.Application.MatchSheets;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LigaVolley.IntegrationTests;

public sealed partial class MatchEngineEndpointsTests
{
    [Fact]
    public async Task Pure_replay_matches_the_persisted_projection_after_productive_operations()
    {
        var opened = await Open();
        await Prepare(opened.MatchId);
        await Lineup(opened.MatchId, 1, MatchSide.Home, opened.Home.Take(6).ToArray());
        await Lineup(opened.MatchId, 1, MatchSide.Away, opened.Away.Take(6).ToArray());
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{opened.MatchId}/sets/1/start", new StartSetRequest(MatchSide.Home));
        await Point(opened.MatchId, 1, MatchSide.Away);
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{opened.MatchId}/sets/1/substitutions", new AddSubstitutionRequest(Guid.NewGuid(), opened.Home[0], opened.Home[6]));
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{opened.MatchId}/sets/1/libero/enter", new LiberoEnterRequest(Guid.NewGuid(), opened.Home[7], opened.Home[4]));
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{opened.MatchId}/sets/1/timeouts", new AddTimeoutRequest(Guid.NewGuid(), MatchSide.Home));
        await Post<MatchEngineCommandResult>($"/api/scorer/matches/{opened.MatchId}/sets/1/sanctions", new RecordSanctionRequest(Guid.NewGuid(), MatchSide.Home, SanctionType.DelayPenalty, SanctionSubjectType.Player, opened.Home[1], null));
        var projected = await Post<MatchEngineCommandResult>($"/api/scorer/matches/{opened.MatchId}/sets/1/sanctions", new RecordSanctionRequest(Guid.NewGuid(), MatchSide.Home, SanctionType.Expulsion, SanctionSubjectType.Player, opened.Home[1], null));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
        var sheet = await db.MatchSheets.Include(x => x.Teams).ThenInclude(x => x.Players)
            .Include(x => x.Teams).ThenInclude(x => x.Liberos)
            .Include(x => x.Sets).ThenInclude(x => x.Lineups).ThenInclude(x => x.Positions)
            .Include(x => x.Events).SingleAsync(x => x.MatchId == opened.MatchId);
        var set = sheet.Sets.Single(x => x.SetNumber == 1);
        var home = sheet.Teams.Single(x => x.Side == MatchSide.Home); var away = sheet.Teams.Single(x => x.Side == MatchSide.Away);
        var baseState = new SetReplayBase(1, set.InitialServingSide!.Value, sheet.RulesSnapshot, sheet.TrackSubstitutions, sheet.TrackLiberoReplacements,
            Team(home, set), Team(away, set));
        var events = sheet.Events.Where(x => x.MatchSetId == set.MatchSetId && x.Status == MatchEventStatus.Active).OrderBy(x => x.SequenceNumber)
            .Select(x => { Assert.True(SetReplayEventReader.TryRead(x, out var replay)); return replay!; }).ToArray();
        var replayed = SetReplayEngine.Replay(baseState, events);

        Assert.Equal(projected.State.HomePoints, replayed.HomePoints); Assert.Equal(projected.State.AwayPoints, replayed.AwayPoints);
        Assert.Equal(projected.State.CurrentServingSide, replayed.ServingSide); Assert.Equal(projected.State.HomeRotationOffset, replayed.HomeRotationOffset);
        Assert.Equal(projected.State.AwayRotationOffset, replayed.AwayRotationOffset); Assert.Equal(projected.State.HomeTimeouts, replayed.Home.Timeouts);
        Assert.Equal(projected.State.HomeCourtState.Select(x => x.EffectiveMatchPlayerId), replayed.Home.Court.Select(x => x.EffectiveMatchPlayerId));
        Assert.Contains(opened.Home[1], replayed.SetIneligiblePlayers);
    }

    private static SetReplayTeamBase Team(MatchTeam team, Domain.Fixtures.MatchSet set) => new(team.Players.Select(x => x.MatchPlayerId).ToArray(),
        team.Liberos.Select(x => x.MatchPlayerId).ToArray(), set.Lineups.Single(x => x.MatchTeamId == team.MatchTeamId).Positions.OrderBy(x => x.Position).Select(x => x.MatchPlayerId).ToArray());
}
