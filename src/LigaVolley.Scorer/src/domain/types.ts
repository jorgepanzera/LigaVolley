export type Side = 'HOME' | 'AWAY';
export type RuntimeState = 'BOOTSTRAPPING' | 'READY' | 'SYNCING' | 'OFFLINE' | 'BLOCKED' | 'CLOSED';
export type SyncStatus = 'PENDING' | 'SYNCING' | 'ACCEPTED';
export type SessionStatus = 'ACTIVE' | 'ABANDONED' | 'CLOSED';
export type EventType =
  | 'PREPARE_SET'
  | 'SET_LINEUP'
  | 'START_SET'
  | 'POINT'
  | 'CORRECT_LAST_POINT'
  | 'SUBSTITUTION'
  | 'LIBERO_ENTER'
  | 'LIBERO_EXIT'
  | 'TIMEOUT'
  | 'MATCH_CLOSE';
export interface PlayerRef {
  matchPlayerId: number;
  isLibero?: boolean;
}
export interface Substitution {
  side: Side;
  position: number;
  playerOutMatchPlayerId: number;
  playerInMatchPlayerId: number;
}
export interface LiberoReplacement {
  side: Side;
  position: number;
  liberoMatchPlayerId: number;
  replacedMatchPlayerId: number;
  active: boolean;
}
export interface LiberoPlan {
  enabled: boolean;
  liberoMatchPlayerId?: number;
  logicalPositions: number[];
}
export interface SportingConsequence {
  kind:
    | 'POINT'
    | 'SERVICE_CHANGE'
    | 'ROTATION'
    | 'LIBERO_ENTER'
    | 'LIBERO_EXIT'
    | 'TIMEOUT'
    | 'SUBSTITUTION'
    | 'CORRECTION'
    | 'SET_FINISHED';
  side?: Side;
  playerMatchPlayerId?: number;
  replacedMatchPlayerId?: number;
  text: string;
}
export interface SetState {
  setNumber: number;
  status: 'READY' | 'IN_PROGRESS' | 'FINISHED';
  homePoints: number;
  awayPoints: number;
  initialServingSide?: Side;
  servingSide?: Side;
  homeRotationOffset: number;
  awayRotationOffset: number;
  homeTimeouts: number;
  awayTimeouts: number;
  winnerSide?: Side;
  lineups: { HOME: number[]; AWAY: number[] };
  liberoPlans: { HOME: LiberoPlan; AWAY: LiberoPlan };
  substitutions: Substitution[];
  liberoReplacements: LiberoReplacement[];
  points: Side[];
  lastSportingEvent?: EventType;
  lastConsequences: SportingConsequence[];
}
export interface MatchState {
  status: 'OPEN' | 'IN_PROGRESS' | 'CLOSED';
  homeSets: number;
  awaySets: number;
  currentSetNumber?: number;
  sets: SetState[];
  matchDecided: boolean;
  closed: boolean;
  closeConfirmed: boolean;
}
export interface LocalEvent {
  eventUuid: string;
  matchId: number;
  sheetUuid: string;
  sessionUuid: string;
  sequence: number;
  type: EventType;
  payload: Record<string, unknown>;
  occurredAt: string;
  syncStatus: SyncStatus;
  createdAt: string;
}
export interface SessionRecord {
  sessionUuid: string;
  matchId: number;
  sheetUuid: string;
  deviceId: string;
  status: SessionStatus;
  lastAcceptedSequence: number;
  nextLocalSequence: number;
  startedAt: string;
  endedAt?: string;
}
export interface MatchSheetRecord {
  matchId: number;
  sheetUuid: string;
  status: string;
  bootstrap: ServerSheetSnapshot;
  updatedAt: string;
}
export interface SnapshotRecord {
  matchId: number;
  sheetUuid: string;
  sessionUuid: string;
  basedOnAcceptedSequence: number;
  state: MatchState;
  updatedAt: string;
}
export interface ServerSheetSnapshot {
  sheet: { matchSheetId: number; sheetUuid: string; status: string; openedAt: string };
  match: { matchId: number; status: string; homeTeamEntryId: number; awayTeamEntryId: number };
  competition?: {
    competitionId: number;
    competitionName: string;
    season: string;
    division: string;
    phase: string;
  };
  home: {
    teamName: string;
    players: Array<{
      matchPlayerId: number;
      jerseyNumber?: number;
      isMatchCaptain: boolean;
      displayName: string;
      role?: string;
    }>;
    liberos: Array<{ matchPlayerId: number }>;
  };
  away: {
    teamName: string;
    players: Array<{
      matchPlayerId: number;
      jerseyNumber?: number;
      isMatchCaptain: boolean;
      displayName: string;
      role?: string;
    }>;
    liberos: Array<{ matchPlayerId: number }>;
  };
  session: {
    sessionUuid: string;
    deviceId: string;
    status: SessionStatus;
    lastAcceptedSequence: number;
    startedAt: string;
    endedAt?: string;
  };
  currentState: {
    currentSetNumber?: number;
    homeSets: number;
    awaySets: number;
    homePoints: number;
    awayPoints: number;
    servingSide?: Side;
    homeRotationOffset: number;
    awayRotationOffset: number;
    homeTimeouts: number;
    awayTimeouts: number;
  };
  operationalState?: MatchState;
  trackSubstitutions?: boolean;
  trackLiberoReplacements?: boolean;
}
export type MatchCommand = { type: EventType; payload: Record<string, unknown> };
export const initialState = (): MatchState => ({
  status: 'OPEN',
  homeSets: 0,
  awaySets: 0,
  sets: [],
  matchDecided: false,
  closed: false,
  closeConfirmed: false,
});
