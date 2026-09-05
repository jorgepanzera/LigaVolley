import 'fake-indexeddb/auto';
import { afterEach, describe, expect, it } from 'vitest';
import { ScorerDatabase } from './database';
import { MatchRepository } from './matchRepository';
import type { ServerSheetSnapshot } from '../domain/types';
import { initialState } from '../domain/types';
import { reconcile } from '../sync/reconciliationService';
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
  it('preserves declared candidates and the pending set selection across IndexedDB reentry and reconciliation', async () => {
    const name = `libero-${crypto.randomUUID()}`;
    db = new ScorerDatabase(name);
    const snapshot = server();
    snapshot.home.players.push({ matchPlayerId: 88, jerseyNumber: 42, isMatchCaptain: false, displayName: 'Libero' });
    snapshot.home.liberos = [{ matchPlayerId: 88 }];
    snapshot.operationalState = initialState();
    const repo = new MatchRepository(db);
    await repo.bootstrap(1, snapshot, 'device');
    await repo.mutate(1, { type: 'PREPARE_SET', payload: {} });
    await repo.mutate(1, { type: 'SET_LINEUP', payload: {
      side: 'HOME', setNumber: 1, p1MatchPlayerId: 1, p2MatchPlayerId: 2, p3MatchPlayerId: 3,
      p4MatchPlayerId: 4, p5MatchPlayerId: 5, p6MatchPlayerId: 6,
      liberoMatchPlayerId: 88, liberoLogicalPositions: [0],
    } });
    const before = await repo.active(1);
    db.close();
    db = new ScorerDatabase(name);
    const reentered = await new MatchRepository(db).active(1);
    expect(reentered?.sheet.bootstrap.home.liberos).toEqual([{ matchPlayerId: 88 }]);
    expect(reentered?.snapshot.state.declaredLiberoMatchPlayerIds).toEqual({ HOME: [88], AWAY: [] });
    expect(reentered?.snapshot.state.sets[0].liberoPlans.HOME).toEqual({ enabled: true, liberoMatchPlayerId: 88, logicalPositions: [0] });
    await reconcile(db, 1, structuredClone(snapshot), 0);
    const reconciled = await new MatchRepository(db).active(1);
    expect(reconciled?.snapshot.state.sets[0].liberoPlans).toEqual(before?.snapshot.state.sets[0].liberoPlans);
    expect(reconciled?.snapshot.state.declaredLiberoMatchPlayerIds).toEqual(before?.snapshot.state.declaredLiberoMatchPlayerIds);
    expect(reconciled?.sheet.bootstrap.home.liberos).toEqual(snapshot.home.liberos);
    // The same selection also survives a GET /sheet containing the accepted operational plan.
    const accepted = structuredClone(snapshot);
    accepted.operationalState = structuredClone(reconciled!.snapshot.state);
    accepted.session.lastAcceptedSequence = 2;
    await reconcile(db, 1, accepted, 2);
    expect((await new MatchRepository(db).active(1))?.snapshot.state.sets[0].liberoPlans.HOME.liberoMatchPlayerId).toBe(88);
  });
  it('never manufactures candidates from a player role when the frozen declaration is empty', async () => {
    db = new ScorerDatabase(`libero-empty-${crypto.randomUUID()}`);
    const snapshot = server();
    snapshot.home.players[4] = { ...snapshot.home.players[4], role: 'Libero' };
    const repo = new MatchRepository(db);
    await repo.bootstrap(1, snapshot, 'device');
    await reconcile(db, 1, snapshot, 0);
    expect((await repo.active(1))?.snapshot.state.declaredLiberoMatchPlayerIds).toEqual({ HOME: [], AWAY: [] });
  });
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
