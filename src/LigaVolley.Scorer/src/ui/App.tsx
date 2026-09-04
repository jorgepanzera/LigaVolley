import { useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react';
import { createScorerController } from '../application/composition';
import type { ViewState } from '../application/scorerController';
import type { Side } from '../domain/types';
import type { OpenMatchContext, OpenTeamContext } from '../api/scorerApi';
import { currentSet } from '../domain/matchEngine';
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
} from './console/ConsoleDialogs';
import './app.css';
const matchId = Number(new URLSearchParams(location.search).get('matchId') ?? 1);
export default function App() {
  const controller = useMemo(createScorerController, []),
    view = useSyncExternalStore(
      (cb) => controller.subscribe(cb),
      () => controller.view,
    ),
    [dialog, setDialog] = useState<string>(),
    [selected, setSelected] = useState<{ side: Side; logical: number }>(),
    [scoreLocked, setScoreLocked] = useState(false),
    [feedback, setFeedback] = useState('');
  useEffect(() => {
    void controller.start(matchId);
    const online = () => void controller.sync(),
      refresh = () => void controller.refresh();
    addEventListener('online', online);
    addEventListener('offline', refresh);
    return () => {
      removeEventListener('online', online);
      removeEventListener('offline', refresh);
    };
  }, [controller]);
  if (view.opening) return <OpeningView context={view.opening} onOpen={(request) => void controller.open(request)} />;
  if (!view.state || !view.bootstrap)
    return (
      <main className="loading">
        <div className="spinner" />
        <h1>Preparando consola</h1>
        <p>
          {view.error === 'offline_no_local_match'
            ? 'Se necesita conexión para cargar este partido por primera vez.'
            : 'Recuperando partido guardado…'}
        </p>
      </main>
    );
  const state = view.state,
    set = state.currentSetNumber ? currentSet(state) : undefined,
    previous = set && state.sets.find((x) => x.setNumber === set.setNumber - 1),
    trackSubs = view.bootstrap.trackSubstitutions !== false,
    trackLibero = view.bootstrap.trackLiberoReplacements !== false,
    closed = state.closed,
    blocked = view.runtime === 'BLOCKED';
  const score = async (side: Side) => {
    if (scoreLocked) return;
    setScoreLocked(true);
    const before = set?.servingSide;
    await controller.point(side);
    setFeedback(
      before === side ? `${side} suma · conserva saque` : `${side} recupera saque · rota`,
    );
    setTimeout(() => setScoreLocked(false), 425);
    setTimeout(() => setFeedback(''), 1800);
  };
  const consequence =
    set?.lastConsequences.map((x) => x.text).join(' · ') ||
    feedback ||
    'Consola preparada para la próxima acción';
  if (closed)
    return (
      <ClosedConsole
        view={view}
        onHistory={() => setDialog('history')}
        onSync={() => void controller.sync()}
        history={
          dialog === 'history' ? (
            <HistoryDrawer events={view.events} onClose={() => setDialog(undefined)} />
          ) : null
        }
      />
    );
  return (
    <main className={`scorer-console ${blocked ? 'is-blocked' : ''}`}>
      <header className="match-header">
        <div>
          <small>
            {view.bootstrap.competition?.competitionName ?? 'LigaVolley'} · Match #{matchId}
          </small>
          <h1>{set?.status === 'READY' ? 'Preparación de set' : 'Consola de partido'}</h1>
        </div>
        <div className="header-actions">
          <SyncStatusIndicator
            runtime={view.runtime}
            pending={view.pendingEventCount}
            onClick={() => setDialog('sync')}
          />
          <button className="menu" onClick={() => setDialog('menu')}>
            ⋮<span>Más</span>
          </button>
        </div>
      </header>
      {blocked && (
        <section className="blocked-banner">
          <b>SESIÓN BLOQUEADA</b>
          <span>Este dispositivo perdió autoridad. Se preservaron todos los eventos locales.</span>
        </section>
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
        <>
          <ScoreBoard
            set={set}
            homeSets={state.homeSets}
            awaySets={state.awaySets}
            snapshot={view.bootstrap}
          />
          {set && (
            <Court
              set={set}
              snapshot={view.bootstrap}
              onPosition={(side, logical) =>
                !blocked && set.status === 'IN_PROGRESS' && setSelected({ side, logical })
              }
            />
          )}
          <section className="primary-controls">
            <button
              className="point home"
              disabled={!set || set.status !== 'IN_PROGRESS' || scoreLocked || blocked}
              onClick={() => void score('HOME')}
            >
              <span>+ PUNTO</span>
              <b>HOME</b>
            </button>
            <div className="middle-controls">
              <button
                disabled={!set || set.lastSportingEvent !== 'POINT' || blocked}
                onClick={() => setDialog('correct')}
              >
                ↶ Corregir último punto
              </button>
              <span className={feedback ? 'flash' : ''}>{consequence}</span>
            </div>
            <button
              className="point away"
              disabled={!set || set.status !== 'IN_PROGRESS' || scoreLocked || blocked}
              onClick={() => void score('AWAY')}
            >
              <span>+ PUNTO</span>
              <b>AWAY</b>
            </button>
          </section>
          {set?.status === 'IN_PROGRESS' && (
            <section className="timeout-controls">
              <button
                disabled={set.homeTimeouts >= 2 || blocked}
                onClick={() => setDialog('timeout-home')}
              >
                Timeout HOME <b>{set.homeTimeouts}/2</b>
              </button>
              <button
                disabled={set.awayTimeouts >= 2 || blocked}
                onClick={() => setDialog('timeout-away')}
              >
                Timeout AWAY <b>{set.awayTimeouts}/2</b>
              </button>
            </section>
          )}
          <footer className="set-strip">
            {!set && (
              <button className="next-set" onClick={() => void controller.prepareSet()}>
                Preparar Set 1
              </button>
            )}
            {state.sets.map((x) => (
              <span className={x.status.toLowerCase()} key={x.setNumber}>
                Set {x.setNumber} {x.homePoints}-{x.awayPoints}
                {x.status === 'FINISHED' ? ' ✓' : ' ●'}
              </span>
            ))}
            <button onClick={() => setDialog('history')}>Historial</button>
            {set?.status === 'FINISHED' && !state.matchDecided && (
              <button className="next-set" onClick={() => void controller.prepareSet()}>
                Preparar Set {set.setNumber + 1}
              </button>
            )}
            {state.matchDecided && (
              <button className="review" onClick={() => setDialog('review')}>
                Revisar partido
              </button>
            )}
          </footer>
        </>
      )}
      {selected && set && (
        <PlayerActionSheet
          {...selected}
          set={set}
          snapshot={view.bootstrap}
          trackSubstitutions={trackSubs}
          onClose={() => setSelected(undefined)}
          onSubstitute={(outId, inId) => {
            void controller.substitute(selected.side, outId, inId);
            setSelected(undefined);
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
      {dialog?.startsWith('timeout-') && set && (
        <ConfirmDialog
          title={`TIMEOUT · ${dialog.endsWith('home') ? 'HOME' : 'AWAY'}`}
          confirmLabel="Registrar timeout"
          onClose={() => setDialog(undefined)}
          onConfirm={() => {
            void controller.timeout(dialog.endsWith('home') ? 'HOME' : 'AWAY');
            setDialog(undefined);
          }}
        >
          <p>Usados: {dialog.endsWith('home') ? set.homeTimeouts : set.awayTimeouts} / 2</p>
        </ConfirmDialog>
      )}
      {dialog === 'history' && (
        <HistoryDrawer events={view.events} onClose={() => setDialog(undefined)} />
      )}{' '}
      {dialog === 'review' && set && (
        <MatchReview
          set={set}
          snapshot={view.bootstrap}
          homeSets={state.homeSets}
          awaySets={state.awaySets}
          onClose={() => setDialog(undefined)}
          onCorrect={() => setDialog('correct')}
          onHistory={() => setDialog('history')}
          onConfirmClose={() => setDialog('close')}
        />
      )}{' '}
      {dialog === 'close' && (
        <ConfirmDialog
          title="CERRAR PARTIDO"
          confirmLabel="Cerrar acta definitivamente"
          onClose={() => setDialog(undefined)}
          onConfirm={() => {
            void controller.closeMatch();
            setDialog(undefined);
          }}
          danger
        >
          <h3>
            HOME {state.homeSets} — {state.awaySets} AWAY
          </h3>
          <p>
            Esta acción cerrará definitivamente el acta. Después del cierre no podrán registrarse
            nuevas acciones deportivas.
          </p>
        </ConfirmDialog>
      )}
      {dialog === 'sync' && (
        <ConfirmDialog
          title="SINCRONIZACIÓN"
          confirmLabel="Intentar sincronizar"
          onClose={() => setDialog(undefined)}
          onConfirm={() => {
            void controller.sync();
            setDialog(undefined);
          }}
        >
          <p>
            Estado: <b>{view.runtime}</b>
          </p>
          <p>
            Eventos pendientes: <b>{view.pendingEventCount}</b>
          </p>
          <p>
            Partido seguro en este dispositivo: <b>Sí</b>
          </p>
        </ConfirmDialog>
      )}
      {dialog === 'menu' && (
        <div className="backdrop" onMouseDown={() => setDialog(undefined)}>
          <aside className="secondary-menu" onMouseDown={(e) => e.stopPropagation()}>
            <h2>Más acciones</h2>
            <button onClick={() => setDialog('history')}>Ver historial</button>
            <button onClick={() => setDialog('sync')}>Estado de sincronización</button>
            {blocked && (
              <button onClick={() => void controller.takeOver()}>
                Continuar en este dispositivo
              </button>
            )}
            <button onClick={() => setDialog(undefined)}>Cerrar menú</button>
          </aside>
        </div>
      )}
    </main>
  );
}
type TeamSelection = { players: { competitionRosterPlayerId: number; jerseyNumber?: number; isMatchCaptain: boolean }[]; liberoCompetitionRosterPlayerIds: number[]; competitionRosterStaffIds: number[] };
function selection(context: OpenTeamContext): TeamSelection {
  return { players: context.players.slice(0, 6).map((x) => ({ competitionRosterPlayerId: x.competitionRosterPlayerId, isMatchCaptain: false })), liberoCompetitionRosterPlayerIds: [], competitionRosterStaffIds: [] };
}
function OpeningView({ context, onOpen }: { context: OpenMatchContext; onOpen: (request: { home: TeamSelection; away: TeamSelection; trackSubstitutions: boolean; trackLiberoReplacements: boolean }) => void }) {
  const [home, setHome] = useState(() => selection(context.home));
  const [away, setAway] = useState(() => selection(context.away));
  const [trackSubstitutions, setTrackSubstitutions] = useState(true);
  const [trackLiberoReplacements, setTrackLiberoReplacements] = useState(true);
  const valid = (team: TeamSelection) => team.players.length >= 6 && team.players.every(x => x.jerseyNumber != null && x.jerseyNumber >= 1 && x.jerseyNumber <= 99) && new Set(team.players.map(x => x.jerseyNumber)).size === team.players.length && team.players.filter(x => x.isMatchCaptain).length === 1;
  return <main className="loading opening-view"><h1>Abrir acta</h1><p>{context.home.teamName} vs {context.away.teamName}</p>{context.warnings.map((warning) => <p key={warning}>{warning}</p>)}<OpeningTeam context={context.home} value={home} onChange={setHome} /><OpeningTeam context={context.away} value={away} onChange={setAway} /><label><input type="checkbox" checked={trackSubstitutions} onChange={(e) => setTrackSubstitutions(e.target.checked)} /> Registrar sustituciones</label><label><input type="checkbox" checked={trackLiberoReplacements} onChange={(e) => setTrackLiberoReplacements(e.target.checked)} /> Registrar reemplazos de líbero</label><button disabled={!valid(home) || !valid(away)} onClick={() => onOpen({ home, away, trackSubstitutions, trackLiberoReplacements })}>Abrir acta</button></main>;
}
function OpeningTeam({ context, value, onChange }: { context: OpenTeamContext; value: TeamSelection; onChange: (value: TeamSelection) => void }) {
  const toggle = (id: number) => {
    const selected = value.players.some(x => x.competitionRosterPlayerId === id);
    onChange({...value, players: selected ? value.players.filter(x => x.competitionRosterPlayerId !== id) : [...value.players, {competitionRosterPlayerId:id,isMatchCaptain:false}]});
  };
  const duplicate = value.players.length !== new Set(value.players.map(x => x.jerseyNumber).filter(Boolean)).size;
  return <section><h2>{context.teamName}</h2>{duplicate&&<p className="warning">Dorsal repetido en {context.teamName}.</p>}{value.players.filter(x=>x.isMatchCaptain).length!==1&&<p className="warning">Debes seleccionar un capitán para {context.teamName}.</p>}{context.players.map((player) => {const selected=value.players.find(x=>x.competitionRosterPlayerId===player.competitionRosterPlayerId);return <div key={player.competitionRosterPlayerId}><label><input type="checkbox" checked={!!selected} onChange={() => toggle(player.competitionRosterPlayerId)} /> {player.displayName}{player.role==='Libero'?' · LIBERO':''}</label>{selected&&<><input aria-label={`Dorsal de ${player.displayName}`} type="number" min="1" max="99" value={selected.jerseyNumber??''} onChange={e=>onChange({...value,players:value.players.map(x=>x.competitionRosterPlayerId===player.competitionRosterPlayerId?{...x,jerseyNumber:e.target.value?Number(e.target.value):undefined}:x)})}/><input aria-label={`Capitán de ${player.displayName}`} type="radio" name={`captain-${context.teamEntryId}`} checked={selected.isMatchCaptain} onChange={()=>onChange({...value,players:value.players.map(x=>({...x,isMatchCaptain:x.competitionRosterPlayerId===player.competitionRosterPlayerId}))})}/></>}</div>})}</section>;
}
function ClosedConsole({
  view,
  onHistory,
  onSync,
  history,
}: {
  view: ViewState;
  onHistory: () => void;
  onSync: () => void;
  history: ReactNode;
}) {
  return (
    <main className="closed-view">
      <small>PARTIDO FINALIZADO</small>
      <h1>
        {view.bootstrap?.home.teamName} {view.state?.homeSets} — {view.state?.awaySets}{' '}
        {view.bootstrap?.away.teamName}
      </h1>
      <div className="closed-checks">
        <p>✓ Cerrado en este dispositivo</p>
        <p>
          {view.pendingEventCount
            ? `⚠ ${view.pendingEventCount} eventos pendientes`
            : '✓ Sincronizado'}
        </p>
      </div>
      <div>
        <button onClick={onHistory}>Historial</button>
        {view.pendingEventCount > 0 && <button onClick={onSync}>Sincronizar ahora</button>}
      </div>
      {history}
    </main>
  );
}
