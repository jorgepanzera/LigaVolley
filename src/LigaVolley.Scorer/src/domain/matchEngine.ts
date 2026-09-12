import { evaluateCommand, assertEvaluation } from './rulesAssistant';
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
export function applyCommand(source: MatchState, command: MatchCommand, origin: 'DIRECT' | 'PERSISTED' = 'DIRECT') {
  assertEvaluation(evaluateCommand(source, command), command, origin);
  if (source.closed) throw new Error('match_closed');
  const state = clone(source);
  const legacyAutomatic = origin === 'PERSISTED' && (source.rulesSnapshot?.rulesProtocolVersion ?? 1) < 2 && command.payload.observedLiberoReplacements !== true;
  switch (command.type) {
    case 'PREPARE_SET':
      prepare(state);
      break;
    case 'SET_LINEUP':
      setLineup(state, command.payload);
      break;
    case 'START_SET':
      start(state, command.payload, legacyAutomatic);
      break;
    case 'POINT':
      point(state, parseSide(command.payload, 'winningSide'), legacyAutomatic);
      break;
    case 'CORRECT_LAST_POINT':
      correct(state, legacyAutomatic);
      break;
    case 'SUBSTITUTION_REQUEST':
    case 'SUBSTITUTION':
      substitute(state, command);
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
    case 'SANCTION':
      sanction(state, command.payload, legacyAutomatic);
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
    enabled: state.trackLiberoReplacements !== false && Boolean(libero && positions.length),
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
    plan.logicalPositions.some((x) => !Number.isInteger(x) || x < 0 || x > 5)
  )
    throw new Error('invalid_libero_plan');

}
function start(state: MatchState, p: Record<string, unknown>, legacyAutomatic: boolean) {
  const set = currentSet(state);
  if (set.status !== 'READY' || set.lineups.HOME.length !== 6 || set.lineups.AWAY.length !== 6)
    throw new Error('match_set_invalid_state');
  set.initialServingSide = parseSide(p, 'initialServingSide');
  set.servingSide = set.initialServingSide;
  set.status = 'IN_PROGRESS';
  state.status = 'IN_PROGRESS';
  set.lastConsequences = legacyAutomatic ? reconcileAutomaticLiberos(set) : [];
}
function point(state: MatchState, winner: Side, legacyAutomatic: boolean) {
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
  if (legacyAutomatic) consequences.push(...reconcileAutomaticLiberos(set));
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
  const courtChange = state.rulesSnapshot?.decidingSetCourtChangePoint ?? 8;
  if (set.setNumber === 5 && Math.max(set.homePoints, set.awayPoints) === courtChange &&
      Math.max(set.homePoints - (winner === 'HOME' ? 1 : 0), set.awayPoints - (winner === 'AWAY' ? 1 : 0)) < courtChange)
    consequences.push({ kind: 'REMINDER', text: 'Cambio de campo' });
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
function correct(state: MatchState, legacyAutomatic: boolean) {
  const set = currentSet(state);
  if (set.lastSportingEvent !== 'POINT' || !set.points.length)
    throw new Error('point_not_last_effective_event');
  if (set.winnerSide === 'HOME') state.homeSets--;
  if (set.winnerSide === 'AWAY') state.awaySets--;
  set.points.pop();
  rebuildPoints(set);
  if (legacyAutomatic) set.liberoReplacements.filter(x => x.automatic !== false).forEach((x) => (x.active = false));
  state.matchDecided = state.homeSets === 3 || state.awaySets === 3;
  set.lastSportingEvent = 'CORRECT_LAST_POINT';
  set.lastConsequences = [
    { kind: 'CORRECTION', text: 'Último punto corregido' },
    ...(legacyAutomatic ? reconcileAutomaticLiberos(set) : []),
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
function substitute(state: MatchState, command: MatchCommand) {
  const set = currentSet(state), team = parseSide(command.payload), regular = regularPlayers(set, team);
  const pairs = command.type === 'SUBSTITUTION' ? [command.payload] : command.payload.replacements as Record<string, unknown>[];
  for (const pair of pairs) set.substitutions.push({ side: team, position: regular.indexOf(Number(pair.playerOutMatchPlayerId)), playerOutMatchPlayerId: Number(pair.playerOutMatchPlayerId), playerInMatchPlayerId: Number(pair.playerInMatchPlayerId) });
  set.lastSportingEvent = command.type;
  set.lastConsequences = [{ kind: 'SUBSTITUTION', side: team, text: `Sustitucion ${team}` }];
}
function manualEnter(state: MatchState, p: Record<string, unknown>) {
  const set = currentSet(state),
    team = parseSide(p),
    libero = numberValue(p, 'liberoMatchPlayerId'),
    replaced = numberValue(p, 'replacedMatchPlayerId'),
    position = effectivePlayers(set, team).indexOf(replaced),
    physical = physicalPosition(position, rotation(set, team));
  set.liberoReplacements.filter(x => x.side === team && x.active && x.position === position).forEach(x => { x.active = false; });
  const regular = regularPlayers(set, team)[position];
  set.lastLiberoRally ??= {}; set.lastLiberoRegular ??= {};
  set.lastLiberoRally[team] = set.points.length; set.lastLiberoRegular[team] = regular;
  set.liberoReplacements.push({
    side: team,
    position,
    liberoMatchPlayerId: libero,
    replacedMatchPlayerId: regular,
    active: true, automatic: false,
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
  set.lastLiberoRally ??= {}; set.lastLiberoRally[team] = set.points.length;
  set.lastSportingEvent = 'LIBERO_EXIT';
}
export function reconcileAutomaticLiberos(set: SetState) {
  const consequences: SportingConsequence[] = [];
  for (const team of ['HOME', 'AWAY'] as Side[]) {
    const plan = set.liberoPlans[team];
    if (!plan?.enabled || !plan.liberoMatchPlayerId) continue;
    if (set.liberoReplacements.some(x => x.side === team && x.active && x.automatic === false)) continue;
    if (regularPlayers(set, team).includes(plan.liberoMatchPlayerId)) continue;
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
        active: true, automatic: true,
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

  set[key]++;
  set.lastSportingEvent = 'TIMEOUT';
  set.lastConsequences = [{ kind: 'TIMEOUT', side: team, text: `Timeout ${team}` }];
}
function sanction(state: MatchState, payload: Record<string, unknown>, legacyAutomatic: boolean) {
  const type = String(payload.type ?? '');
  const side = parseSide(payload);
  if (!['MisconductWarning', 'MisconductPenalty', 'Expulsion', 'Disqualification', 'ImproperRequest', 'DelayWarning', 'DelayPenalty'].includes(type)) throw new Error('sanction_type_invalid');
  if (type === 'MisconductPenalty' || type === 'DelayPenalty') point(state, side === 'HOME' ? 'AWAY' : 'HOME', legacyAutomatic);
  const set = currentSet(state);
  set.lastSportingEvent = 'SANCTION';
  set.lastConsequences = [{ kind: type === 'MisconductPenalty' || type === 'DelayPenalty' ? 'POINT' : 'REMINDER', side, text: `Sanción ${type}` }];
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
  return events.reduce((state, event) => applyCommand(state, event, 'PERSISTED'), base);
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

// Ephemeral candidates, never part of the operational snapshot or event queue.
export function liberoSuggestions(state: MatchState): Array<{ side: Side; logical: number; command: MatchCommand }> {
  if (state.closed || state.trackLiberoReplacements === false) return [];
  const set = state.sets.find(x => x.setNumber === state.currentSetNumber);
  if (!set || set.status !== 'IN_PROGRESS') return [];
  return (['HOME', 'AWAY'] as Side[]).flatMap<{ side: Side; logical: number; command: MatchCommand }>(side => {
    const active = set.liberoReplacements.find(x => x.side === side && x.active);
    if (active) {
      const physical = physicalPosition(active.position, rotation(set, side));
      if ([2, 3, 4].includes(physical) || (physical === 1 && set.servingSide === side && !state.rulesSnapshot?.liberoCanServe))
        return [{ side, logical: active.position, command: { type: 'LIBERO_EXIT' as const, payload: { setNumber: set.setNumber, side, liberoMatchPlayerId: active.liberoMatchPlayerId } } }];
      return [];
    }
    const plan = set.liberoPlans[side];
    const last = set.liberoReplacements.filter(x => x.side === side).at(-1);
    const libero = plan?.enabled ? plan.liberoMatchPlayerId : last?.liberoMatchPlayerId;
    if (!libero || !state.declaredLiberoMatchPlayerIds[side].includes(libero) || effectivePlayers(set, side).includes(libero)) return [];
    // Avoid immediately suggesting reentry after an observed exit.
    if (set.lastLiberoRally?.[side] === set.points.length) return [];
    const positions = plan?.enabled ? plan.logicalPositions : last ? [last.position] : [];
    return positions.filter(logical => {
      const physical = physicalPosition(logical, rotation(set, side));
      return physical === 5 || physical === 6 || (physical === 1 && set.servingSide !== side);
    }).map(logical => ({ side, logical, command: { type: 'LIBERO_ENTER' as const, payload: { setNumber: set.setNumber, side, liberoMatchPlayerId: libero, replacedMatchPlayerId: regularPlayers(set, side)[logical] } } }));
  });
}
