import { useEffect, useState } from 'react';
import type { LiberoPlan, ServerSheetSnapshot, SetState, Side } from '../../domain/types';
import { team } from './model';
export function SetPreparation({
  set,
  previous,
  snapshot,
  trackLibero,
  onSave,
  onStart,
}: {
  set: SetState;
  previous?: SetState;
  snapshot: ServerSheetSnapshot;
  trackLibero: boolean;
  onSave: (side: Side, players: number[], plan: LiberoPlan) => Promise<void>;
  onStart: (side: Side) => Promise<void>;
}) {
  const [drafts, setDrafts] = useState(() => ({
      HOME: [...set.lineups.HOME],
      AWAY: [...set.lineups.AWAY],
    })),
    [plans, setPlans] = useState(() => structuredClone(set.liberoPlans)),
    [editing, setEditing] = useState<{ side: Side; position: number }>({
      side: 'HOME',
      position: 0,
    }),
    [serving, setServing] = useState<Side>(),
    [busy, setBusy] = useState(false),
    [error, setError] = useState('');
  useEffect(() => {
    setDrafts({ HOME: [...set.lineups.HOME], AWAY: [...set.lineups.AWAY] });
    setPlans(structuredClone(set.liberoPlans));
    setServing(undefined);
    setError('');
  }, [set.setNumber]);
  const select = (id: number) => {
    const next = { ...drafts, [editing.side]: [...drafts[editing.side]] };
    next[editing.side][editing.position] = id;
    setDrafts(next);
    if (editing.position < 5) setEditing({ ...editing, position: editing.position + 1 });
  };
  const save = async (side: Side) => {
    setError('');
    try {
      await onSave(side, drafts[side], plans[side]);
    } catch (cause) {
      setError(commandError(cause));
    }
  };
  const start = async () => {
    if (!serving) return;
    setBusy(true);
    setError('');
    try {
      for (const side of ['HOME', 'AWAY'] as Side[])
        if (!sameLineupConfiguration(set, side, drafts[side], plans[side]))
          await onSave(side, drafts[side], plans[side]);
      await onStart(serving);
    } catch (cause) {
      setError(commandError(cause));
    } finally {
      setBusy(false);
    }
  };
  const complete =
    drafts.HOME.length === 6 &&
    drafts.AWAY.length === 6 &&
    new Set(drafts.HOME).size === 6 &&
    new Set(drafts.AWAY).size === 6;
  const persistedComplete = set.lineups.HOME.length === 6 && set.lineups.AWAY.length === 6;
  const missingDrafts = (['HOME', 'AWAY'] as Side[]).filter(
    (side) => drafts[side].length !== 6 || new Set(drafts[side]).size !== 6,
  );
  const missingSaved = (['HOME', 'AWAY'] as Side[]).filter(
    (side) => set.lineups[side].length !== 6,
  );
  return (
    <section className="preparation">
      <header>
        <div>
          <small>PREPARACIÓN</small>
          <h2>Set {set.setNumber} · Alineaciones iniciales</h2>
        </div>
        <div className="prep-count">
          HOME {drafts.HOME.length}/6 · AWAY {drafts.AWAY.length}/6
        </div>
      </header>
      <div className="prep-grid">
        {(['HOME', 'AWAY'] as Side[]).map((side) => (
          <article key={side}>
            <h3>{team(snapshot, side)?.teamName}</h3>
            <div className="lineup-slots">
              {Array.from({ length: 6 }, (_, i) => (
                <button
                  className={editing.side === side && editing.position === i ? 'active' : ''}
                  onClick={() => setEditing({ side, position: i })}
                  key={i}
                >
                  <small>P{i + 1}</small>
                  <b>
                    #
                    {team(snapshot, side)?.players.find((x) => x.matchPlayerId === drafts[side][i])
                      ?.jerseyNumber ?? '—'}
                  </b>
                </button>
              ))}
            </div>
            <div className="prep-tools">
              {previous && (
                <button
                  onClick={() => setDrafts((x) => ({ ...x, [side]: [...previous.lineups[side]] }))}
                >
                  Copiar Set {previous.setNumber}
                </button>
              )}
              <button
                onClick={() =>
                  setDrafts((x) => ({ ...x, [side]: [x[side][5], ...x[side].slice(0, 5)] }))
                }
              >
                Rotar ↻
              </button>
            </div>
            <PlayerGrid
              snapshot={snapshot}
              side={side}
              occupied={drafts[side]}
              onSelect={select}
              active={editing.side === side}
            />
            {trackLibero && (
              <LiberoConfiguration
                snapshot={snapshot}
                side={side}
                lineup={drafts[side]}
                plan={plans[side]}
                onChange={(plan) => setPlans((x) => ({ ...x, [side]: plan }))}
              />
            )}
            <button
              className="save-lineup"
              disabled={drafts[side].length !== 6}
              onClick={() => void save(side)}
            >
              Guardar {side}
            </button>
          </article>
        ))}
      </div>
      <footer>
        {!complete ? (
          <p>Completa seis jugadores distintos para: {missingDrafts.join(' y ')}.</p>
        ) : !persistedComplete ? (
          <p>Se guardará automáticamente al iniciar: {missingSaved.join(' y ')}.</p>
        ) : null}
          {error && <p className="prep-error">{error}</p>}
          <h3>¿Quién comienza sacando?</h3>
          <div>
            <button
              className={serving === 'HOME' ? 'selected' : ''}
              onClick={() => setServing('HOME')}
            >
              HOME SACA
            </button>
            <button
              className={serving === 'AWAY' ? 'selected' : ''}
              onClick={() => setServing('AWAY')}
            >
              AWAY SACA
            </button>
          </div>
          <button
            className="start-set"
            disabled={!serving || !complete || busy}
            onClick={() => void start()}
          >
            {busy ? 'Guardando…' : `Iniciar Set ${set.setNumber}`}
          </button>
      </footer>
    </section>
  );
}
function PlayerGrid({
  snapshot,
  side,
  occupied,
  onSelect,
  active,
}: {
  snapshot: ServerSheetSnapshot;
  side: Side;
  occupied: number[];
  onSelect: (id: number) => void;
  active: boolean;
}) {
  const liberoIds = new Set(team(snapshot, side)?.liberos.map((x) => x.matchPlayerId));
  return (
    <div className="player-grid">
      {team(snapshot, side)
        ?.players.filter((x) => !liberoIds.has(x.matchPlayerId))
        .map((p) => (
          <button
            disabled={!active}
            className={occupied.includes(p.matchPlayerId) ? 'occupied' : ''}
            onClick={() => onSelect(p.matchPlayerId)}
            key={p.matchPlayerId}
          >
            <b>#{p.jerseyNumber ?? '—'}</b>
            <span>{p.displayName}</span>
            {occupied.includes(p.matchPlayerId) && (
              <small>P{occupied.indexOf(p.matchPlayerId) + 1}</small>
            )}
          </button>
        ))}
    </div>
  );
}
function LiberoConfiguration({
  snapshot,
  side,
  lineup,
  plan,
  onChange,
}: {
  snapshot: ServerSheetSnapshot;
  side: Side;
  lineup: number[];
  plan: LiberoPlan;
  onChange: (p: LiberoPlan) => void;
}) {
  const liberos = team(snapshot, side)?.liberos ?? [];
  return (
    <div className="libero-config">
      <label>
        Líbero del set
        <select
          value={plan.liberoMatchPlayerId ?? ''}
          onChange={(e) =>
            onChange(
              e.target.value
                ? { ...plan, enabled: true, liberoMatchPlayerId: Number(e.target.value) }
                : { enabled: false, logicalPositions: [] },
            )
          }
        >
          <option value="">Ninguno</option>
          {liberos.map((l) => (
            <option key={l.matchPlayerId} value={l.matchPlayerId}>
              #
              {
                team(snapshot, side)?.players.find((x) => x.matchPlayerId === l.matchPlayerId)
                  ?.jerseyNumber
              }
            </option>
          ))}
        </select>
      </label>
      {plan.enabled ? (
        <div>
          <span>Plazas cubiertas</span>
          {lineup.map((_, i) => (
            <button
              className={plan.logicalPositions.includes(i) ? 'selected' : ''}
              onClick={() =>
                onChange({
                  ...plan,
                  logicalPositions: plan.logicalPositions.includes(i)
                    ? plan.logicalPositions.filter((x) => x !== i)
                    : [...plan.logicalPositions, i],
                })
              }
              key={i}
            >
              P{i + 1}
            </button>
          ))}
        </div>
      ) : (
        <div>
          <span>Plazas cubiertas</span>
          <small>No aplica sin líbero.</small>
        </div>
      )}
    </div>
  );
}

function sameLineupConfiguration(
  set: SetState,
  side: Side,
  lineup: number[],
  plan: LiberoPlan,
) {
  const savedPlan = set.liberoPlans[side];
  return (
    set.lineups[side].length === lineup.length &&
    set.lineups[side].every((id, index) => id === lineup[index]) &&
    savedPlan.enabled === plan.enabled &&
    savedPlan.liberoMatchPlayerId === plan.liberoMatchPlayerId &&
    savedPlan.logicalPositions.length === plan.logicalPositions.length &&
    savedPlan.logicalPositions.every((position, index) => position === plan.logicalPositions[index])
  );
}

function commandError(cause: unknown) {
  if (cause instanceof Error && cause.message === 'ambiguous_libero_plan')
    return 'El plan de líbero puede requerir dos reemplazos simultáneos. Revisa las plazas cubiertas.';
  if (cause instanceof Error && cause.message === 'invalid_libero_plan')
    return 'Selecciona un líbero y al menos una plaza válida, o elige Ninguno.';
  return 'No se pudo guardar e iniciar el set. Revisa las alineaciones e inténtalo nuevamente.';
}
