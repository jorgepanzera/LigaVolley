import type { ScorerDatabase } from '../persistence/database';
import { deviceId } from '../persistence/database';
import { MatchRepository } from '../persistence/matchRepository';
import { ApiProblem, scorerApi, type OpenMatchContext, type OpenMatchRequest } from '../api/scorerApi';
import { SyncService } from '../sync/syncService';
import { reconcile } from '../sync/reconciliationService';
import type {
  LiberoPlan,
  LocalEvent,
  MatchCommand,
  MatchState,
  RuntimeState,
  ServerSheetSnapshot,
  Side,
} from '../domain/types';
export interface ViewState {
  runtime: RuntimeState;
  pendingEventCount: number;
  state?: MatchState;
  bootstrap?: ServerSheetSnapshot;
  opening?: OpenMatchContext;
  events: LocalEvent[];
  matchId?: number;
  lastAcceptedSequence: number;
  error?: string;
  storagePersisted?: boolean;
}
export class ScorerController {
  view: ViewState = {
    runtime: 'BOOTSTRAPPING',
    pendingEventCount: 0,
    lastAcceptedSequence: 0,
    events: [],
  };
  private repo: MatchRepository;
  readonly syncService: SyncService;
  private listeners = new Set<() => void>();
  constructor(
    private database: ScorerDatabase,
    private api = scorerApi,
  ) {
    this.repo = new MatchRepository(database);
    this.syncService = new SyncService(database, api, () => void this.refresh());
  }
  subscribe(fn: () => void) {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  }
  private emit() {
    this.listeners.forEach((x) => x());
  }
  async start(matchId: number) {
    this.view = {
      runtime: 'BOOTSTRAPPING',
      pendingEventCount: 0,
      lastAcceptedSequence: 0,
      events: [],
      matchId,
    };
    this.emit();
    await this.repo.resetSyncing();
    let local = await this.repo.active(matchId);
    try {
      if (local) {
        const server = await this.api.sheet(matchId);
        await reconcile(this.database, matchId, server, server.session.lastAcceptedSequence);
        local = await this.repo.active(matchId);
      } else {
        const server = await this.api.sheet(matchId);
        await this.repo.bootstrap(matchId, server, await deviceId(this.database));
        local = await this.repo.active(matchId);
      }
    } catch (error) {
      if (!local && isMissingSheet(error)) {
        try {
          const opening = await this.api.openContext(matchId);
          if (opening.existingMatchSheet) {
            const server = await this.api.sheet(matchId);
            await this.repo.bootstrap(matchId, server, await deviceId(this.database));
            local = await this.repo.active(matchId);
          } else {
            this.view = { ...this.view, runtime: 'READY', opening };
            this.emit();
            return;
          }
        } catch (contextError) {
          this.view = { ...this.view, runtime: 'OFFLINE', error: errorCode(contextError) };
          this.emit();
          return;
        }
      } else {
        this.view = { ...this.view, runtime: 'OFFLINE', error: errorCode(error) };
        this.emit();
        return;
      }
    }
    this.view.storagePersisted = await requestPersistentStorage();
    await this.refresh();
    if (local) void this.syncService.sync(matchId);
  }
  async open(request: Omit<OpenMatchRequest, 'deviceId' | 'clientRequestId'>) {
    if (!this.view.matchId || !this.view.opening) throw new Error('opening_context_missing');
    const device = await deviceId(this.database);
    const response = await this.api.open(this.view.matchId, {
      ...request,
      deviceId: device,
      clientRequestId: crypto.randomUUID(),
    });
    await this.repo.bootstrap(this.view.matchId, response.matchSheet, device);
    this.view = { ...this.view, opening: undefined };
    await this.refresh();
  }
  async refresh() {
    if (!this.view.matchId) return;
    const local = await this.repo.active(this.view.matchId);
    if (!local) return;
    const pending = await this.database.events
        .where('[sessionUuid+syncStatus]')
        .equals([local.session.sessionUuid, 'PENDING'])
        .count(),
      events = await this.database.events
        .where('[sessionUuid+sequence]')
        .between([local.session.sessionUuid, DexieMin], [local.session.sessionUuid, DexieMax])
        .toArray();
    const runtime: RuntimeState =
      local.session.status === 'CLOSED' || local.snapshot.state.closeConfirmed
        ? 'CLOSED'
        : local.session.status === 'ABANDONED' || this.syncService.phase === 'BLOCKED'
          ? 'BLOCKED'
          : this.syncService.phase === 'SYNCING' || this.syncService.phase === 'RECONCILING'
            ? 'SYNCING'
            : navigator.onLine
              ? 'READY'
              : 'OFFLINE';
    this.view = {
      ...this.view,
      runtime,
      pendingEventCount: pending,
      state: local.snapshot.state,
      bootstrap: local.sheet.bootstrap,
      events: events.sort((a, b) => b.sequence - a.sequence),
      lastAcceptedSequence: local.session.lastAcceptedSequence,
      error: this.syncService.lastError,
    };
    this.emit();
  }
  async command(command: MatchCommand) {
    if (!this.view.matchId || this.view.runtime === 'BLOCKED' || this.view.state?.closed)
      throw new Error(this.view.state?.closed ? 'match_closed' : 'session_lost');
    await this.repo.mutate(this.view.matchId, command);
    await this.refresh();
    void this.syncService.sync(this.view.matchId);
  }
  prepareSet() {
    return this.command({ type: 'PREPARE_SET', payload: {} });
  }
  saveLineup(side: Side, players: number[], plan: LiberoPlan) {
    const payload: Record<string, unknown> = {
      setNumber: this.view.state?.currentSetNumber,
      side,
      liberoMatchPlayerId: plan.enabled ? plan.liberoMatchPlayerId : null,
      liberoLogicalPositions: plan.enabled ? plan.logicalPositions : [],
    };
    players.forEach((id, index) => (payload[`p${index + 1}MatchPlayerId`] = id));
    return this.command({ type: 'SET_LINEUP', payload });
  }
  startSet(initialServingSide: Side) {
    return this.command({
      type: 'START_SET',
      payload: { setNumber: this.view.state?.currentSetNumber, initialServingSide },
    });
  }
  point(winningSide: Side) {
    return this.command({
      type: 'POINT',
      payload: { setNumber: this.view.state?.currentSetNumber, winningSide },
    });
  }
  timeout(side: Side) {
    return this.command({
      type: 'TIMEOUT',
      payload: { setNumber: this.view.state?.currentSetNumber, side },
    });
  }
  correctLastPoint() {
    return this.command({
      type: 'CORRECT_LAST_POINT',
      payload: { setNumber: this.view.state?.currentSetNumber },
    });
  }
  substitute(side: Side, playerOutMatchPlayerId: number, playerInMatchPlayerId: number) {
    return this.command({
      type: 'SUBSTITUTION',
      payload: {
        setNumber: this.view.state?.currentSetNumber,
        side,
        playerOutMatchPlayerId,
        playerInMatchPlayerId,
      },
    });
  }
  closeMatch() {
    return this.command({ type: 'MATCH_CLOSE', payload: {} });
  }
  async sync() {
    if (this.view.matchId) await this.syncService.sync(this.view.matchId);
  }
  async takeOver() {
    if (!this.view.matchId) throw new Error('offline_no_local_match');
    const local = await this.repo.active(this.view.matchId);
    if (!local) throw new Error('offline_no_local_match');
    const device = await deviceId(this.database),
      response = await this.api.takeOver(this.view.matchId, {
        sheetUuid: local.sheet.sheetUuid,
        expectedSessionUuid: local.session.sessionUuid,
        deviceId: device,
        clientRequestId: crypto.randomUUID(),
      });
    await this.database.transaction(
      'rw',
      this.database.sessions,
      this.database.snapshots,
      this.database.matchSheets,
      async () => {
        await this.database.sessions.update(local.session.sessionUuid, {
          status: 'ABANDONED',
          endedAt: new Date().toISOString(),
        });
        await this.repo.bootstrap(this.view.matchId!, response.snapshot, device);
      },
    );
    await this.refresh();
  }
}
function isMissingSheet(error: unknown): error is ApiProblem {
  return error instanceof ApiProblem && error.status === 404 && error.code === 'match_sheet_not_found';
}
function errorCode(error: unknown) {
  return error instanceof ApiProblem ? error.code : 'offline_no_local_match';
}
const DexieMin = -Number.MAX_SAFE_INTEGER,
  DexieMax = Number.MAX_SAFE_INTEGER;
export async function requestPersistentStorage() {
  try {
    return Boolean(await navigator.storage?.persist?.());
  } catch {
    return false;
  }
}
