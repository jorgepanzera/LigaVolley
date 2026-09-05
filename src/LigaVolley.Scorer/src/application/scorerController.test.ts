import 'fake-indexeddb/auto';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiProblem, type OpenMatchContext } from '../api/scorerApi';
import type { ServerSheetSnapshot } from '../domain/types';
import { ScorerDatabase } from '../persistence/database';
import { MatchRepository } from '../persistence/matchRepository';
import { ScorerController } from './scorerController';

const snapshot = (matchId = 1): ServerSheetSnapshot => ({
  sheet: {
    matchSheetId: 1,
    sheetUuid: 'sheet',
    status: 'OPEN',
    openedAt: new Date().toISOString(),
  },
  match: { matchId, status: 'SCHEDULED', homeTeamEntryId: 1, awayTeamEntryId: 2 },
  home: {
    teamName: 'HOME',
    players: Array.from({ length: 6 }, (_, i) => ({
      matchPlayerId: i + 1,
      jerseyNumber: i + 1,
      isMatchCaptain: i === 0,
      displayName: `H${i}`,
    })),
    liberos: [],
  },
  away: {
    teamName: 'AWAY',
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

const context: OpenMatchContext = {
  match: { matchId: 1, status: 'SCHEDULED', homeTeamEntryId: 1, awayTeamEntryId: 2 },
  competition: {
    competitionId: 1,
    competitionName: 'Demo',
    season: '2026',
    division: 'Female',
    phase: 'Regular',
  },
  home: {
    teamEntryId: 1,
    teamName: 'HOME',
    competitionRosterId: 1,
    rosterStatus: 'ACTIVE',
    players: Array.from({ length: 6 }, (_, i) => ({
      competitionRosterPlayerId: i + 1,
      displayName: `H${i}`,
      role: 'Setter',
    })),
    staff: [],
  },
  away: {
    teamEntryId: 2,
    teamName: 'AWAY',
    competitionRosterId: 2,
    rosterStatus: 'ACTIVE',
    players: Array.from({ length: 6 }, (_, i) => ({
      competitionRosterPlayerId: i + 11,
      displayName: `A${i}`,
      role: 'Setter',
    })),
    staff: [],
  },
  matchOfficials: [],
  warnings: [],
};

function api(overrides: Partial<typeof import('../api/scorerApi').scorerApi> = {}) {
  return {
    sheet: vi.fn(),
    openContext: vi.fn(),
    open: vi.fn(),
    sync: vi.fn(),
    takeOver: vi.fn(),
    ...overrides,
  } as typeof import('../api/scorerApi').scorerApi;
}

describe('ScorerController bootstrap', () => {
  let database: ScorerDatabase;
  afterEach(async () => {
    await new Promise((resolve) => setTimeout(resolve, 20));
    await database?.delete();
  });

  it('falls back to opening when the sheet is specifically missing', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    const client = api({
      sheet: vi.fn().mockRejectedValue(new ApiProblem(404, 'match_sheet_not_found', 'missing')),
      openContext: vi.fn().mockResolvedValue(context),
    });
    const controller = new ScorerController(database, client);

    await controller.start(1);

    expect(controller.view.runtime).toBe('READY');
    expect(controller.view.opening).toEqual(context);
    expect(controller.view.error).toBeUndefined();
    expect(client.openContext).toHaveBeenCalledWith(1);
  });

  it('keeps other 404 responses terminal', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    const client = api({
      sheet: vi.fn().mockRejectedValue(new ApiProblem(404, 'match_not_found', 'missing')),
    });
    const controller = new ScorerController(database, client);

    await controller.start(1);

    expect(controller.view.opening).toBeUndefined();
    expect(controller.view.error).toBe('match_not_found');
    expect(client.openContext).not.toHaveBeenCalled();
  });

  it('keeps conflicts terminal instead of opening automatically', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    const client = api({
      sheet: vi.fn().mockRejectedValue(new ApiProblem(409, 'match_not_scheduled', 'conflict')),
    });
    const controller = new ScorerController(database, client);

    await controller.start(1);

    expect(controller.view.error).toBe('match_not_scheduled');
    expect(client.openContext).not.toHaveBeenCalled();
  });

  it('persists the canonical response after confirming opening', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    const client = api({
      sheet: vi.fn().mockRejectedValue(new ApiProblem(404, 'match_sheet_not_found', 'missing')),
      openContext: vi.fn().mockResolvedValue(context),
      open: vi.fn().mockResolvedValue({ alreadyOpen: false, matchSheet: snapshot() }),
    });
    const controller = new ScorerController(database, client);
    await controller.start(1);

    await controller.open({
      home: {
        players: [1, 2, 3, 4, 5, 6].map((id, i) => ({
          competitionRosterPlayerId: id,
          jerseyNumber: i + 1,
          isMatchCaptain: i === 0,
        })),
        liberoCompetitionRosterPlayerIds: [],
        competitionRosterStaffIds: [],
      },
      away: {
        players: [11, 12, 13, 14, 15, 16].map((id, i) => ({
          competitionRosterPlayerId: id,
          jerseyNumber: i + 1,
          isMatchCaptain: i === 0,
        })),
        liberoCompetitionRosterPlayerIds: [],
        competitionRosterStaffIds: [],
      },
      trackSubstitutions: true,
      trackLiberoReplacements: true,
    });

    expect(await database.matchSheets.get(1)).toBeTruthy();
    expect(controller.view.opening).toBeUndefined();
    expect(controller.view.state).toBeTruthy();
  });

  it('preserves local pending events when remote reconciliation is unavailable', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    const repo = new MatchRepository(database);
    await repo.bootstrap(1, snapshot(), 'device');
    await repo.mutate(1, { type: 'PREPARE_SET', payload: {} });
    const client = api({
      sheet: vi
        .fn()
        .mockRejectedValue(new ApiProblem(0, 'sync_temporarily_unavailable', 'offline')),
      sync: vi.fn().mockRejectedValue(new ApiProblem(0, 'sync_temporarily_unavailable', 'offline')),
    });
    const controller = new ScorerController(database, client);

    await controller.start(1);
    expect(await database.events.count()).toBe(1);
    expect((await database.events.toArray())[0].syncStatus).toBe('PENDING');
  });

  it('reconciles a local bootstrap with an existing remote sheet', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    await new MatchRepository(database).bootstrap(1, snapshot(), 'device');
    const client = api({ sheet: vi.fn().mockResolvedValue(snapshot()) });
    const controller = new ScorerController(database, client);

    await controller.start(1);

    expect(client.sheet).toHaveBeenCalledWith(1);
    expect(controller.view.opening).toBeUndefined();
    expect(controller.view.state).toBeTruthy();
  });

  it('continues from the central session and preserves the abandoned local queue', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    const repo = new MatchRepository(database);
    await repo.bootstrap(1, snapshot(), 'device');
    await repo.mutate(1, { type: 'PREPARE_SET', payload: {} });
    const central = {
      ...snapshot(),
      session: { ...snapshot().session, sessionUuid: 'central-session', deviceId: 'other' },
    };
    const next = {
      ...central,
      session: { ...central.session, sessionUuid: 'new-session', deviceId: 'device' },
    };
    const client = api({
      sheet: vi.fn().mockResolvedValue(central),
      takeOver: vi.fn().mockResolvedValue({ sessionUuid: 'new-session', snapshot: next }),
    });
    const controller = new ScorerController(database, client);
    controller.view = { ...controller.view, matchId: 1 };
    controller.syncService.phase = 'BLOCKED';
    controller.syncService.lastError = 'substitution_player_is_libero';

    await controller.continueFromCentral();

    expect(client.sheet).toHaveBeenCalledWith(1);
    expect(client.takeOver).toHaveBeenCalledWith(
      1,
      expect.objectContaining({
        sheetUuid: 'sheet',
        expectedSessionUuid: 'central-session',
      }),
    );
    expect((await database.sessions.get('session'))?.status).toBe('ABANDONED');
    expect((await database.sessions.get('new-session'))?.status).toBe('ACTIVE');
    expect(await database.events.count()).toBe(1);
    expect((await database.events.toArray())[0].sessionUuid).toBe('session');
    expect(controller.view.runtime).toBe('READY');
  });

  it('recovers only a deterministic local view and preserves the rejected causal tail', async () => {
    database = new ScorerDatabase(`controller-${crypto.randomUUID()}`);
    await new MatchRepository(database).bootstrap(1, snapshot(), 'device');
    const now = new Date().toISOString();
    await database.events.add({
      eventUuid: 'rejected-timeout',
      matchId: 1,
      sheetUuid: 'sheet',
      sessionUuid: 'session',
      sequence: 1,
      type: 'TIMEOUT',
      payload: { setNumber: 1, side: 'HOME' },
      occurredAt: now,
      syncStatus: 'PENDING',
      createdAt: now,
    });
    await database.events.add({
      eventUuid: 'descendant',
      matchId: 1,
      sheetUuid: 'sheet',
      sessionUuid: 'session',
      sequence: 2,
      type: 'PREPARE_SET',
      payload: {},
      occurredAt: now,
      syncStatus: 'PENDING',
      createdAt: now,
    });
    await database.appMeta.put({
      key: 'syncBlocked:1',
      value: JSON.stringify({
        code: 'match_set_not_found',
        eventUuid: 'rejected-timeout',
        localSequence: 1,
      }),
    });
    const controller = new ScorerController(database, api());
    controller.view = { ...controller.view, matchId: 1 };
    await controller.recoverLastValidLocal();

    expect((await database.snapshots.get(1))?.state.sets).toEqual([]);
    expect(await database.events.count()).toBe(2);
    expect((await database.sessions.get('session'))?.status).toBe('ACTIVE');
    expect(controller.view.runtime).toBe('BLOCKED');
    expect(controller.view.syncBlock?.locallyRecovered).toBe(true);
  });
});
