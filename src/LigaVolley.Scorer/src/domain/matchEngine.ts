import type {
  EventType,
  LiberoPlan,
  MatchCommand,
  MatchState,
  SetState,
  Side,
  SportingConsequence,
} from './types';
const clone = (s: MatchState): MatchState => structuredClone(s);
const parseSide = (p: Record<string, unknown>, key = 'side') =>
  String(p[key]).toUpperCase() as Side;
const numberValue = (p: Record<string, unknown>, key: string) => Number(p[key]);
const rotation = (s: SetState, side: Side) =>
  side === 'HOME' ? s.homeRotationOffset : s.awayRotationOffset;
export function currentSet(state: MatchState) {
  const set = state.sets.find((x) => x.setNumber === state.currentSetNumber);
  if (!set) throw new Error('match_set_not_found');
  return set;
}
export function applyCommand(source: MatchState, command: MatchCommand) {
  if (source.closed) throw new Error('match_closed');
  const state = clone(source);
  switch (command.type) {
    case 'PREPARE_SET':
      prepare(state);
      break;
    case 'SET_LINEUP':
      setLineup(state, command.payload);
      break;
    case 'START_SET':
      start(state, command.payload);
      break;
    case 'POINT':
      point(state, parseSide(command.payload, 'winningSide'));
      break;
    case 'CORRECT_LAST_POINT':
      correct(state);
      break;
    case 'SUBSTITUTION':
      substitute(state, command.payload);
      break;
    case 'LIBERO_ENTER':
      manualEnter(state, command.payload);
      break;
    case 'LIBERO_EXIT':
      manualExit(state, command.payload);
      break;
    case 'TIMEOUT':
      timeout(state, parseSide(command.payload));
      break;
    case 'MATCH_CLOSE':
      close(state);
      break;
    default:
      throw new Error('sync_invalid_event_type');
  }
  return state;
}
const emptyPlan = (): LiberoPlan => ({ enabled: false, logicalPositions: [] });
function prepare(state: MatchState) {
  if (state.matchDecided) throw new Error('match_already_decided');
  if (state.sets.some((x) => x.status !== 'FINISHED')) throw new Error('match_set_invalid_state');
  const setNumber = state.sets.length + 1;
  if (setNumber > 5) throw new Error('match_already_decided');
  state.sets.push({
    setNumber,
    status: 'READY',
    homePoints: 0,
    awayPoints: 0,
    homeRotationOffset: 0,
    awayRotationOffset: 0,
    homeTimeouts: 0,
    awayTimeouts: 0,
    lineups: { HOME: [], AWAY: [] },
    liberoPlans: { HOME: emptyPlan(), AWAY: emptyPlan() },
    substitutions: [],
    liberoReplacements: [],
    points: [],
    lastConsequences: [],
  });
  state.currentSetNumber = setNumber;
}
function setLineup(state: MatchState, p: Record<string, unknown>) {
  const set = currentSet(state);
  if (set.status !== 'READY') throw new Error('lineup_locked');
  const team = parseSide(p),
    players = [1, 2, 3, 4, 5, 6].map((i) => numberValue(p, `p${i}MatchPlayerId`));
  if (new Set(players).size !== 6 || players.some((x) => !x)) throw new Error('invalid_lineup');
  const libero = p.liberoMatchPlayerId ? Number(p.liberoMatchPlayerId) : undefined,
    positions = Array.isArray(p.liberoLogicalPositions) ? p.liberoLogicalPositions.map(Number) : [];
  const plan = {
    enabled: Boolean(libero && positions.length),
    liberoMatchPlayerId: libero,
    logicalPositions: [...new Set(positions)].sort(),
  } as LiberoPlan;
  validateLiberoPlan(plan, players);
  set.lineups[team] = players;
  set.liberoPlans[team] = plan;
}
export function validateLiberoPlan(plan: LiberoPlan, lineup: number[]) {
  if (!plan.enabled) return;
  if (
    !plan.liberoMatchPlayerId ||
    lineup.includes(plan.liberoMatchPlayerId) ||
    !plan.logicalPositions.length ||
    plan.logicalPositions.some((x) => x < 0 || x > 5)
  )
    throw new Error('invalid_libero_plan');
  for (let r = 0; r < 6; r++)
    for (const serving of [true, false])
      if (
        plan.logicalPositions.filter((logical) => {
          const physical = physicalPosition(logical, r);
          return physical === 5 || physical === 6 || (physical === 1 && !serving);
        }).length > 1
      )
        throw new Error('ambiguous_libero_plan');
}
function start(state: MatchState, p: Record<string, unknown>) {
  const set = currentSet(state);
  if (set.status !== 'READY' || set.lineups.HOME.length !== 6 || set.lineups.AWAY.length !== 6)
    throw new Error('match_set_invalid_state');
  set.initialServingSide = parseSide(p, 'initialServingSide');
  set.servingSide = set.initialServingSide;
  set.status = 'IN_PROGRESS';
  state.status = 'IN_PROGRESS';
  set.lastConsequences = reconcileAutomaticLiberos(set);
}
function point(state: MatchState, winner: Side) {
  const set = currentSet(state);
  if (set.status !== 'IN_PROGRESS') throw new Error('match_set_invalid_state');
  const consequences: SportingConsequence[] = [
    { kind: 'POINT', side: winner, text: `Punto ${winner}` },
  ];
  if (set.servingSide !== winner) {
    consequences.push({
      kind: 'SERVICE_CHANGE',
      side: winner,
      text: `${winner} recupera el saque`,
    });
    if (winner === 'HOME') set.homeRotationOffset = (set.homeRotationOffset + 1) % 6;
    else set.awayRotationOffset = (set.awayRotationOffset + 1) % 6;
    consequences.push({ kind: 'ROTATION', side: winner, text: `${winner} rota` });
  }
  set.servingSide = winner;
  if (winner === 'HOME') set.homePoints++;
  else set.awayPoints++;
  set.points.push(winner);
  consequences.push(...reconcileAutomaticLiberos(set));
  set.lastSportingEvent = 'POINT';
  const target = set.setNumber === 5 ? 15 : 25;
  if (
    Math.max(set.homePoints, set.awayPoints) >= target &&
    Math.abs(set.homePoints - set.awayPoints) >= 2
  ) {
    set.status = 'FINISHED';
    set.winnerSide = set.homePoints > set.awayPoints ? 'HOME' : 'AWAY';
    if (set.winnerSide === 'HOME') state.homeSets++;
    else state.awaySets++;
    state.matchDecided = state.homeSets === 3 || state.awaySets === 3;
    consequences.push({
      kind: 'SET_FINISHED',
      side: set.winnerSide,
      text: `Set ${set.setNumber} finalizado`,
    });
  }
  set.lastConsequences = consequences;
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
function correct(state: MatchState) {
  const set = currentSet(state);
  if (set.lastSportingEvent !== 'POINT' || !set.points.length)
    throw new Error('point_not_last_effective_event');
  if (set.winnerSide === 'HOME') state.homeSets--;
  if (set.winnerSide === 'AWAY') state.awaySets--;
  set.points.pop();
  rebuildPoints(set);
  set.liberoReplacements.forEach((x) => (x.active = false));
  state.matchDecided = state.homeSets === 3 || state.awaySets === 3;
  set.lastSportingEvent = 'CORRECT_LAST_POINT';
  set.lastConsequences = [
    { kind: 'CORRECTION', text: 'Último punto corregido' },
    ...reconcileAutomaticLiberos(set),
  ];
}
export function regularPlayers(set: SetState, team: Side) {
  const players = [...set.lineups[team]];
  for (const x of set.substitutions.filter((x) => x.side === team))
    if (players[x.position] === x.playerOutMatchPlayerId)
      players[x.position] = x.playerInMatchPlayerId;
  return players;
}
export function effectivePlayers(set: SetState, team: Side) {
  const players = regularPlayers(set, team);
  for (const x of set.liberoReplacements.filter((x) => x.side === team && x.active))
    players[x.position] = x.liberoMatchPlayerId;
  return players;
}
function substitute(state: MatchState, p: Record<string, unknown>) {
  const set = currentSet(state),
    team = parseSide(p),
    out = numberValue(p, 'playerOutMatchPlayerId'),
    into = numberValue(p, 'playerInMatchPlayerId'),
    regular = regularPlayers(set, team),
    position = regular.indexOf(out);
  const declaredLiberos = new Set(state.declaredLiberoMatchPlayerIds?.[team] ?? []);
  if (declaredLiberos.has(out) || declaredLiberos.has(into))
    throw new Error('substitution_player_is_libero');
  if (set.status !== 'IN_PROGRESS' || position < 0 || regular.includes(into))
    throw new Error('invalid_substitution');
  set.substitutions.push({
    side: team,
    position,
    playerOutMatchPlayerId: out,
    playerInMatchPlayerId: into,
  });
  set.lastSportingEvent = 'SUBSTITUTION';
  set.lastConsequences = [
    {
      kind: 'SUBSTITUTION',
      side: team,
      playerMatchPlayerId: into,
      replacedMatchPlayerId: out,
      text: `Sustitución ${team}`,
    },
    ...reconcileAutomaticLiberos(set),
  ];
}
function manualEnter(state: MatchState, p: Record<string, unknown>) {
  const set = currentSet(state),
    team = parseSide(p),
    libero = numberValue(p, 'liberoMatchPlayerId'),
    replaced = numberValue(p, 'replacedMatchPlayerId'),
    position = regularPlayers(set, team).indexOf(replaced),
    physical = physicalPosition(position, rotation(set, team));
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
function manualExit(state: MatchState, p: Record<string, unknown>) {
  const set = currentSet(state),
    team = parseSide(p),
    libero = numberValue(p, 'liberoMatchPlayerId'),
    active = set.liberoReplacements.find(
      (x) => x.side === team && x.liberoMatchPlayerId === libero && x.active,
    );
  if (!active) throw new Error('invalid_libero_replacement');
  active.active = false;
  set.lastSportingEvent = 'LIBERO_EXIT';
}
export function reconcileAutomaticLiberos(set: SetState) {
  const consequences: SportingConsequence[] = [];
  for (const team of ['HOME', 'AWAY'] as Side[]) {
    const plan = set.liberoPlans[team];
    if (!plan?.enabled || !plan.liberoMatchPlayerId) continue;
    const desired = plan.logicalPositions.filter((logical) => {
      const physical = physicalPosition(logical, rotation(set, team));
      return physical === 5 || physical === 6 || (physical === 1 && set.servingSide !== team);
    });
    if (desired.length > 1) throw new Error('ambiguous_libero_plan');
    const active = set.liberoReplacements.find((x) => x.side === team && x.active),
      position = desired[0];
    if (active && active.position !== position) {
      active.active = false;
      consequences.push({
        kind: 'LIBERO_EXIT',
        side: team,
        playerMatchPlayerId: regularPlayers(set, team)[active.position],
        replacedMatchPlayerId: active.liberoMatchPlayerId,
        text: `Líbero ${team} sale`,
      });
    }
    if (position !== undefined && (!active || !active.active)) {
      const regular = regularPlayers(set, team)[position];
      set.liberoReplacements.push({
        side: team,
        position,
        liberoMatchPlayerId: plan.liberoMatchPlayerId,
        replacedMatchPlayerId: regular,
        active: true,
      });
      consequences.push({
        kind: 'LIBERO_ENTER',
        side: team,
        playerMatchPlayerId: plan.liberoMatchPlayerId,
        replacedMatchPlayerId: regular,
        text: `Líbero ${team} entra`,
      });
    }
  }
  return consequences;
}
function timeout(state: MatchState, team: Side) {
  const set = currentSet(state),
    key = team === 'HOME' ? 'homeTimeouts' : 'awayTimeouts';
  if (set.status !== 'IN_PROGRESS' || set[key] >= 2) throw new Error('timeout_limit_reached');
  set[key]++;
  set.lastSportingEvent = 'TIMEOUT';
  set.lastConsequences = [{ kind: 'TIMEOUT', side: team, text: `Timeout ${team}` }];
}
function close(state: MatchState) {
  if (!state.matchDecided) throw new Error('match_not_decided');
  state.closed = true;
  state.status = 'CLOSED';
}
export function replay(
  base: MatchState,
  events: Array<{ type: EventType; payload: Record<string, unknown> }>,
) {
  return events.reduce((state, event) => applyCommand(state, event), base);
}
export function physicalPosition(logicalIndex: number, rotationOffset: number) {
  return ((((logicalIndex - rotationOffset) % 6) + 6) % 6) + 1;
}
export function logicalAtPhysical(physical: number, rotationOffset: number) {
  return (((physical - 1 + rotationOffset) % 6) + 6) % 6;
}
export function serverPlayer(set: SetState, team: Side) {
  return regularPlayers(set, team)[logicalAtPhysical(1, rotation(set, team))];
}
