import { LiberoSuggestions } from './console/LiberoSuggestions';
import { RuleWarningDialog } from './console/RuleWarningDialog';
import { useEffect, useMemo, useState, useSyncExternalStore } from 'react';
import { createScorerController } from '../application/composition';
import type { ViewState } from '../application/scorerController';
import type { MatchCommand, ServerSheetSnapshot, SetState, Side } from '../domain/types';
import type { OpenMatchContext, OpenTeamContext } from '../api/scorerApi';
import { currentSet, effectivePlayers } from '../domain/matchEngine';
import { Court } from './console/Court';
import { ScoreBoard } from './console/ScoreBoard';
import { SetPreparation } from './console/SetPreparation';
import { SyncStatusIndicator } from './console/SyncStatusIndicator';
import {
  ConfirmDialog,
  CorrectPreview,
  HistoryDrawer,
  MatchReview,
  PlayerActionSheet,
  SanctionDialog,
} from './console/ConsoleDialogs';
import { team } from './console/model';
import { isOpeningTeamValid } from './console/openSheetValidation';
import { toggleLiberoDeclaration, toggleOpeningPlayer, type OpeningSelectionState } from './console/openingTeamSelection';
import './app.css';

const matchId = Number(new URLSearchParams(location.search).get('matchId') ?? 1);
type Panel = 'history' | 'sheet' | 'more';
type Dialog = 'timeout' | 'correct' | 'review' | 'close' | 'takeover' | 'sanction';

export default function App() {
  const controller = useMemo(createScorerController, []);
  const view = useSyncExternalStore(
    (cb) => controller.subscribe(cb),
    () => controller.view,
  );
  const [panel, setPanel] = useState<Panel>();
  const [dialog, setDialog] = useState<Dialog>();
  const [selected, setSelected] = useState<{ side: Side; logical: number }>();
  const [substitutionError, setSubstitutionError] = useState('');
  const [scoreLocked, setScoreLocked] = useState(false);
  const [feedback, setFeedback] = useState('');

  useEffect(() => {
    void controller.start(matchId);
    const online = () => void controller.refresh().then(() => controller.sync());
    const refresh = () => void controller.refresh();
    addEventListener('online', online);
    addEventListener('offline', refresh);
    return () => {
      removeEventListener('online', online);
      removeEventListener('offline', refresh);
    };
  }, [controller]);

  if (view.opening)
    return (
      <OpenSheetWorkspace
        context={view.opening}
        view={view}
        onOpen={(request) => void controller.open(request)}
      />
    );
  if (!view.state || !view.bootstrap) return <RecoveryView error={view.error} />;

  const state = view.state;
  const set = state.currentSetNumber ? currentSet(state) : undefined;
  const previous = set && state.sets.find((x) => x.setNumber === set.setNumber - 1);
  const blocked = view.runtime === 'BLOCKED';
  const trackSubs = view.bootstrap.trackSubstitutions !== false;
  const trackLibero = view.bootstrap.trackLiberoReplacements !== false;
  const score = async (side: Side) => {
    if (scoreLocked || blocked) return;
    setScoreLocked(true);
    const before = set?.servingSide;
    try {
      await controller.point(side);
      setFeedback(
        before === side ? `${side} suma · conserva saque` : `${side} recupera saque · rota`,
      );
    } finally {
      window.setTimeout(() => setScoreLocked(false), 425);
      window.setTimeout(() => setFeedback(''), 1800);
    }
  };

  return (
    <ScorerShell view={view} panel={panel} onPanel={setPanel}>
      <section className="console-workspace" aria-live="polite">
        {set?.setNumber === 5 &&
          Math.max(set.homePoints, set.awayPoints) ===
            (state.rulesSnapshot?.decidingSetCourtChangePoint ?? 8) && (
            <p role="status">Cambio de campo</p>
          )}
        {set?.status === 'READY' ? (
          <SetPreparation
            set={set}
            previous={previous}
            snapshot={view.bootstrap}
            trackLibero={trackLibero}
            onSave={(side, players, plan) => controller.saveLineup(side, players, plan)}
            onStart={(side) => controller.startSet(side)}
          />
        ) : (
          <MatchWorkspace
            view={view}
            set={set}
            blocked={blocked}
            trackSubs={trackSubs}
            scoreLocked={scoreLocked}
            feedback={feedback}
            onScore={(side) => void score(side)}
            onPrepare={() => void controller.prepareSet()}
            onPosition={(side, logical) => {
              setSubstitutionError('');
              setSelected({ side, logical });
            }}
            onDialog={setDialog}
            onCommand={(command) => controller.command(command)}
          />
        )}
      </section>

      {selected && set?.status === 'IN_PROGRESS' && !blocked && (
        <PlayerActionSheet
          {...selected}
          set={set}
          snapshot={view.bootstrap}
          trackSubstitutions={trackSubs}
          error={substitutionError}
          onClose={() => setSelected(undefined)}
          onSubstitutionRequest={(pairs) => {
            void controller
              .substituteRequest(selected.side, pairs)
              .then(() => setSelected(undefined))
              .catch((error: unknown) => setSubstitutionError(substitutionMessage(error)));
          }}
          onLiberoEnter={(liberoMatchPlayerId, replacedMatchPlayerId) => {
            void controller
              .command({
                type: 'LIBERO_ENTER',
                payload: {
                  setNumber: set.setNumber,
                  side: selected.side,
                  liberoMatchPlayerId,
                  replacedMatchPlayerId,
                },
              })
              .then(() => setSelected(undefined))
              .catch((error: unknown) => setSubstitutionError(substitutionMessage(error)));
          }}
          onLiberoExit={(liberoMatchPlayerId) => {
            void controller
              .command({
                type: 'LIBERO_EXIT',
                payload: { setNumber: set.setNumber, side: selected.side, liberoMatchPlayerId },
              })
              .then(() => setSelected(undefined))
              .catch((error: unknown) => setSubstitutionError(substitutionMessage(error)));
          }}
          onObservedServer={(observedServerMatchPlayerId, winningSide) => {
            void controller
              .command({
                type: 'POINT',
                payload: { setNumber: set.setNumber, observedServerMatchPlayerId, winningSide },
              })
              .then(() => setSelected(undefined))
              .catch((error: unknown) => setSubstitutionError(substitutionMessage(error)));
          }}
          onSubstitute={(outId, inId) => {
            void controller
              .substitute(selected.side, outId, inId)
              .then(() => setSelected(undefined))
              .catch((error: unknown) => setSubstitutionError(substitutionMessage(error)));
          }}
        />
      )}
      {dialog === 'timeout' && set && (
        <TimeoutDialog
          set={set}
          maximum={state.rulesSnapshot?.maxTimeoutsPerSet ?? 2}
          onClose={() => setDialog(undefined)}
          onTimeout={(side) => {
            void controller.timeout(side);
            setDialog(undefined);
          }}
        />
      )}
      {dialog === 'correct' && set && (
        <CorrectPreview
          set={set}
          snapshot={view.bootstrap}
          onClose={() => setDialog(undefined)}
          onConfirm={() => {
            void controller.correctLastPoint();
            setDialog(undefined);
          }}
        />
      )}
      {dialog === 'sanction' && set && <SanctionDialog snapshot={view.bootstrap} onClose={() => setDialog(undefined)} onConfirm={(side, type, subjectType, subjectId) => { if (subjectType !== 'Team' && !subjectId) return; void controller.sanction(side, type, subjectType, subjectId); setDialog(undefined); }} />}
      {dialog === 'review' && set && (
        <MatchReview
          set={set}
          snapshot={view.bootstrap}
          homeSets={state.homeSets}
          awaySets={state.awaySets}
          onClose={() => setDialog(undefined)}
          onCorrect={() => setDialog('correct')}
          onHistory={() => {
            setDialog(undefined);
            setPanel('history');
          }}
          onConfirmClose={() => setDialog('close')}
        />
      )}
      {dialog === 'close' && (
        <ConfirmDialog
          title="CERRAR PARTIDO"
          confirmLabel="Cerrar partido"
          danger
          onClose={() => setDialog(undefined)}
          onConfirm={() => {
            void controller.closeMatch();
            setDialog(undefined);
          }}
        >
          <h3>
            {view.bootstrap.home.teamName} {state.homeSets} — {state.awaySets}{' '}
            {view.bootstrap.away.teamName}
          </h3>
          <p>
            Cerrar el acta es definitivo. El cierre se guardará localmente aunque no haya conexión.
          </p>
        </ConfirmDialog>
      )}
      {dialog === 'takeover' && (
        <ConfirmDialog
          title="CONTINUAR DESDE ESTADO CENTRAL"
          confirmLabel="Continuar desde estado central"
          danger
          onClose={() => setDialog(undefined)}
          onConfirm={() => {
            void controller.continueFromCentral();
            setDialog(undefined);
          }}
        >
          <p>
            Se consultará el estado central y se tomará control con una sesión nueva. La sesión
            anterior y toda su cola quedarán intactas como trazabilidad.
          </p>
        </ConfirmDialog>
      )}
      {view.pendingRuleDecision && !blocked && (
        <RuleWarningDialog
          evaluation={view.pendingRuleDecision.evaluation}
          onCancel={() => controller.cancelRuleDecision()}
          onConfirm={() => {
            void controller.confirmRuleDecision();
          }}
        />
      )}
      {blocked && (
        <BlockedOverlay
          error={view.error}
          syncBlock={view.syncBlock}
          onContinueCentral={() => setDialog('takeover')}
          onRecoverLocal={() => void controller.recoverLastValidLocal()}
        />
      )}
    </ScorerShell>
  );
}

function ScorerShell({
  view,
  panel,
  onPanel,
  children,
}: {
  view: ViewState;
  panel?: Panel;
  onPanel: (panel?: Panel) => void;
  children: React.ReactNode;
}) {
  const snapshot = view.bootstrap!;
  return (
    <main className={`scorer-shell runtime-${view.runtime.toLowerCase()}`}>
      <header className="scorer-header">
        <div className="brand">
          <span className="brand-ball">◉</span>
          <div>
            <b>LigaVolley</b>
            <small>SCORER</small>
          </div>
        </div>
        <div className="match-context">
          <small>{snapshot.competition?.competitionName ?? `Match #${view.matchId}`}</small>
          <strong>
            {snapshot.home.teamName} <span>vs</span> {snapshot.away.teamName}
          </strong>
        </div>
        <SyncStatusIndicator
          runtime={view.runtime}
          pending={view.pendingEventCount}
          onClick={() => onPanel('more')}
        />
      </header>
      <aside className="scorer-sidebar" aria-label="Navegación de la consola">
        <button
          className={!panel ? 'active' : ''}
          onClick={() => onPanel(undefined)}
          title="Partido"
          aria-label="Partido"
        >
          <span>▦</span>
          <b>Partido</b>
        </button>
        <button
          className={panel === 'history' ? 'active' : ''}
          onClick={() => onPanel('history')}
          title="Historial"
          aria-label="Historial"
        >
          <span>↺</span>
          <b>Historial</b>
        </button>
        <button
          className={panel === 'sheet' ? 'active' : ''}
          onClick={() => onPanel('sheet')}
          title="Acta"
          aria-label="Acta"
        >
          <span>▤</span>
          <b>Acta</b>
        </button>
        <button
          className={panel === 'more' ? 'active' : ''}
          onClick={() => onPanel('more')}
          title="Más"
          aria-label="Más"
        >
          <span>•••</span>
          <b>Más</b>
        </button>
      </aside>
      {children}
      {panel === 'history' && (
        <HistoryDrawer snapshot={view.bootstrap!} events={view.events} onClose={() => onPanel(undefined)} />
      )}
      {panel === 'sheet' && (
        <InfoDrawer title="ACTA" onClose={() => onPanel(undefined)}>
          <SheetInfo view={view} />
        </InfoDrawer>
      )}
      {panel === 'more' && (
        <InfoDrawer title="MÁS" onClose={() => onPanel(undefined)}>
          <MoreInfo view={view} />
        </InfoDrawer>
      )}
    </main>
  );
}

function MatchWorkspace({
  view,
  set,
  blocked,
  trackSubs,
  scoreLocked,
  feedback,
  onScore,
  onPrepare,
  onPosition,
  onDialog,
  onCommand,
}: {
  view: ViewState;
  set?: SetState;
  blocked: boolean;
  trackSubs: boolean;
  scoreLocked: boolean;
  feedback: string;
  onScore: (side: Side) => void;
  onPrepare: () => void;
  onPosition: (side: Side, logical: number) => void;
  onDialog: (dialog: Dialog) => void;
  onCommand: (command: MatchCommand) => Promise<void>;
}) {
  const state = view.state!;
  const snapshot = view.bootstrap!;
  if (state.closed)
    return (
      <FinalState
        eyebrow="ACTA CERRADA"
        title="PARTIDO FINALIZADO ✓"
        snapshot={snapshot}
        state={state}
      />
    );
  if (!set)
    return (
      <>
        <ScoreBoard homeSets={state.homeSets} awaySets={state.awaySets} snapshot={snapshot} />
        <PendingCourt setNumber={1} />
        <div className="state-actions">
          <button className="primary-action" disabled={blocked} onClick={onPrepare}>
            Preparar Set 1
          </button>
        </div>
      </>
    );
  if (state.matchDecided)
    return (
      <>
        <ScoreBoard
          set={set}
          homeSets={state.homeSets}
          awaySets={state.awaySets}
          snapshot={snapshot}
        />
        <FinalState
          eyebrow="EL ACTA PERMANECE ABIERTA"
          title="PARTIDO DECIDIDO"
          snapshot={snapshot}
          state={state}
        />
        <div className="state-actions">
          <button onClick={() => onDialog('review')}>Revisar partido</button>
          <button className="danger" disabled={blocked} onClick={() => onDialog('close')}>
            Cerrar partido
          </button>
        </div>
        <PreviousSets sets={state.sets} />
      </>
    );
  if (set.status === 'FINISHED')
    return (
      <>
        <ScoreBoard
          set={set}
          homeSets={state.homeSets}
          awaySets={state.awaySets}
          snapshot={snapshot}
        />
        <FinalState
          eyebrow={`SET ${set.setNumber} FINALIZADO`}
          title={`${snapshot.home.teamName} ${set.homePoints} — ${set.awayPoints} ${snapshot.away.teamName}`}
          snapshot={snapshot}
          state={state}
          compact
        />
        <div className="state-actions">
          <button className="primary-action" disabled={blocked} onClick={onPrepare}>
            Preparar Set {set.setNumber + 1}
          </button>
        </div>
        <PreviousSets sets={state.sets} />
      </>
    );
  const canCorrect = set.lastSportingEvent === 'POINT';
  return (
    <>
      <ScoreBoard
        set={set}
        homeSets={state.homeSets}
        awaySets={state.awaySets}
        snapshot={snapshot}
      />
      <section className="match-floor">
        <BenchSide side="HOME" snapshot={snapshot} set={set} />
        <Court
          set={set}
          snapshot={snapshot}
          onPosition={(side, logical) => !blocked && onPosition(side, logical)}
        />
        <BenchSide side="AWAY" snapshot={snapshot} set={set} />
      </section>
      <section className="point-actions">
        <div className="point-side home">
          <button
            className="point-button home"
            disabled={scoreLocked || blocked}
            onClick={() => onScore('HOME')}
          >
            <span>+ PUNTO</span>
            <b>{snapshot.home.teamName}</b>
          </button>
          <LiberoSuggestions state={state} snapshot={snapshot} side="HOME" disabled={blocked} onCommand={onCommand} />
        </div>
        <div className="match-feedback">
          <button disabled={!canCorrect || blocked} onClick={() => onDialog('correct')}>
            ↶ Corregir último punto
          </button>
          <small>
            {feedback ||
              set.lastConsequences.map((x) => x.text).join(' · ') ||
              'Listo para la próxima acción'}
          </small>
        </div>
        <div className="point-side away">
          <button
            className="point-button away"
            disabled={scoreLocked || blocked}
            onClick={() => onScore('AWAY')}
          >
            <span>+ PUNTO</span>
            <b>{snapshot.away.teamName}</b>
          </button>
          <LiberoSuggestions state={state} snapshot={snapshot} side="AWAY" disabled={blocked} onCommand={onCommand} />
        </div>
      </section>
      <section className="secondary-actions">
        <button disabled={blocked} onClick={() => onDialog('timeout')}>
          ◷ Timeout{' '}
          <span>
            {set.homeTimeouts}/{view.state?.rulesSnapshot?.maxTimeoutsPerSet ?? 2} ·{' '}
            {set.awayTimeouts}/{view.state?.rulesSnapshot?.maxTimeoutsPerSet ?? 2}
          </span>
        </button>
        {trackSubs && (
          <button
            disabled={blocked}
            onClick={() => document.querySelector<HTMLButtonElement>('.court-position')?.focus()}
          >
            ⇄ Sustitución{' '}
            <span>
              {set.substitutions.filter((x) => x.side === 'HOME').length}/
              {state.rulesSnapshot?.maxSubstitutionsPerSet ?? '∞'} ·{' '}
              {set.substitutions.filter((x) => x.side === 'AWAY').length}/
              {state.rulesSnapshot?.maxSubstitutionsPerSet ?? '∞'} · Selecciona cancha
            </span>
          </button>
        )}
        <button disabled={blocked} onClick={() => onDialog('sanction')}>
          Sanciones <span>Conducta, demora y disciplina</span>
        </button>
      </section>
      <PreviousSets
        sets={state.sets.filter((x) => x.status === 'FINISHED')}
        current={set.setNumber}
      />
    </>
  );
}

function BenchSide({
  side,
  snapshot,
  set,
}: {
  side: Side;
  snapshot: ServerSheetSnapshot;
  set: SetState;
}) {
  const playing = new Set(effectivePlayers(set, side));
  const players = team(snapshot, side)?.players.filter((x) => !playing.has(x.matchPlayerId)) ?? [];
  return (
    <aside className={`bench-side ${side.toLowerCase()}`}>
      <header>
        <b>Banco {side}</b>
        <span>{players.length}</span>
      </header>
      <div className="bench-list">
        {players.map((p) => (
          <span className="bench-player" title={p.displayName} key={p.matchPlayerId}>
            <b>#{p.jerseyNumber}</b>
            <span>{p.displayName}</span>
            {team(snapshot, side)?.liberos.some((x) => x.matchPlayerId === p.matchPlayerId) && (
              <em>L</em>
            )}
          </span>
        ))}
      </div>
    </aside>
  );
}

function PreviousSets({ sets, current }: { sets: SetState[]; current?: number }) {
  const finished = sets.filter((x) => x.status === 'FINISHED' && x.setNumber !== current);
  if (!finished.length) return null;
  return (
    <footer className="previous-sets">
      <b>SETS ANTERIORES</b>
      {finished.map((x) => (
        <span key={x.setNumber}>
          Set {x.setNumber}{' '}
          <strong>
            {x.homePoints}-{x.awayPoints}
          </strong>
        </span>
      ))}
    </footer>
  );
}
function PendingCourt({ setNumber }: { setNumber: number }) {
  return (
    <section className="pending-court">
      <span>◇</span>
      <h2>SET {setNumber} PENDIENTE DE PREPARACIÓN</h2>
      <p>Define las alineaciones iniciales y quién comienza sacando.</p>
    </section>
  );
}
function FinalState({
  eyebrow,
  title,
  snapshot,
  state,
  compact,
}: {
  eyebrow: string;
  title: string;
  snapshot: ServerSheetSnapshot;
  state: NonNullable<ViewState['state']>;
  compact?: boolean;
}) {
  return (
    <section className={`final-state ${compact ? 'compact' : ''}`}>
      <small>{eyebrow}</small>
      <h1>{title}</h1>
      {!compact && (
        <div>
          <strong>{snapshot.home.teamName}</strong>
          <b>
            {state.homeSets} — {state.awaySets}
          </b>
          <strong>{snapshot.away.teamName}</strong>
        </div>
      )}
      <p>{state.sets.map((x) => `${x.homePoints}-${x.awayPoints}`).join(' · ')}</p>
    </section>
  );
}

function TimeoutDialog({
  set,
  maximum,
  onClose,
  onTimeout,
}: {
  set: SetState;
  maximum: number;
  onClose: () => void;
  onTimeout: (side: Side) => void;
}) {
  return (
    <ConfirmDialog title="TIMEOUT" onClose={onClose}>
      <div className="timeout-grid">
        {(['HOME', 'AWAY'] as Side[]).map((side) => {
          const count = side === 'HOME' ? set.homeTimeouts : set.awayTimeouts;
          return (
            <article key={side}>
              <b>{side}</b>
              <div aria-label={`${count} de ${maximum} timeouts usados`}>
                {Array.from({ length: Math.min(maximum, 10) }, (_, i) => (
                  <span key={i}>{i < count ? '●' : '○'}</span>
                ))}
              </div>
              <strong>
                {count}/{maximum}
              </strong>
              <button onClick={() => onTimeout(side)}>Timeout {side}</button>
            </article>
          );
        })}
      </div>
    </ConfirmDialog>
  );
}
function BlockedOverlay({
  error,
  syncBlock,
  onContinueCentral,
  onRecoverLocal,
}: {
  error?: string;
  syncBlock?: ViewState['syncBlock'];
  onContinueCentral: () => void;
  onRecoverLocal: () => void;
}) {
  const authorityLost = [
    'match_sheet_session_not_active',
    'match_sheet_session_mismatch',
    'session_lost',
  ].includes(error ?? '');
  return (
    <div className="blocked-overlay" role="alertdialog" aria-modal="true">
      <section>
        <span>!</span>
        <small>{authorityLost ? 'SESIÓN SIN AUTORIDAD' : 'SINCRONIZACIÓN BLOQUEADA'}</small>
        <h2>
          {authorityLost
            ? 'Otra sesión tiene el control del partido.'
            : 'El servidor rechazó una acción deportiva.'}
        </h2>
        <p>
          Tus eventos locales no fueron eliminados. Puedes consultar el estado, pero no registrar
          nuevas acciones.
        </p>
        {!authorityLost && error && <p className="blocked-code">Código: {error}</p>}
        {syncBlock?.eventUuid && (
          <p className="blocked-code">
            Evento: {syncBlock.eventUuid} · Secuencia local: {syncBlock.localSequence}
          </p>
        )}
        {syncBlock?.locallyRecovered && (
          <p>
            Se reconstruyó el último estado local válido. Sigue siendo de solo consulta: continuar
            offline requiere una decisión pendiente de branching/rebase del protocolo.
          </p>
        )}
        <div>
          <button
            disabled={typeof navigator !== 'undefined' && !navigator.onLine}
            onClick={onContinueCentral}
          >
            Continuar desde estado central
          </button>
          <button
            className="danger"
            disabled={
              (typeof navigator !== 'undefined' && navigator.onLine) ||
              !syncBlock?.eventUuid ||
              syncBlock.localSequence == null
            }
            onClick={onRecoverLocal}
          >
            Recuperar último estado local válido
          </button>
        </div>
      </section>
    </div>
  );
}

function InfoDrawer({
  title,
  children,
  onClose,
}: {
  title: string;
  children: React.ReactNode;
  onClose: () => void;
}) {
  useEscape(onClose);
  return (
    <div className="drawer-backdrop" onMouseDown={onClose}>
      <aside className="info-drawer" onMouseDown={(e) => e.stopPropagation()}>
        <button className="close" aria-label="Cerrar" onClick={onClose}>
          ×
        </button>
        <small>CONSULTA</small>
        <h2>{title}</h2>
        {children}
      </aside>
    </div>
  );
}
function SheetInfo({ view }: { view: ViewState }) {
  const s = view.bootstrap!;
  return (
    <>
      <section className="drawer-group">
        <h3>Partido</h3>
        <p>
          {s.home.teamName} vs {s.away.teamName}
        </p>
        <p>{s.competition?.competitionName}</p>
      </section>
      <section className="drawer-group">
        <h3>Configuración</h3>
        <p>
          Sustituciones: <b>{s.trackSubstitutions === false ? 'No' : 'Sí'}</b>
        </p>
        <p>
          Reemplazos de líbero: <b>{s.trackLiberoReplacements === false ? 'No' : 'Sí'}</b>
        </p>
      </section>
      <section className="drawer-group">
        <h3>Oficiales</h3>
        {s.officials?.map((x) => (
          <p key={x.role}>
            <span>{x.role.replaceAll('_', ' ')}</span>
            <b>{x.displayName}</b>
          </p>
        )) ?? <p>Información central no disponible.</p>}
      </section>
    </>
  );
}
function MoreInfo({ view }: { view: ViewState }) {
  const s = view.bootstrap!;
  return (
    <>
      <section className="drawer-group">
        <h3>Sesión</h3>
        <p>
          <span>Dispositivo</span>
          <b>{s.session.deviceId}</b>
        </p>
        <p>
          <span>Sesión</span>
          <b>{s.session.sessionUuid.slice(0, 8)}</b>
        </p>
        <p>
          <span>Eventos pendientes</span>
          <b>{view.pendingEventCount}</b>
        </p>
      </section>
      <section className="drawer-group">
        <h3>Conexión</h3>
        <p>
          <span>Runtime</span>
          <b>{view.runtime}</b>
        </p>
        <button onClick={() => location.reload()}>Verificar estado central</button>
      </section>
    </>
  );
}
function RecoveryView({ error }: { error?: string }) {
  return (
    <main className="recovery">
      <div className="brand">
        <span className="brand-ball">◉</span>
        <div>
          <b>LigaVolley</b>
          <small>SCORER</small>
        </div>
      </div>
      <section>
        <span className="recovery-ball">◌</span>
        <h1>Recuperando partido…</h1>
        <ul>
          <li>✓ Aplicación disponible</li>
          <li>○ Buscando datos locales</li>
          <li>○ Verificando estado central</li>
        </ul>
        {error && (
          <p>
            {error === 'offline_no_local_match'
              ? 'Se necesita conexión para cargar este partido por primera vez.'
              : error}
          </p>
        )}
      </section>
    </main>
  );
}

type TeamSelection = OpeningSelectionState & {
  players: { competitionRosterPlayerId: number; jerseyNumber?: number; isMatchCaptain: boolean }[];
  liberoCompetitionRosterPlayerIds: number[];
  competitionRosterStaffIds: number[];
};
function initialSelection(): TeamSelection {
  return { players: [], liberoCompetitionRosterPlayerIds: [], competitionRosterStaffIds: [] };
}
function OpenSheetWorkspace({
  context,
  view,
  onOpen,
}: {
  context: OpenMatchContext;
  view: ViewState;
  onOpen: (request: {
    home: TeamSelection;
    away: TeamSelection;
    trackSubstitutions: boolean;
    trackLiberoReplacements: boolean;
  }) => void;
}) {
  const [home, setHome] = useState(initialSelection);
  const [away, setAway] = useState(initialSelection);
  const [subs, setSubs] = useState(true);
  const [libero, setLibero] = useState(true);
  return (
    <main className="open-shell">
      <header className="open-header">
        <div className="brand">
          <span className="brand-ball">◉</span>
          <div>
            <b>LigaVolley</b>
            <small>SCORER</small>
          </div>
        </div>
        <SyncStatusIndicator
          runtime={view.runtime}
          pending={view.pendingEventCount}
          onClick={() => {}}
        />
      </header>
      <section className="open-workspace">
        <div className="open-title">
          <small>CONFIGURACIÓN DEL PARTIDO</small>
          <h1>Abrir acta</h1>
          <p>
            {context.home.teamName} <span>vs</span> {context.away.teamName}
          </p>
        </div>
        {context.warnings.length > 0 && (
          <div className="readiness-warnings">
            {context.warnings.map((x) => (
              <p key={x}>⚠ {x}</p>
            ))}
          </div>
        )}
        <div className="open-teams">
          <OpeningTeam side="HOME" context={context.home} rules={context.rules ?? { liberoEnabled: true, maxLiberos: 2 }} value={home} onChange={setHome} />
          <OpeningTeam side="AWAY" context={context.away} rules={context.rules ?? { liberoEnabled: true, maxLiberos: 2 }} value={away} onChange={setAway} />
        </div>
        <section className="sheet-config">
          <div>
            <small>CONFIGURACIÓN DEL ACTA</small>
            <label>
              <input type="checkbox" checked={subs} onChange={(e) => setSubs(e.target.checked)} />
              <span>
                <b>Registrar sustituciones</b>
                <small>Habilita el seguimiento durante el partido</small>
              </span>
            </label>
            <label>
              <input
                type="checkbox"
                checked={libero}
                onChange={(e) => setLibero(e.target.checked)}
              />
              <span>
                <b>Registrar reemplazos de líbero</b>
                <small>Registra entradas y salidas observadas</small>
              </span>
            </label>
          </div>
          <button
            className="open-button"
            disabled={!isOpeningTeamValid(home.players, home.liberoCompetitionRosterPlayerIds, context.rules?.maxLiberos ?? 2) || !isOpeningTeamValid(away.players, away.liberoCompetitionRosterPlayerIds, context.rules?.maxLiberos ?? 2)}
            onClick={() =>
              onOpen({ home, away, trackSubstitutions: subs, trackLiberoReplacements: libero })
            }
          >
            Abrir acta <span>→</span>
          </button>
        </section>
      </section>
    </main>
  );
}
function OpeningTeam({
  side,
  context,
  rules,
  value,
  onChange,
}: {
  side: Side;
  context: OpenTeamContext;
  rules: { liberoEnabled: boolean; maxLiberos: number };
  value: TeamSelection;
  onChange: (v: TeamSelection) => void;
}) {
  const selected = (id: number) => value.players.find((x) => x.competitionRosterPlayerId === id);
  const maxLiberos = rules.maxLiberos;
  const liberoEnabled = rules.liberoEnabled;
  const toggle = (player: OpenTeamContext['players'][number]) =>
    onChange(toggleOpeningPlayer(value, player, context.players, maxLiberos));
  const duplicate = value.players
    .map((x) => x.jerseyNumber)
    .filter(Boolean)
    .some((x, i, a) => a.indexOf(x) !== i);
  const captainCount = value.players.filter((x) => x.isMatchCaptain).length;
  return (
    <article className={`open-team ${side.toLowerCase()}`}>
      <header>
        <div>
          <small>{side}</small>
          <h2>{context.teamName}</h2>
        </div>
        <strong>
          {value.players.length}
          <span>/ {context.players.length}</span>
          <small>CONVOCADOS</small>
        </strong>
      </header>
      <div className="player-list">
        <div className="player-columns">
          <span>JUGADOR</span>
          <span>DORSAL</span>
          <span>CAPITÁN</span>
        </div>
        {context.players.map((p) => {
          const choice = selected(p.competitionRosterPlayerId);
          const isHabitualLibero = p.isHabitualLiberoCandidate;
          const declaredLibero = value.liberoCompetitionRosterPlayerIds.includes(p.competitionRosterPlayerId);
          return (
            <div
              className={`open-player ${choice ? 'selected' : ''}`}
              key={p.competitionRosterPlayerId}
            >
              <label>
                <input type="checkbox" checked={!!choice} onChange={() => toggle(p)} />
                <span>
                  <b>{p.displayName}</b>
                  <small>
                    Función habitual: {p.role ?? 'Sin especificar'}
                    {isHabitualLibero && <em>Candidata habitual</em>}
                  </small>
                </span>
              </label>
              <input
                aria-label={`Dorsal de ${p.displayName}`}
                type="number"
                min="1"
                max="99"
                disabled={!choice}
                value={choice?.jerseyNumber ?? ''}
                placeholder="—"
                onChange={(e) =>
                  onChange({
                    ...value,
                    players: value.players.map((x) =>
                      x.competitionRosterPlayerId === p.competitionRosterPlayerId
                        ? {
                            ...x,
                            jerseyNumber: e.target.value ? Number(e.target.value) : undefined,
                          }
                        : x,
                    ),
                  })
                }
              />
              <input
                aria-label={`Capitán de ${p.displayName}`}
                type="radio"
                name={`captain-${side}`}
                disabled={!choice}
                checked={choice?.isMatchCaptain ?? false}
                onChange={() =>
                  onChange({
                    ...value,
                    players: value.players.map((x) => ({
                      ...x,
                      isMatchCaptain: x.competitionRosterPlayerId === p.competitionRosterPlayerId,
                    })),
                  })
                }
              />
              <label className="opening-libero-declaration">
                <input
                  type="checkbox"
                  disabled={!choice || !liberoEnabled || (!declaredLibero && value.liberoCompetitionRosterPlayerIds.length >= maxLiberos)}
                  checked={declaredLibero}
                  onChange={() => onChange(toggleLiberoDeclaration(value, p.competitionRosterPlayerId, maxLiberos))}
                />
                <span>Declarar líbero</span>
              </label>
            </div>
          );
        })}
      </div>
      <section className="opening-staff" aria-label={`Staff ${side}`}>
        <h3>Staff convocado</h3>
        {context.staff.length === 0 ? (
          <p>Sin staff activo en el plantel.</p>
        ) : (
          context.staff.map((staff) => {
            const selected = value.competitionRosterStaffIds.includes(staff.competitionRosterStaffId);
            return (
              <label key={staff.competitionRosterStaffId}>
                <input
                  type="checkbox"
                  checked={selected}
                  onChange={() =>
                    onChange({
                      ...value,
                      competitionRosterStaffIds: selected
                        ? value.competitionRosterStaffIds.filter(
                            (id) => id !== staff.competitionRosterStaffId,
                          )
                        : [...value.competitionRosterStaffIds, staff.competitionRosterStaffId],
                    })
                  }
                />
                {staff.displayName}
              </label>
            );
          })
        )}
      </section>
      <footer>
        {duplicate ? (
          <span>⚠ Dorsal repetido en {context.teamName}.</span>
        ) : captainCount !== 1 ? (
          <span>○ Selecciona un capitán.</span>
        ) : value.players.length - value.liberoCompetitionRosterPlayerIds.length < 6 ? (
          <span>◦ Deben quedar seis jugadoras regulares luego de declarar líberos.</span>
        ) : value.players.length < 6 ? (
          <span>○ Faltan {6 - value.players.length} convocados.</span>
        ) : (
          <span className="ok">✓ Convocatoria válida.</span>
        )}
        <small>Declarados para este partido: {value.liberoCompetitionRosterPlayerIds.length}/{maxLiberos}. La función habitual es sólo informativa.</small>
      </footer>
    </article>
  );
}
function useEscape(close: () => void) {
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close();
    };
    addEventListener('keydown', key);
    return () => removeEventListener('keydown', key);
  }, [close]);
}

function substitutionMessage(error: unknown) {
  const code = error instanceof Error ? error.message : '';
  if (code === 'substitution_player_is_libero')
    return 'Los líberos no pueden participar en sustituciones normales.';
  if (code === 'invalid_substitution_pair')
    return 'La sustitución no respeta la pareja titular–suplente. Sólo puede reingresar el titular original.';
  return 'No se pudo registrar la sustitución. Revisa los jugadores seleccionados.';
}
