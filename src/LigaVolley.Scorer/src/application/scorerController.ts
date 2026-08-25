import type { ScorerDatabase } from '../persistence/database';
import { deviceId } from '../persistence/database';
import { MatchRepository } from '../persistence/matchRepository';
import { scorerApi } from '../api/scorerApi';
import { SyncService } from '../sync/syncService';
import type { MatchCommand, MatchState, RuntimeState } from '../domain/types';
export interface ViewState {
  runtime: RuntimeState;
  pendingEventCount: number;
  state?: MatchState;
  matchId?: number;
  sessionUuid?: string;
  deviceId?: string;
  lastAcceptedSequence: number;
  error?: string;
}
export class ScorerController {
  view: ViewState = { runtime: 'BOOTSTRAPPING', pendingEventCount: 0, lastAcceptedSequence: 0 };
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
      matchId,
    };
    this.emit();
    await this.repo.resetSyncing();
    let local = await this.repo.active(matchId);
    if (!local) {
      try {
        const server = await this.api.sheet(matchId);
        await this.repo.bootstrap(matchId, server, await deviceId(this.database));
        local = await this.repo.active(matchId);
      } catch (e) {
        this.view = { ...this.view, runtime: 'OFFLINE', error: 'offline_no_local_match' };
        this.emit();
        return;
      }
    }
    await this.refresh();
    if (local) void this.syncService.sync(matchId);
  }
  async refresh() {
    if (!this.view.matchId) return;
    const local = await this.repo.active(this.view.matchId);
    if (!local) return;
    const pending = await this.database.events
      .where('[sessionUuid+syncStatus]')
      .equals([local.session.sessionUuid, 'PENDING'])
      .count();
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
      runtime,
      pendingEventCount: pending,
      state: local.snapshot.state,
      matchId: this.view.matchId,
      sessionUuid: local.session.sessionUuid,
      deviceId: local.session.deviceId,
      lastAcceptedSequence: local.session.lastAcceptedSequence,
      error: this.syncService.lastError,
    };
    this.emit();
  }
  async command(command: MatchCommand) {
    if (!this.view.matchId || ['BLOCKED', 'CLOSED'].includes(this.view.runtime))
      throw new Error(this.view.runtime === 'CLOSED' ? 'match_closed' : 'session_lost');
    await this.repo.mutate(this.view.matchId, command);
    await this.refresh();
    void this.syncService.sync(this.view.matchId);
  }
  async prepareAndStart() {
    if (!this.view.matchId) return;
    const local = await this.repo.active(this.view.matchId);
    if (!local) return;
    const setNumber = local.snapshot.state.sets.length + 1,
      home = local.sheet.bootstrap.home.players.slice(0, 6).map((x) => x.matchPlayerId),
      away = local.sheet.bootstrap.away.players.slice(0, 6).map((x) => x.matchPlayerId);
    await this.command({ type: 'PREPARE_SET', payload: {} });
    await this.command({
      type: 'SET_LINEUP',
      payload: {
        setNumber,
        side: 'HOME',
        ...Object.fromEntries(home.map((x, i) => [`p${i + 1}MatchPlayerId`, x])),
      },
    });
    await this.command({
      type: 'SET_LINEUP',
      payload: {
        setNumber,
        side: 'AWAY',
        ...Object.fromEntries(away.map((x, i) => [`p${i + 1}MatchPlayerId`, x])),
      },
    });
    await this.command({ type: 'START_SET', payload: { setNumber, initialServingSide: 'HOME' } });
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
