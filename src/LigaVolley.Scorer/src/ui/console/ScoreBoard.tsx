import type { SetState, Side } from '../../domain/types';
import { serverPlayer } from '../../domain/matchEngine';
import { player, shortName, team } from './model';
import type { ServerSheetSnapshot } from '../../domain/types';
export function ScoreBoard({
  set,
  homeSets,
  awaySets,
  snapshot,
}: {
  set?: SetState;
  homeSets: number;
  awaySets: number;
  snapshot?: ServerSheetSnapshot;
}) {
  return (
    <section className="scoreboard">
      <TeamScore
        side="HOME"
        points={set?.homePoints ?? 0}
        serving={set?.servingSide === 'HOME'}
        set={set}
        snapshot={snapshot}
      />
      <div className="sets">
        <small>SET {set?.setNumber ?? '—'}</small>
        <strong>
          {homeSets}
          <span>—</span>
          {awaySets}
        </strong>
        <span>SETS</span>
      </div>
      <TeamScore
        side="AWAY"
        points={set?.awayPoints ?? 0}
        serving={set?.servingSide === 'AWAY'}
        set={set}
        snapshot={snapshot}
      />
    </section>
  );
}
function TeamScore({
  side,
  points,
  serving,
  set,
  snapshot,
}: {
  side: Side;
  points: number;
  serving: boolean;
  set?: SetState;
  snapshot?: ServerSheetSnapshot;
}) {
  const server = set && serving ? player(snapshot, side, serverPlayer(set, side)) : undefined;
  return (
    <article className={`team-score ${side.toLowerCase()} ${serving ? 'serving' : ''}`}>
      <small>{team(snapshot, side)?.teamName ?? side}</small>
      <strong>{points}</strong>
      <div className="serve-line">
        {serving ? (
          <>
            <b>● SAQUE</b>
            <span>
              #{server?.jerseyNumber ?? '—'} {shortName(server?.displayName)} · P1
            </span>
          </>
        ) : (
          <span>RECIBE</span>
        )}
      </div>
    </article>
  );
}
