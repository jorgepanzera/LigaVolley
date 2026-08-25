import type { LocalEvent, ServerSheetSnapshot, SetState, Side } from '../../domain/types';
import { effectivePlayers, regularPlayers, serverPlayer } from '../../domain/matchEngine';
import { player, shortName, team } from './model';
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
  const regular = regularPlayers(set, side)[logical],
    effective = effectivePlayers(set, side)[logical],
    current = player(snapshot, side, effective),
    under = player(snapshot, side, regular),
    onCourt = new Set(regularPlayers(set, side)),
    liberos = new Set(team(snapshot, side)?.liberos.map((x) => x.matchPlayerId)),
    available =
      team(snapshot, side)?.players.filter(
        (x) => !liberos.has(x.matchPlayerId) && !onCourt.has(x.matchPlayerId),
      ) ?? [];
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
          <p>
            Líbero · reemplaza actualmente a #{under?.jerseyNumber} {shortName(under?.displayName)}
          </p>
        )}
        {trackSubstitutions && effective === regular && (
          <>
            <h3>Sustituir</h3>
            <div className="selector-list">
              {available.map((p) => (
                <button
                  key={p.matchPlayerId}
                  onClick={() => onSubstitute(regular, p.matchPlayerId)}
                >
                  <b>#{p.jerseyNumber}</b>
                  {p.displayName}
                </button>
              ))}
            </div>
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
  confirmLabel: string;
  onConfirm: () => void;
  onClose: () => void;
  danger?: boolean;
}) {
  return (
    <div className="backdrop">
      <div className="dialog">
        <h2>{title}</h2>
        {children}
        <footer>
          <button onClick={onClose}>Cancelar</button>
          <button className={danger ? 'danger' : ''} onClick={onConfirm}>
            {confirmLabel}
          </button>
        </footer>
      </div>
    </div>
  );
}
export function HistoryDrawer({ events, onClose }: { events: LocalEvent[]; onClose: () => void }) {
  return (
    <div className="backdrop" onMouseDown={onClose}>
      <aside className="history" onMouseDown={(e) => e.stopPropagation()}>
        <button className="close" onClick={onClose}>
          ×
        </button>
        <small>CONSULTA</small>
        <h2>Historial del partido</h2>
        {events.length === 0 ? (
          <p>Sin acciones todavía.</p>
        ) : (
          events.map((event) => (
            <article key={event.eventUuid}>
              <b>{eventLabel(event)}</b>
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
