import type { EventType, MatchCommand, MatchState, SetState, Side } from './types';
const copy = (s: MatchState): MatchState => structuredClone(s);
const current = (s: MatchState) => {
  const set = s.sets.find((x) => x.setNumber === s.currentSetNumber);
  if (!set) throw new Error('match_set_not_found');
  return set;
};
const side = (p: Record<string, unknown>, key = 'side') => String(p[key]).toUpperCase() as Side;
const num = (p: Record<string, unknown>, key: string) => Number(p[key]);
export function applyCommand(source: MatchState, command: MatchCommand): MatchState {
  if (source.closed) throw new Error('match_closed');
  const state = copy(source);
  switch (command.type) {
    case 'PREPARE_SET':
      prepare(state);
      break;
    case 'SET_LINEUP':
      lineup(state, command.payload);
      break;
    case 'START_SET':
      start(state, command.payload);
      break;
    case 'POINT':
      point(state, side(command.payload, 'winningSide'));
      break;
    case 'CORRECT_LAST_POINT':
      correct(state);
      break;
    case 'SUBSTITUTION':
      substitute(state, command.payload);
      break;
    case 'LIBERO_ENTER':
      liberoEnter(state, command.payload);
      break;
    case 'LIBERO_EXIT':
      liberoExit(state, command.payload);
      break;
    case 'TIMEOUT':
      timeout(state, side(command.payload));
      break;
    case 'MATCH_CLOSE':
      close(state);
      break;
    default:
      throw new Error('sync_invalid_event_type');
  }
  return state;
}
function prepare(s: MatchState) {
  if (s.matchDecided) throw new Error('match_already_decided');
  if (s.sets.some((x) => x.status !== 'FINISHED')) throw new Error('match_set_invalid_state');
  const n = s.sets.length + 1;
  if (n > 5) throw new Error('match_already_decided');
  s.sets.push({
    setNumber: n,
    status: 'READY',
    homePoints: 0,
    awayPoints: 0,
    homeRotationOffset: 0,
    awayRotationOffset: 0,
    homeTimeouts: 0,
    awayTimeouts: 0,
    lineups: { HOME: [], AWAY: [] },
    substitutions: [],
    liberoReplacements: [],
    points: [],
  });
  s.currentSetNumber = n;
}
function lineup(s: MatchState, p: Record<string, unknown>) {
  const set = current(s);
  if (set.status !== 'READY') throw new Error('lineup_locked');
  const team = side(p);
  const ids = [1, 2, 3, 4, 5, 6].map((i) => num(p, `p${i}MatchPlayerId`));
  if (new Set(ids).size !== 6 || ids.some((x) => !x)) throw new Error('invalid_lineup');
  set.lineups[team] = ids;
}
function start(s: MatchState, p: Record<string, unknown>) {
  const set = current(s);
  if (set.status !== 'READY' || set.lineups.HOME.length !== 6 || set.lineups.AWAY.length !== 6)
    throw new Error('match_set_invalid_state');
  set.initialServingSide = side(p, 'initialServingSide');
  set.servingSide = set.initialServingSide;
  set.status = 'IN_PROGRESS';
  s.status = 'IN_PROGRESS';
}
function point(s: MatchState, winner: Side) {
  const set = current(s);
  if (set.status !== 'IN_PROGRESS') throw new Error('match_set_invalid_state');
  if (set.servingSide !== winner) {
    if (winner === 'HOME') set.homeRotationOffset = (set.homeRotationOffset + 1) % 6;
    else set.awayRotationOffset = (set.awayRotationOffset + 1) % 6;
  }
  set.servingSide = winner;
  if (winner === 'HOME') set.homePoints++;
  else set.awayPoints++;
  set.points.push(winner);
  set.lastSportingEvent = 'POINT';
  const target = set.setNumber === 5 ? 15 : 25;
  if (
    Math.max(set.homePoints, set.awayPoints) >= target &&
    Math.abs(set.homePoints - set.awayPoints) >= 2
  ) {
    set.status = 'FINISHED';
    set.winnerSide = set.homePoints > set.awayPoints ? 'HOME' : 'AWAY';
    if (set.winnerSide === 'HOME') s.homeSets++;
    else s.awaySets++;
    s.matchDecided = s.homeSets === 3 || s.awaySets === 3;
  }
}
function rebuildPoints(set: SetState) {
  set.homePoints = 0;
  set.awayPoints = 0;
  set.homeRotationOffset = 0;
  set.awayRotationOffset = 0;
  set.servingSide = set.initialServingSide;
  for (const winner of set.points) {
    if (set.servingSide !== winner) {
      if (winner === 'HOME') set.homeRotationOffset = (set.homeRotationOffset + 1) % 6;
      else set.awayRotationOffset = (set.awayRotationOffset + 1) % 6;
    }
    set.servingSide = winner;
    if (winner === 'HOME') set.homePoints++;
    else set.awayPoints++;
  }
  set.status = 'IN_PROGRESS';
  set.winnerSide = undefined;
}
function correct(s: MatchState) {
  const set = current(s);
  if (set.lastSportingEvent !== 'POINT' || !set.points.length)
    throw new Error('point_not_last_effective_event');
  const was = set.winnerSide;
  if (was === 'HOME') s.homeSets--;
  if (was === 'AWAY') s.awaySets--;
  set.points.pop();
  rebuildPoints(set);
  s.matchDecided = s.homeSets === 3 || s.awaySets === 3;
  set.lastSportingEvent = 'CORRECT_LAST_POINT';
}
function effective(set: SetState, team: Side) {
  const players = [...set.lineups[team]];
  for (const x of set.substitutions.filter((x) => x.side === team))
    if (players[x.position] === x.playerOutMatchPlayerId)
      players[x.position] = x.playerInMatchPlayerId;
  for (const x of set.liberoReplacements.filter((x) => x.side === team && x.active))
    players[x.position] = x.liberoMatchPlayerId;
  return players;
}
function substitute(s: MatchState, p: Record<string, unknown>) {
  const set = current(s),
    team = side(p),
    out = num(p, 'playerOutMatchPlayerId'),
    into = num(p, 'playerInMatchPlayerId'),
    court = effective(set, team),
    position = court.indexOf(out);
  if (set.status !== 'IN_PROGRESS' || position < 0 || court.includes(into))
    throw new Error('invalid_substitution');
  set.substitutions.push({
    side: team,
    position,
    playerOutMatchPlayerId: out,
    playerInMatchPlayerId: into,
  });
  set.lastSportingEvent = 'SUBSTITUTION';
}
function liberoEnter(s: MatchState, p: Record<string, unknown>) {
  const set = current(s),
    team = side(p),
    libero = num(p, 'liberoMatchPlayerId'),
    replaced = num(p, 'replacedMatchPlayerId'),
    position = effective(set, team).indexOf(replaced);
  const offset = team === 'HOME' ? set.homeRotationOffset : set.awayRotationOffset,
    physical = ((((position - offset) % 6) + 6) % 6) + 1;
  if (position < 0 || ![1, 5, 6].includes(physical)) throw new Error('libero_not_back_row');
  set.liberoReplacements.push({
    side: team,
    position,
    liberoMatchPlayerId: libero,
    replacedMatchPlayerId: replaced,
    active: true,
  });
  set.lastSportingEvent = 'LIBERO_ENTER';
}
function liberoExit(s: MatchState, p: Record<string, unknown>) {
  const set = current(s),
    team = side(p),
    libero = num(p, 'liberoMatchPlayerId'),
    x = set.liberoReplacements.find(
      (x) => x.side === team && x.liberoMatchPlayerId === libero && x.active,
    );
  if (!x) throw new Error('invalid_libero_replacement');
  x.active = false;
  set.lastSportingEvent = 'LIBERO_EXIT';
}
function timeout(s: MatchState, team: Side) {
  const set = current(s),
    key = team === 'HOME' ? 'homeTimeouts' : 'awayTimeouts';
  if (set.status !== 'IN_PROGRESS' || set[key] >= 2) throw new Error('timeout_limit_reached');
  set[key]++;
  set.lastSportingEvent = 'TIMEOUT';
}
function close(s: MatchState) {
  if (!s.matchDecided) throw new Error('match_not_decided');
  s.closed = true;
  s.status = 'CLOSED';
}
export function replay(
  base: MatchState,
  events: Array<{ type: EventType; payload: Record<string, unknown> }>,
) {
  return events.reduce((s, e) => applyCommand(s, e), base);
}
export function serverPlayer(set: SetState, team: Side) {
  const players = effective(set, team),
    offset = team === 'HOME' ? set.homeRotationOffset : set.awayRotationOffset;
  return players[((offset % 6) + 6) % 6];
}
