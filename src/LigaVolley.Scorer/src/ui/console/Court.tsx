import { effectivePlayers, logicalAtPhysical, regularPlayers } from '../../domain/matchEngine';
import type { ServerSheetSnapshot, SetState, Side } from '../../domain/types';
import { player, shortName, team } from './model';
import { VolleyballIcon } from './VolleyballIcon';
const layout: { HOME: number[]; AWAY: number[] } = {
  HOME: [4, 3, 2, 5, 6, 1],
  AWAY: [2, 3, 4, 1, 6, 5],
};
export function Court({
  set,
  snapshot,
  onPosition,
}: {
  set: SetState;
  snapshot?: ServerSheetSnapshot;
  onPosition: (side: Side, logical: number) => void;
}) {
  return (
    <section className="court">
      <TeamCourt side="HOME" set={set} snapshot={snapshot} onPosition={onPosition} />
      <div className="net">
        <span>RED</span>
      </div>
      <TeamCourt side="AWAY" set={set} snapshot={snapshot} onPosition={onPosition} />
    </section>
  );
}
function TeamCourt({
  side,
  set,
  snapshot,
  onPosition,
}: {
  side: Side;
  set: SetState;
  snapshot?: ServerSheetSnapshot;
  onPosition: (side: Side, logical: number) => void;
}) {
  const rotation = side === 'HOME' ? set.homeRotationOffset : set.awayRotationOffset,
    effective = effectivePlayers(set, side),
    regular = regularPlayers(set, side);
  return (
    <div className={`team-court ${side.toLowerCase()}`}>
      <header>
        <b>{team(snapshot, side)?.teamName ?? side}</b>
        <span>{set.lineups[side].length}/6</span>
      </header>
      <div className="positions">
        {layout[side].map((physical) => {
          const logical = logicalAtPhysical(physical, rotation),
            shown = player(snapshot, side, effective[logical]),
            under = player(snapshot, side, regular[logical]),
            isLibero = effective[logical] !== regular[logical],
            sub = set.substitutions.filter((x) => x.side === side && x.position === logical).at(-1),
            serving = set.servingSide === side && physical === 1;
          return (
            <button
              key={physical}
              className={`court-position ${isLibero ? 'libero' : ''} ${sub ? 'substitute' : ''} ${serving ? 'server' : ''}`}
              onClick={() => onPosition(side, logical)}
            >
              <span className="position-label">
                P{physical}
                {serving && (
                  <b className="serve-badge">
                    <VolleyballIcon /> SAQUE
                  </b>
                )}
              </span>
              <strong>
                {isLibero ? 'L ' : ''}#{shown?.jerseyNumber ?? '—'}
              </strong>
              <span>{shortName(shown?.displayName)}</span>
              {isLibero && <small>↳ #{under?.jerseyNumber ?? '—'}</small>}
              {!isLibero && sub && (
                <small>
                  ↔ #{player(snapshot, side, sub.playerOutMatchPlayerId)?.jerseyNumber ?? '—'}
                </small>
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
