import { useEffect, useState } from 'react';
import type { LocalEvent, ServerSheetSnapshot, SetState, Side } from '../../domain/types';
import { effectivePlayers, regularPlayers, serverPlayer } from '../../domain/matchEngine';
import { player, shortName, team } from './model';
export function normalSubstitutionCandidates(
  snapshot: ServerSheetSnapshot,
  side: Side,
  set: SetState,
) {
  const onCourt = new Set(regularPlayers(set, side));
  const liberos = new Set(team(snapshot, side)?.liberos.map((x) => x.matchPlayerId));
  return (
    team(snapshot, side)?.players.filter(
      (x) => !liberos.has(x.matchPlayerId) && !onCourt.has(x.matchPlayerId),
    ) ?? []
  );
}
export function canNormalSubstituteFromPosition(set: SetState, side: Side, logical: number) {
  return effectivePlayers(set, side)[logical] === regularPlayers(set, side)[logical];
}
export function normalSubstitutionBlockReason(set: SetState, side: Side, logical: number) {
  return canNormalSubstituteFromPosition(set, side, logical)
    ? undefined
    : 'El jugador en cancha es un líbero. Los líberos no pueden participar en sustituciones normales.';
}
export function PlayerActionSheet({
  side,
  logical,
  set,
  snapshot,
  trackSubstitutions,
  onClose,
  onSubstitute,
}: {
  side: Side;
  logical: number;
  set: SetState;
  snapshot: ServerSheetSnapshot;
  trackSubstitutions: boolean;
  onClose: () => void;
  onSubstitute: (outId: number, inId: number) => void;
}) {
  useEscape(onClose);
  const [candidate, setCandidate] = useState<number>();
  const regular = regularPlayers(set, side)[logical],
    effective = effectivePlayers(set, side)[logical],
    current = player(snapshot, side, effective),
    under = player(snapshot, side, regular),
    available = normalSubstitutionCandidates(snapshot, side, set),
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
        {effective !== regular && (
          <>
            <p>
              Líbero · reemplaza actualmente a #{under?.jerseyNumber}{' '}
              {shortName(under?.displayName)}
            </p>
            <p className="action-warning" role="status">
              {blockReason}
            </p>
          </>
        )}
        {trackSubstitutions && canNormalSubstituteFromPosition(set, side, logical) && (
          <>
            <h3>Sustituir</h3>
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
                <button onClick={() => onSubstitute(regular, candidate)}>
                  Confirmar sustitución
                </button>
              </div>
            )}
          </>
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
export function HistoryDrawer({ events, onClose }: { events: LocalEvent[]; onClose: () => void }) {
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
                {eventLabel(event)}
                {events.indexOf(event) === correctedPoint && <small> CORREGIDO</small>}
              </b>
              <time>
                {new Date(event.occurredAt).toLocaleTimeString([], {
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </time>
            </article>
          ))
        )}
      </aside>
    </div>
  );
}
function eventLabel(event: LocalEvent) {
  const side = String(event.payload.winningSide ?? event.payload.side ?? '');
  return event.type === 'POINT'
    ? `Punto ${side}`
    : event.type === 'TIMEOUT'
      ? `Timeout ${side}`
      : event.type === 'SUBSTITUTION'
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
          Timeouts del último set: HOME {set.homeTimeouts}/2 · AWAY {set.awayTimeouts}/2
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
