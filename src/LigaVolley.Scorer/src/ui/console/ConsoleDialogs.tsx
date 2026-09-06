import { useEffect, useState } from 'react';
import type { LocalEvent, ServerSheetSnapshot, SetState, Side } from '../../domain/types';
import { effectivePlayers, regularPlayers, serverPlayer } from '../../domain/matchEngine';
import { player, shortName, team } from './model';
export function normalSubstitutionCandidates(
  snapshot: ServerSheetSnapshot,
  side: Side,
  set: SetState,
  logical: number,
) {
  const onCourt = new Set(regularPlayers(set, side));
  const liberos = new Set(team(snapshot, side)?.liberos.map((x) => x.matchPlayerId));
  const outgoing = regularPlayers(set, side)[logical];
  const starter = set.lineups[side][logical];
  const history = set.substitutions.filter((x) => x.side === side && x.position === logical);
  return (
    team(snapshot, side)?.players.filter(
      (x) =>
        !liberos.has(x.matchPlayerId) &&
        !onCourt.has(x.matchPlayerId) &&
        (outgoing === starter
          ? history.length === 0 &&
            !set.substitutions.some((entry) => entry.playerInMatchPlayerId === x.matchPlayerId)
          : history.length === 1 &&
            history[0].playerInMatchPlayerId === outgoing &&
            x.matchPlayerId === starter),
    ) ?? []
  );
}
export function canNormalSubstituteFromPosition(set: SetState, side: Side, logical: number) {
  return regularPlayers(set, side)[logical] !== undefined;
}
export function normalSubstitutionBlockReason(set: SetState, side: Side, logical: number) {
  return canNormalSubstituteFromPosition(set, side, logical)
    ? undefined
    : 'No se puede determinar el regular de esta plaza.';
}
export function PlayerActionSheet({
  side,
  logical,
  set,
  snapshot,
  trackSubstitutions,
  error,
  onClose,
  onSubstitute,
  onSubstitutionRequest,
  onLiberoEnter,
  onLiberoExit,
  onObservedServer,
}: {
  side: Side;
  logical: number;
  set: SetState;
  snapshot: ServerSheetSnapshot;
  trackSubstitutions: boolean;
  error?: string;
  onClose: () => void;
  onSubstitute: (outId: number, inId: number) => void;
  onSubstitutionRequest?: (
    pairs: Array<{ playerOutMatchPlayerId: number; playerInMatchPlayerId: number }>,
  ) => void;
  onLiberoEnter?: (libero: number, replaced: number) => void;
  onLiberoExit?: (libero: number) => void;
  onObservedServer?: (server: number, winner: Side) => void;
}) {
  useEscape(onClose);
  const [candidate, setCandidate] = useState<number>();
  const [exceptional, setExceptional] = useState(false);
  const [additional, setAdditional] = useState<
    Array<{ playerOutMatchPlayerId: number; playerInMatchPlayerId: number }>
  >([]);
  const regular = regularPlayers(set, side)[logical],
    effective = effectivePlayers(set, side)[logical],
    current = player(snapshot, side, effective),
    under = player(snapshot, side, regular),
    available = exceptional
      ? (team(snapshot, side)?.players.filter(
          (x) =>
            !effectivePlayers(set, side).includes(x.matchPlayerId) &&
            !regularPlayers(set, side).includes(x.matchPlayerId),
        ) ?? [])
      : normalSubstitutionCandidates(snapshot, side, set, logical),
    blockReason = normalSubstitutionBlockReason(set, side, logical);
  return (
    <div className="backdrop" onMouseDown={onClose}>
      <aside className="action-sheet" onMouseDown={(e) => e.stopPropagation()}>
        <button className="close" onClick={onClose}>
          ×
        </button>
        <small>
          {side} · PLAZA LÓGICA P{logical + 1}
        </small>
        <h2>
          #{current?.jerseyNumber} {current?.displayName}
        </h2>
        {error && (
          <p className="action-warning" role="alert">
            {error}
          </p>
        )}
        {effective !== regular && (
          <>
            <p>
              Líbero · reemplaza actualmente a #{under?.jerseyNumber}{' '}
              {shortName(under?.displayName)}
            </p>
            <p className="action-warning" role="status">
              {blockReason ??
                'La sustitución cambia al regular de esta plaza y conserva al líbero en cancha.'}
            </p>
          </>
        )}
        {snapshot.trackLiberoReplacements !== false && (
          <section className="libero-quick-actions" aria-label="Reemplazo observado de líbero">
            <h3>Reemplazo observado de líbero</h3>
            {effective !== regular && <button onClick={() => onLiberoExit?.(effective)}>Sale líbero / vuelve regular #{under?.jerseyNumber}</button>}
            {(!set.liberoReplacements.some(x => x.side === side && x.active) || effective !== regular) && team(snapshot, side)?.liberos
              .filter(x => !effectivePlayers(set, side).includes(x.matchPlayerId))
              .sort((a, b) => Number(b.matchPlayerId === set.liberoPlans[side]?.liberoMatchPlayerId) - Number(a.matchPlayerId === set.liberoPlans[side]?.liberoMatchPlayerId))
              .map(x => <button key={x.matchPlayerId} onClick={() => onLiberoEnter?.(x.matchPlayerId, effective)}>
                {effective !== regular ? 'Cambiar por segundo líbero' : 'Ingresar líbero'} #{player(snapshot, side, x.matchPlayerId)?.jerseyNumber} · sale #{current?.jerseyNumber}
              </button>)}
            <small>No consume sustituciones. Confirma sólo si el reemplazo ocurrió.</small>
          </section>
        )}
        {trackSubstitutions && canNormalSubstituteFromPosition(set, side, logical) && (
          <>
            <h3>Sustituir al regular #{under?.jerseyNumber}</h3>
            <button
              onClick={() => {
                setExceptional(!exceptional);
                setCandidate(undefined);
              }}
            >
              {exceptional ? 'Opciones habituales' : 'Otra opción por decisión del juez'}
            </button>
            <div className="selector-list">
              {available.map((p) => (
                <button
                  className={candidate === p.matchPlayerId ? 'selected' : ''}
                  key={p.matchPlayerId}
                  onClick={() => setCandidate(p.matchPlayerId)}
                >
                  <b>#{p.jerseyNumber}</b>
                  {p.displayName}
                </button>
              ))}
            </div>
            {available.length === 0 && (
              <p className="action-warning" role="status">
                No hay jugadores regulares disponibles para una sustitución normal.
              </p>
            )}
            {candidate && (
              <div className="substitution-confirm">
                <small>SALE</small>
                <b>
                  #{under?.jerseyNumber} {under?.displayName}
                </b>
                <span>→</span>
                <small>ENTRA</small>
                <b>
                  #{player(snapshot, side, candidate)?.jerseyNumber}{' '}
                  {player(snapshot, side, candidate)?.displayName}
                </b>
                {additional.map((pair, index) => (
                  <div key={index}>
                    <select
                      aria-label={`Sale en cambio ${index + 2}`}
                      value={pair.playerOutMatchPlayerId}
                      onChange={(e) =>
                        setAdditional(
                          additional.map((x, i) =>
                            i === index
                              ? { ...x, playerOutMatchPlayerId: Number(e.target.value) }
                              : x,
                          ),
                        )
                      }
                    >
                      <option value={0}>Sale...</option>
                      {regularPlayers(set, side)
                        .filter((id) => id !== regular)
                        .map((id) => (
                          <option key={id} value={id}>
                            #{player(snapshot, side, id)?.jerseyNumber}
                          </option>
                        ))}
                    </select>
                    <select
                      aria-label={`Entra en cambio ${index + 2}`}
                      value={pair.playerInMatchPlayerId}
                      onChange={(e) =>
                        setAdditional(
                          additional.map((x, i) =>
                            i === index
                              ? { ...x, playerInMatchPlayerId: Number(e.target.value) }
                              : x,
                          ),
                        )
                      }
                    >
                      <option value={0}>Entra...</option>
                      {team(snapshot, side)
                        ?.players.filter(
                          (x) =>
                            !effectivePlayers(set, side).includes(x.matchPlayerId) &&
                            !regularPlayers(set, side).includes(x.matchPlayerId),
                        )
                        .map((x) => (
                          <option key={x.matchPlayerId} value={x.matchPlayerId}>
                            #{x.jerseyNumber}
                          </option>
                        ))}
                    </select>
                    <button onClick={() => setAdditional(additional.filter((_, i) => i !== index))}>
                      Quitar
                    </button>
                  </div>
                ))}
                {onSubstitutionRequest && (
                  <button
                    onClick={() =>
                      setAdditional([
                        ...additional,
                        { playerOutMatchPlayerId: 0, playerInMatchPlayerId: 0 },
                      ])
                    }
                  >
                    + Agregar otro cambio
                  </button>
                )}
                <button
                  onClick={() =>
                    onSubstitutionRequest
                      ? onSubstitutionRequest([
                          { playerOutMatchPlayerId: regular, playerInMatchPlayerId: candidate },
                          ...additional,
                        ])
                      : onSubstitute(regular, candidate)
                  }
                >
                  Confirmar sustitución
                </button>
              </div>
            )}
          </>
        )}
        {set.servingSide === side && (
          <details>
            <summary>Servidor observado</summary>
            <p>Registrar este servidor observado para el siguiente punto.</p>
            {(['HOME', 'AWAY'] as Side[]).map((winner) => (
              <button key={winner} onClick={() => onObservedServer?.(effective, winner)}>
                Registrar punto {winner}
              </button>
            ))}
          </details>
        )}
        {!trackSubstitutions && <p>Sustituciones no registradas</p>}
      </aside>
    </div>
  );
}
export function ConfirmDialog({
  title,
  children,
  confirmLabel,
  onConfirm,
  onClose,
  danger = false,
}: {
  title: string;
  children: React.ReactNode;
  confirmLabel?: string;
  onConfirm?: () => void;
  onClose: () => void;
  danger?: boolean;
}) {
  useEscape(onClose);
  return (
    <div className="backdrop" onMouseDown={onClose}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <h2>{title}</h2>
        {children}
        {confirmLabel && onConfirm && (
          <footer>
            <button onClick={onClose}>Cancelar</button>
            <button className={danger ? 'danger' : ''} onClick={onConfirm}>
              {confirmLabel}
            </button>
          </footer>
        )}
      </div>
    </div>
  );
}
export function HistoryDrawer({ events, snapshot, onClose }: { events: LocalEvent[]; snapshot?: ServerSheetSnapshot; onClose: () => void }) {
  useEscape(onClose);
  const correctedPoint = events
    .map(
      (event) =>
        event.type === 'POINT' &&
        events.some(
          (candidate) =>
            candidate.sequence > event.sequence && candidate.type === 'CORRECT_LAST_POINT',
        ),
    )
    .lastIndexOf(true);
  return (
    <div className="drawer-backdrop" onMouseDown={onClose}>
      <aside className="history" onMouseDown={(e) => e.stopPropagation()}>
        <button className="close" onClick={onClose}>
          ×
        </button>
        <small>CONSULTA</small>
        <h2>Historial del partido</h2>
        {events.length === 0 ? (
          <p>Sin acciones todavía.</p>
        ) : (
          [...events].reverse().map((event) => (
            <article
              className={events.indexOf(event) === correctedPoint ? 'corrected' : ''}
              key={event.eventUuid}
            >
              <b>
                {eventLabel(event, snapshot)}
                {events.indexOf(event) === correctedPoint && <small> CORREGIDO</small>}
              </b>
              <time>
                {new Date(event.occurredAt).toLocaleTimeString([], {
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </time>
              {Array.isArray(event.payload.confirmedRuleWarnings) &&
                event.payload.confirmedRuleWarnings.length > 0 && (
                  <details>
                    <summary>Decisión confirmada por el juez</summary>
                    <p>{event.payload.confirmedRuleWarnings.join(', ')}</p>
                  </details>
                )}
            </article>
          ))
        )}
      </aside>
    </div>
  );
}
function eventLabel(event: LocalEvent, snapshot?: ServerSheetSnapshot) {
  if (event.type === 'LIBERO_ENTER' || event.type === 'LIBERO_EXIT') {
    const side = String(event.payload.side).toUpperCase() as Side;
    const label = (id: unknown) => { const p = snapshot && player(snapshot, side, Number(id)); return p ? `#${p.jerseyNumber} ${p.displayName}` : `jugador ${id}`; };
    return event.type === 'LIBERO_ENTER' ? `${side}: sale ${label(event.payload.replacedMatchPlayerId)} → entra líbero ${label(event.payload.liberoMatchPlayerId)}` : `${side}: sale líbero ${label(event.payload.liberoMatchPlayerId)} → vuelve regular vigente`;
  }
  const side = String(event.payload.winningSide ?? event.payload.side ?? '');
  return event.type === 'POINT'
    ? `Punto ${side}`
    : event.type === 'TIMEOUT'
      ? `Timeout ${side}`
      : event.type === 'SUBSTITUTION' || event.type === 'SUBSTITUTION_REQUEST'
        ? `Sustitución ${side}`
        : event.type === 'CORRECT_LAST_POINT'
          ? 'Último punto corregido'
          : event.type === 'START_SET'
            ? 'Set iniciado'
            : event.type === 'MATCH_CLOSE'
              ? 'Acta cerrada'
              : event.type.replaceAll('_', ' ');
}
export function MatchReview({
  set,
  snapshot,
  homeSets,
  awaySets,
  onClose,
  onCorrect,
  onHistory,
  onConfirmClose,
}: {
  set: SetState;
  snapshot: ServerSheetSnapshot;
  homeSets: number;
  awaySets: number;
  onClose: () => void;
  onCorrect: () => void;
  onHistory: () => void;
  onConfirmClose: () => void;
}) {
  useEscape(onClose);
  const winner = homeSets > awaySets ? 'HOME' : 'AWAY';
  return (
    <div className="backdrop">
      <section className="match-review">
        <button className="close" onClick={onClose}>
          ×
        </button>
        <small>REVISIÓN DEL PARTIDO</small>
        <h2>
          {team(snapshot, 'HOME')?.teamName} {homeSets} — {awaySets}{' '}
          {team(snapshot, 'AWAY')?.teamName}
        </h2>
        <p>
          Ganador: <b>{team(snapshot, winner)?.teamName}</b>
        </p>
        <div className="set-summary">
          {snapshot.operationalState?.sets.map((x) => (
            <span key={x.setNumber}>
              Set {x.setNumber} {x.homePoints}-{x.awayPoints}
            </span>
          )) ?? (
            <span>
              Set {set.setNumber} {set.homePoints}-{set.awayPoints}
            </span>
          )}
        </div>
        <p>
          Timeouts del último set: HOME {set.homeTimeouts}/
          {snapshot.rulesSnapshot?.maxTimeoutsPerSet ?? 2} · AWAY {set.awayTimeouts}/
          {snapshot.rulesSnapshot?.maxTimeoutsPerSet ?? 2}
        </p>
        <footer>
          <button onClick={onHistory}>Ver historial</button>
          <button onClick={onCorrect}>Corregir último punto</button>
          <button className="danger" onClick={onConfirmClose}>
            Cerrar partido
          </button>
        </footer>
      </section>
    </div>
  );
}

function useEscape(onClose: () => void) {
  useEffect(() => {
    const close = (event: KeyboardEvent) => event.key === 'Escape' && onClose();
    addEventListener('keydown', close);
    return () => removeEventListener('keydown', close);
  }, [onClose]);
}
export function CorrectPreview({
  set,
  snapshot,
  onClose,
  onConfirm,
}: {
  set: SetState;
  snapshot: ServerSheetSnapshot;
  onClose: () => void;
  onConfirm: () => void;
}) {
  const last = set.points.at(-1),
    home = set.homePoints - (last === 'HOME' ? 1 : 0),
    away = set.awayPoints - (last === 'AWAY' ? 1 : 0),
    server = set.servingSide
      ? player(snapshot, set.servingSide, serverPlayer(set, set.servingSide))
      : undefined;
  return (
    <ConfirmDialog
      title="CORREGIR ÚLTIMO PUNTO"
      confirmLabel="Corregir"
      onConfirm={onConfirm}
      onClose={onClose}
      danger
    >
      <p>
        Último punto: <b>{last} +1</b>
      </p>
      <p>
        Estado actual: {set.homePoints}-{set.awayPoints} · saque {set.servingSide} · #
        {server?.jerseyNumber} {shortName(server?.displayName)}
      </p>
      <p>
        La corrección volverá a:{' '}
        <b>
          {home}-{away}
        </b>{' '}
        y reconstruirá saque, rotación, servidor y líbero.
      </p>
    </ConfirmDialog>
  );
}
