import type { ScorerDatabase } from './database';
import {
  initialState,
  type MatchCommand,
  type MatchState,
  type ServerSheetSnapshot,
  type SessionRecord,
} from '../domain/types';
import { applyCommand } from '../domain/matchEngine';
export class MatchRepository {
  constructor(private database: ScorerDatabase) {}
  async bootstrap(matchId: number, server: ServerSheetSnapshot, device: string) {
    const state = normalizeState(server.operationalState ?? fromServer(server), server);
    const session: SessionRecord = {
      ...server.session,
      status: String(server.session.status).toUpperCase() as SessionRecord['status'],
      matchId,
      sheetUuid: server.sheet.sheetUuid,
      deviceId: server.session.deviceId || device,
      nextLocalSequence: server.session.lastAcceptedSequence + 1,
    };
    await this.database.transaction(
      'rw',
      this.database.matchSheets,
      this.database.sessions,
      this.database.snapshots,
      async () => {
        await this.database.matchSheets.put({
          matchId,
          sheetUuid: server.sheet.sheetUuid,
          status: server.sheet.status,
          bootstrap: server,
          updatedAt: new Date().toISOString(),
        });
        await this.database.sessions.put(session);
        await this.database.snapshots.put({
          matchId,
          sheetUuid: server.sheet.sheetUuid,
          sessionUuid: session.sessionUuid,
          basedOnAcceptedSequence: session.lastAcceptedSequence,
          state,
          updatedAt: new Date().toISOString(),
        });
      },
    );
    return state;
  }
  async active(matchId: number) {
    const sheet = await this.database.matchSheets.get(matchId);
    if (!sheet) return;
    const sessions = await this.database.sessions.where('matchId').equals(matchId).toArray();
    const session =
      sessions.find((x) => x.status === 'ACTIVE') ??
      sessions.sort((a, b) => b.startedAt.localeCompare(a.startedAt))[0];
    const snapshot = await this.database.snapshots.get(matchId);
    if (snapshot) snapshot.state = normalizeState(snapshot.state, sheet.bootstrap);
    return sheet && session && snapshot ? { sheet, session, snapshot } : undefined;
  }
  async mutate(matchId: number, command: MatchCommand) {
    return this.database.transaction(
      'rw',
      this.database.matchSheets,
      this.database.sessions,
      this.database.snapshots,
      this.database.events,
      async () => {
        const local = await this.active(matchId);
        if (!local || local.session.status !== 'ACTIVE') throw new Error('session_lost');
        if (local.snapshot.state.closed) throw new Error('match_closed');
        const state = applyCommand(local.snapshot.state, command),
          sequence = local.session.nextLocalSequence,
          eventUuid = crypto.randomUUID(),
          now = new Date().toISOString();
        await this.database.events.add({
          eventUuid,
          matchId,
          sheetUuid: local.sheet.sheetUuid,
          sessionUuid: local.session.sessionUuid,
          sequence,
          type: command.type,
          payload: command.payload,
          occurredAt: now,
          syncStatus: 'PENDING',
          createdAt: now,
        });
        await this.database.snapshots.update(matchId, { state, updatedAt: now });
        await this.database.sessions.update(local.session.sessionUuid, {
          nextLocalSequence: sequence + 1,
        });
        return state;
      },
    );
  }
  async pending(sessionUuid: string) {
    return this.database.events
      .where('[sessionUuid+syncStatus]')
      .equals([sessionUuid, 'PENDING'])
      .sortBy('sequence');
  }
  async resetSyncing() {
    await this.database.events
      .toCollection()
      .filter((x) => x.syncStatus === 'SYNCING')
      .modify({ syncStatus: 'PENDING' });
  }
  async recoverBeforeRejected(
    matchId: number,
    rejected: { code: string; eventUuid: string; localSequence: number },
  ) {
    return this.database.transaction(
      'rw',
      this.database.matchSheets,
      this.database.sessions,
      this.database.snapshots,
      this.database.events,
      async () => {
        const local = await this.active(matchId);
        if (!local || local.session.status !== 'ACTIVE') throw new Error('session_lost');
        const events = await this.database.events
          .where('[sessionUuid+sequence]')
          .between(
            [local.session.sessionUuid, local.session.lastAcceptedSequence + 1],
            [local.session.sessionUuid, Number.MAX_SAFE_INTEGER],
          )
          .sortBy('sequence');
        const index = events.findIndex(
          (event) =>
            event.eventUuid === rejected.eventUuid && event.sequence === rejected.localSequence,
        );
        if (index < 0) throw new Error('local_recovery_event_not_found');

        let state = normalizeState(
          structuredClone(
            local.sheet.bootstrap.operationalState ?? fromServer(local.sheet.bootstrap),
          ),
          local.sheet.bootstrap,
        );
        for (const event of events.slice(0, index))
          state = applyCommand(state, { type: event.type, payload: event.payload });

        try {
          applyCommand(state, { type: events[index].type, payload: events[index].payload });
        } catch (error) {
          if (error instanceof Error && error.message === rejected.code) {
            await this.database.snapshots.update(matchId, {
              state,
              updatedAt: new Date().toISOString(),
            });
            return state;
          }
          throw new Error('local_recovery_not_deterministic');
        }
        throw new Error('local_recovery_not_reproducible');
      },
    );
  }
}
export function fromServer(s: ServerSheetSnapshot): MatchState {
  const state = initialState(),
    closed = String(s.sheet.status).toUpperCase() === 'CLOSED';
  state.status = closed ? 'CLOSED' : s.currentState.currentSetNumber ? 'IN_PROGRESS' : 'OPEN';
  state.homeSets = s.currentState.homeSets;
  state.awaySets = s.currentState.awaySets;
  state.currentSetNumber = s.currentState.currentSetNumber;
  state.matchDecided = state.homeSets === 3 || state.awaySets === 3;
  state.closed = closed;
  state.closeConfirmed = closed;
  if (s.currentState.currentSetNumber)
    state.sets.push({
      setNumber: s.currentState.currentSetNumber,
      status: state.matchDecided ? 'FINISHED' : 'IN_PROGRESS',
      homePoints: s.currentState.homePoints,
      awayPoints: s.currentState.awayPoints,
      servingSide: s.currentState.servingSide
        ? (String(s.currentState.servingSide).toUpperCase() as 'HOME' | 'AWAY')
        : undefined,
      homeRotationOffset: s.currentState.homeRotationOffset,
      awayRotationOffset: s.currentState.awayRotationOffset,
      homeTimeouts: s.currentState.homeTimeouts,
      awayTimeouts: s.currentState.awayTimeouts,
      lineups: { HOME: [], AWAY: [] },
      liberoPlans: {
        HOME: { enabled: false, logicalPositions: [] },
        AWAY: { enabled: false, logicalPositions: [] },
      },
      substitutions: [],
      liberoReplacements: [],
      points: [],
      lastConsequences: [],
    });
  return state;
}

export function normalizeState(state: MatchState, server?: ServerSheetSnapshot): MatchState {
  if (server)
    state.declaredLiberoMatchPlayerIds = {
      HOME: server.home.liberos.map((x) => x.matchPlayerId),
      AWAY: server.away.liberos.map((x) => x.matchPlayerId),
    };
  else state.declaredLiberoMatchPlayerIds ??= { HOME: [], AWAY: [] };
  for (const set of state.sets) {
    set.liberoPlans ??= {
      HOME: { enabled: false, logicalPositions: [] },
      AWAY: { enabled: false, logicalPositions: [] },
    };
    set.lastConsequences ??= [];
  }
  return state;
}
