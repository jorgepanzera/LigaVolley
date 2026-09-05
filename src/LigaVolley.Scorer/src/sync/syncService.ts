import type { ScorerDatabase } from '../persistence/database';
import { MatchRepository } from '../persistence/matchRepository';
import { ApiProblem, scorerApi } from '../api/scorerApi';
import { reconcile } from './reconciliationService';
export type SyncPhase = 'IDLE' | 'SYNCING' | 'RECONCILING' | 'BLOCKED';
export class SyncService {
  phase: SyncPhase = 'IDLE';
  lastError?: string;
  constructor(
    private database: ScorerDatabase,
    private api = scorerApi,
    private changed = () => {},
  ) {}
  async sync(matchId: number) {
    if (this.phase === 'SYNCING' || this.phase === 'BLOCKED') return;
    const repo = new MatchRepository(this.database),
      local = await repo.active(matchId);
    if (!local || local.session.status !== 'ACTIVE') return;
    const events = await repo.pending(local.session.sessionUuid);
    if (!events.length) return;
    this.phase = 'SYNCING';
    this.changed();
    await this.database.events
      .where('eventUuid')
      .anyOf(events.map((x) => x.eventUuid))
      .modify({ syncStatus: 'SYNCING' });
    try {
      const response = await this.api.sync(matchId, {
        sheetUuid: local.sheet.sheetUuid,
        sessionUuid: local.session.sessionUuid,
        deviceId: local.session.deviceId,
        events,
      });
      this.phase = 'RECONCILING';
      await this.database.transaction(
        'rw',
        this.database.events,
        this.database.sessions,
        async () => {
          await this.database.events
            .where('eventUuid')
            .anyOf(response.results.map((x) => x.eventUuid))
            .modify({ syncStatus: 'ACCEPTED' });
          await this.database.sessions.update(local.session.sessionUuid, {
            lastAcceptedSequence: response.lastAcceptedSequence,
            status: response.snapshot.sheet.status === 'CLOSED' ? 'CLOSED' : 'ACTIVE',
            endedAt:
              response.snapshot.sheet.status === 'CLOSED' ? new Date().toISOString() : undefined,
          });
        },
      );
      await reconcile(this.database, matchId, response.snapshot, response.lastAcceptedSequence);
      this.phase = response.snapshot.sheet.status === 'CLOSED' ? 'BLOCKED' : 'IDLE';
      this.lastError = undefined;
    } catch (error) {
      const p = error as ApiProblem;
      await this.database.events
        .where('eventUuid')
        .anyOf(events.map((x) => x.eventUuid))
        .modify({ syncStatus: 'PENDING' });
      const permanent = p.status >= 400 && p.status < 500;
      if (permanent) {
        await this.database.sessions.update(local.session.sessionUuid, {
          status: 'ABANDONED',
          endedAt: new Date().toISOString(),
        });
        this.phase = 'BLOCKED';
        this.lastError = p.code;
      } else {
        this.phase = 'IDLE';
        this.lastError = 'sync_temporarily_unavailable';
      }
    } finally {
      this.changed();
    }
  }
}
