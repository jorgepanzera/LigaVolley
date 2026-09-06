import 'fake-indexeddb/auto';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ScorerDatabase } from '../persistence/database';
import { MatchRepository } from '../persistence/matchRepository';
import { SyncService } from './syncService';
import type { ServerSheetSnapshot } from '../domain/types';
import { applyCommand } from '../domain/matchEngine';
const snapshot: ServerSheetSnapshot = {
  sheet: { matchSheetId: 1, sheetUuid: 'sheet', status: 'OPEN', openedAt: '' },
  match: { matchId: 1, status: 'SCHEDULED', homeTeamEntryId: 1, awayTeamEntryId: 2 },
  home: {
    teamName: 'H',
    players: Array.from({ length: 6 }, (_, i) => ({
      matchPlayerId: i + 1,
      jerseyNumber: i + 1,
      displayName: `H${i}`,
      isMatchCaptain: i === 0,
    })),
    liberos: [],
  },
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
  it('drains events persisted during an accepted batch without another user action', async () => {
    const repo = await setup();
    const state = (await db.snapshots.get(1))!.state;
    const command = {
      type: 'SET_LINEUP' as const,
      payload: {
        side: 'HOME',
        p1MatchPlayerId: 1,
        p2MatchPlayerId: 2,
        p3MatchPlayerId: 3,
        p4MatchPlayerId: 4,
        p5MatchPlayerId: 5,
        p6MatchPlayerId: 6,
      },
    };
    const api = {
      sync: vi.fn().mockImplementation(async (_id, body) => {
        if (body.events[0].sequence === 1) {
          await repo.mutate(1, command);
        }
        const sequence = body.events.at(-1).sequence;
        return {
          sheetUuid: 'sheet',
          sessionUuid: 'session',
          lastAcceptedSequence: sequence,
          results: body.events.map((x: any) => ({
            eventUuid: x.eventUuid,
            sequence: x.sequence,
            status: 'APPLIED',
          })),
          snapshot: {
            ...snapshot,
            operationalState: sequence === 1 ? state : applyCommand(state, command),
            session: { ...snapshot.session, lastAcceptedSequence: sequence },
          },
        };
      }),
      takeOver: vi.fn(),
      sheet: vi.fn(),
    };
    await new SyncService(db, api as never).sync(1);
    expect(api.sync).toHaveBeenCalledTimes(2);
    expect((await db.events.toArray()).map((x) => x.syncStatus)).toEqual(['ACCEPTED', 'ACCEPTED']);
  });
  it('blocks and preserves the causal queue on a permanent domain 400', async () => {
    await setup();
    const api = {
      sync: vi.fn().mockRejectedValue({
        status: 400,
        code: 'substitution_player_is_libero',
        eventUuid: 'rejected-event',
        localSequence: 1,
      }),
      takeOver: vi.fn(),
      sheet: vi.fn(),
    };
    const service = new SyncService(db, api as never);
    await service.sync(1);
    expect(service.phase).toBe('BLOCKED');
    expect(service.lastError).toBe('substitution_player_is_libero');
    expect((await db.events.toArray()).map((event) => event.syncStatus)).toEqual(['PENDING']);
    expect((await db.sessions.get('session'))?.status).toBe('ACTIVE');
    expect(JSON.parse((await db.appMeta.get('syncBlocked:1'))!.value)).toEqual({
      code: 'substitution_player_is_libero',
      eventUuid: 'rejected-event',
      localSequence: 1,
    });
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
