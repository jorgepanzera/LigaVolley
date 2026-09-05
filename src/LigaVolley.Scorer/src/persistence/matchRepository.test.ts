import 'fake-indexeddb/auto';
import { afterEach, describe, expect, it } from 'vitest';
import { ScorerDatabase } from './database';
import { MatchRepository } from './matchRepository';
import type { ServerSheetSnapshot } from '../domain/types';
const server = (id = 1): ServerSheetSnapshot => ({
  sheet: {
    matchSheetId: 1,
    sheetUuid: 'sheet',
    status: 'OPEN',
    openedAt: new Date().toISOString(),
  },
  match: { matchId: id, status: 'SCHEDULED', homeTeamEntryId: 1, awayTeamEntryId: 2 },
  home: {
    teamName: 'H',
    players: Array.from({ length: 6 }, (_, i) => ({
      matchPlayerId: i + 1,
      jerseyNumber: i + 1,
      isMatchCaptain: i === 0,
      displayName: `H${i}`,
    })),
    liberos: [],
  },
  away: {
    teamName: 'A',
    players: Array.from({ length: 6 }, (_, i) => ({
      matchPlayerId: i + 11,
      jerseyNumber: i + 1,
      isMatchCaptain: i === 0,
      displayName: `A${i}`,
    })),
    liberos: [],
  },
  session: {
    sessionUuid: 'session',
    deviceId: 'device',
    status: 'ACTIVE',
    lastAcceptedSequence: 0,
    startedAt: new Date().toISOString(),
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
});
describe('Dexie repository', () => {
  let db: ScorerDatabase;
  afterEach(() => db?.delete());
  it('bootstraps all records and reenters', async () => {
    db = new ScorerDatabase(`t-${crypto.randomUUID()}`);
    const repo = new MatchRepository(db);
    await repo.bootstrap(1, server(), 'device');
    const local = await repo.active(1);
    expect(local?.session.nextLocalSequence).toBe(1);
    expect(local?.sheet.bootstrap.home.players).toHaveLength(6);
  });
  it('commits event, snapshot and next sequence atomically', async () => {
    db = new ScorerDatabase(`t-${crypto.randomUUID()}`);
    const repo = new MatchRepository(db);
    await repo.bootstrap(1, server(), 'device');
    await repo.mutate(1, { type: 'PREPARE_SET', payload: {} });
    expect(await db.events.count()).toBe(1);
    expect((await db.sessions.get('session'))?.nextLocalSequence).toBe(2);
    expect((await db.snapshots.get(1))?.state.currentSetNumber).toBe(1);
  });
  it('resets interrupted SYNCING events', async () => {
    db = new ScorerDatabase(`t-${crypto.randomUUID()}`);
    const repo = new MatchRepository(db);
    await repo.bootstrap(1, server(), 'device');
    await repo.mutate(1, { type: 'PREPARE_SET', payload: {} });
    await db.events.toCollection().modify({ syncStatus: 'SYNCING' });
    await repo.resetSyncing();
    expect((await db.events.toArray())[0].syncStatus).toBe('PENDING');
  });
  it('does not persist an invalid normal substitution involving a declared libero', async () => {
    db = new ScorerDatabase(`t-${crypto.randomUUID()}`);
    const repo = new MatchRepository(db);
    const snapshot = server();
    snapshot.home.liberos = [{ matchPlayerId: 88 }];
    await repo.bootstrap(1, snapshot, 'device');
    await repo.mutate(1, { type: 'PREPARE_SET', payload: {} });
    for (const side of ['HOME', 'AWAY'] as const)
      await repo.mutate(1, {
        type: 'SET_LINEUP',
        payload: {
          side,
          ...Object.fromEntries(
            [1, 2, 3, 4, 5, 6].map((position, index) => [
              `p${position}MatchPlayerId`,
              (side === 'HOME' ? 1 : 11) + index,
            ]),
          ),
        },
      });
    await repo.mutate(1, { type: 'START_SET', payload: { initialServingSide: 'HOME' } });
    const before = await db.events.count();
    await expect(
      repo.mutate(1, {
        type: 'SUBSTITUTION',
        payload: { side: 'HOME', playerOutMatchPlayerId: 1, playerInMatchPlayerId: 88 },
      }),
    ).rejects.toThrow('substitution_player_is_libero');
    expect(await db.events.count()).toBe(before);
    expect((await db.snapshots.get(1))?.state.sets[0].substitutions).toHaveLength(0);
  });
});
