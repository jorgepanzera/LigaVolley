import 'fake-indexeddb/auto';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ScorerDatabase } from '../persistence/database';
import { MatchRepository } from '../persistence/matchRepository';
import { SyncService } from './syncService';
import type { ServerSheetSnapshot } from '../domain/types';
const snapshot: ServerSheetSnapshot = {
  sheet: { matchSheetId: 1, sheetUuid: 'sheet', status: 'OPEN', openedAt: '' },
  match: { matchId: 1, status: 'SCHEDULED', homeTeamEntryId: 1, awayTeamEntryId: 2 },
  home: { teamName: 'H', players: [], liberos: [] },
  away: { teamName: 'A', players: [], liberos: [] },
  session: {
    sessionUuid: 'session',
    deviceId: 'device',
    status: 'ACTIVE',
    lastAcceptedSequence: 1,
    startedAt: '',
  },
  currentState: {
    homeSets: 0,
    awaySets: 0,
    homePoints: 0,
    awayPoints: 0,
    homeRotationOffset: 0,
    awayRotationOffset: 0,
    homeTimeouts: 0,
    awayTimeouts: 0,
  },
};
describe('SyncService', () => {
  let db: ScorerDatabase;
  afterEach(() => db?.delete());
  async function setup() {
    db = new ScorerDatabase(`s-${crypto.randomUUID()}`);
    const repo = new MatchRepository(db);
    await repo.bootstrap(
      1,
      { ...snapshot, session: { ...snapshot.session, lastAcceptedSequence: 0 } },
      'device',
    );
    await repo.mutate(1, { type: 'PREPARE_SET', payload: {} });
    return repo;
  }
  it('accepts Applied/AlreadyAccepted and advances session', async () => {
    await setup();
    const api = {
      sync: vi.fn().mockResolvedValue({
        sheetUuid: 'sheet',
        sessionUuid: 'session',
        lastAcceptedSequence: 1,
        results: [
          { eventUuid: (await db.events.toArray())[0].eventUuid, sequence: 1, status: 'APPLIED' },
        ],
        snapshot,
      }),
      takeOver: vi.fn(),
      sheet: vi.fn(),
    };
    await new SyncService(db, api as never).sync(1);
    expect((await db.events.toArray())[0].syncStatus).toBe('ACCEPTED');
    expect((await db.sessions.get('session'))?.lastAcceptedSequence).toBe(1);
  });
  it('returns SYNCING to PENDING after timeout', async () => {
    await setup();
    const api = {
      sync: vi.fn().mockRejectedValue({ status: 0, code: 'sync_temporarily_unavailable' }),
      takeOver: vi.fn(),
      sheet: vi.fn(),
    };
    const service = new SyncService(db, api as never);
    await service.sync(1);
    expect((await db.events.toArray())[0].syncStatus).toBe('PENDING');
    expect(service.phase).toBe('IDLE');
  });
  it('blocks and preserves the causal queue on a permanent domain 400', async () => {
    await setup();
    const api = {
      sync: vi.fn().mockRejectedValue({ status: 400, code: 'substitution_player_is_libero' }),
      takeOver: vi.fn(),
      sheet: vi.fn(),
    };
    const service = new SyncService(db, api as never);
    await service.sync(1);
    expect(service.phase).toBe('BLOCKED');
    expect(service.lastError).toBe('substitution_player_is_libero');
    expect((await db.events.toArray()).map((event) => event.syncStatus)).toEqual(['PENDING']);
    expect((await db.sessions.get('session'))?.status).toBe('ABANDONED');
    await new SyncService(db, api as never).sync(1);
    expect(api.sync).toHaveBeenCalledTimes(1);
    expect(await db.events.count()).toBe(1);
  });
  it('blocks and preserves events on session loss or conflict', async () => {
    await setup();
    const api = {
      sync: vi.fn().mockRejectedValue({ status: 409, code: 'match_sheet_session_not_active' }),
      takeOver: vi.fn(),
      sheet: vi.fn(),
    };
    const service = new SyncService(db, api as never);
    await service.sync(1);
    expect(service.phase).toBe('BLOCKED');
    expect((await db.sessions.get('session'))?.status).toBe('ABANDONED');
    expect(await db.events.count()).toBe(1);
  });
});
