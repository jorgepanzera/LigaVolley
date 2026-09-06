import type { MatchCommand, MatchState, Side } from './types';
import { regularPlayers, effectivePlayers, physicalPosition, serverPlayer } from './matchEngine';

export interface RulesSnapshot {
  rulesProtocolVersion: number;
  rulesSnapshotVersion: number;
  maxSubstitutionsPerSet: number | null;
  maxTimeoutsPerSet: number;
  liberoEnabled: boolean;
  maxLiberos: number;
  liberoCanServe: boolean;
  decidingSetCourtChangePoint: number;
}
export const legacyRules: RulesSnapshot = {
  rulesProtocolVersion: 1,
  rulesSnapshotVersion: 0,
  maxSubstitutionsPerSet: null,
  maxTimeoutsPerSet: 2,
  liberoEnabled: true,
  maxLiberos: 2,
  liberoCanServe: false,
  decidingSetCourtChangePoint: 8,
};
export interface RuleIssue {
  code: string;
  context: Record<string, number>;
  requiresConfirmation: boolean;
}
export interface RuleEvaluation {
  hardViolations: RuleIssue[];
  warnings: RuleIssue[];
  classification: 'VALID' | 'INVALID_STATE' | 'WARNING_REQUIRES_CONFIRMATION';
}
export class RuleConfirmationRequired extends Error {
  constructor(public evaluation: RuleEvaluation) {
    super('rule_confirmation_required');
  }
}
export function evaluateCommand(state: MatchState, command: MatchCommand): RuleEvaluation {
  const hardViolations: RuleIssue[] = [],
    warnings: RuleIssue[] = [];
  const hard = (code: string) =>
    hardViolations.push({ code, context: {}, requiresConfirmation: false });
  const warn = (code: string, context: Record<string, number> = {}) => {
    if (!warnings.some((x) => x.code === code))
      warnings.push({ code, context, requiresConfirmation: true });
  };
  const result = (): RuleEvaluation => ({
    hardViolations,
    warnings,
    classification: hardViolations.length
      ? 'INVALID_STATE'
      : warnings.length
        ? 'WARNING_REQUIRES_CONFIRMATION'
        : 'VALID',
  });
  if (state.closed) {
    hard('match_already_closed');
    return result();
  }
  const set = state.sets.find((x) => x.setNumber === state.currentSetNumber);
  const p = command.payload;
  const side = String(p.side ?? p.winningSide ?? '').toUpperCase() as Side;
  const rules = state.rulesSnapshot ?? legacyRules;
  if (command.type === 'PREPARE_SET') {
    if (state.matchDecided) hard('match_already_decided');
    if (state.sets.some((x) => x.status !== 'FINISHED')) hard('match_set_invalid_state');
    return result();
  }
  if (command.type === 'MATCH_CLOSE') {
    if (!state.matchDecided || state.sets.some((x) => x.status !== 'FINISHED'))
      hard('match_not_decided');
    return result();
  }
  if (!set) {
    hard('match_set_not_found');
    return result();
  }
  if (p.setNumber !== undefined && Number(p.setNumber) !== set.setNumber)
    hard('match_set_invalid_state');
  if (command.type === 'SET_LINEUP') {
    if (set.status !== 'READY') hard('lineup_locked');
    if (!['HOME', 'AWAY'].includes(side)) {
      hard('invalid_side');
      return result();
    }
    const ids = [1, 2, 3, 4, 5, 6].map((i) => Number(p[`p${i}MatchPlayerId`]));
    if (new Set(ids).size !== 6 || ids.some((x) => !Number.isInteger(x) || x <= 0))
      hard('lineup_duplicate_player');
    if (state.matchPlayerIds && ids.some((x) => !state.matchPlayerIds![side].includes(x)))
      hard('lineup_player_wrong_team');
    if (ids.some((x) => state.declaredLiberoMatchPlayerIds[side].includes(x)))
      hard('lineup_libero_not_allowed');
    if (
      state.trackLiberoReplacements !== false &&
      p.liberoMatchPlayerId != null &&
      Array.isArray(p.liberoLogicalPositions) &&
      p.liberoLogicalPositions.length
    ) {
      if (!state.declaredLiberoMatchPlayerIds[side].includes(Number(p.liberoMatchPlayerId)))
        hard('invalid_libero_plan');
    }
    return result();
  }
  if (command.type === 'START_SET') {
    if (set.status !== 'READY' || set.lineups.HOME.length !== 6 || set.lineups.AWAY.length !== 6)
      hard('match_set_invalid_state');
    if (!['HOME', 'AWAY'].includes(String(p.initialServingSide).toUpperCase()))
      hard('invalid_side');
    return result();
  }
  if (command.type === 'CORRECT_LAST_POINT') {
    if (set.lastSportingEvent !== 'POINT' || !set.points.length)
      hard('point_not_last_effective_event');
    return result();
  }
  if (set.status !== 'IN_PROGRESS') hard('match_set_invalid_state');
  if (!['HOME', 'AWAY'].includes(side)) hard('invalid_side');
  if (hardViolations.length) return result();
  const regular = regularPlayers(set, side),
    effective = effectivePlayers(set, side);
  const liberos = state.declaredLiberoMatchPlayerIds[side];
  const belongs = (id: number) =>
    Number.isInteger(id) &&
    id > 0 &&
    (!state.matchPlayerIds || state.matchPlayerIds[side].includes(id));
  if (regular.length !== 6 || new Set(regular).size !== 6 || new Set(effective).size !== 6) {
    hard('invalid_court_state');
    return result();
  }
  switch (command.type) {
    case 'SUBSTITUTION':
    case 'SUBSTITUTION_REQUEST': {
      if (state.trackSubstitutions === false) {
        hard('substitution_tracking_disabled');
        break;
      }
      const pairs =
        command.type === 'SUBSTITUTION'
          ? [p]
          : Array.isArray(p.replacements)
            ? (p.replacements as Record<string, unknown>[])
            : [];
      if (
        !pairs.length ||
        new Set(pairs.map((x) => x.playerOutMatchPlayerId)).size !== pairs.length ||
        new Set(pairs.map((x) => x.playerInMatchPlayerId)).size !== pairs.length
      ) {
        hard('invalid_substitution');
        break;
      }
      const final = [...regular],
        substitutions = set.substitutions.filter((x) => x.side === side);
      for (const pair of pairs) {
        const out = Number(pair.playerOutMatchPlayerId),
          into = Number(pair.playerInMatchPlayerId),
          position = regular.indexOf(out);
        if (!belongs(out) || !belongs(into) || position < 0 || out === into) {
          hard('invalid_substitution');
          continue;
        }
        final[position] = into;
        const starter = set.lineups[side][position],
          history = substitutions.filter((x) => x.position === position);
        if (
          (out === starter && history.length > 0) ||
          (out !== starter && (history.length !== 1 || history[0].playerInMatchPlayerId !== out))
        )
          warn('substitution_reentry_irregular', { position });
        if (
          substitutions.some(
            (x) =>
              x.position !== position &&
              (x.playerInMatchPlayerId === into || x.playerOutMatchPlayerId === into),
          ) ||
          set.lineups[side].some((x, i) => i !== position && x === into)
        )
          warn('substitution_player_already_bound', { playerInMatchPlayerId: into });
        if (out !== starter && into !== starter)
          warn('substitution_original_player_mismatch', { expectedMatchPlayerId: starter });
        if (liberos.includes(out) || liberos.includes(into))
          warn('libero_used_as_regular_substitute');
      }
      if (new Set(final).size !== 6) hard('substitution_player_already_on_court');
      set.liberoReplacements
        .filter((x) => x.side === side && x.active)
        .forEach((x) => {
          final[x.position] = x.liberoMatchPlayerId;
        });
      if (new Set(final).size !== 6) hard('duplicate_effective_player');
      if (final.filter(id => liberos.includes(id)).length > 1) hard('invalid_libero_replacement');
      if (
        rules.maxSubstitutionsPerSet !== null &&
        substitutions.length + pairs.length > rules.maxSubstitutionsPerSet
      )
        warn('substitution_limit_exceeded', {
          used: substitutions.length,
          requested: pairs.length,
          projected: substitutions.length + pairs.length,
          maximum: rules.maxSubstitutionsPerSet,
        });
      break;
    }
    case 'TIMEOUT': {
      const used = side === 'HOME' ? set.homeTimeouts : set.awayTimeouts;
      if (used >= rules.maxTimeoutsPerSet)
        warn('timeout_limit_exceeded', { used, maximum: rules.maxTimeoutsPerSet });
      break;
    }
    case 'POINT': {
      if (state.trackLiberoReplacements !== false)
        for (const active of set.liberoReplacements.filter(x => x.active)) {
          const physical = physicalPosition(active.position, active.side === 'HOME' ? set.homeRotationOffset : set.awayRotationOffset);
          if ([2, 3, 4].includes(physical)) warn('libero_in_front_row', { physicalPosition: physical });
          if (physical === 1 && set.servingSide === active.side && !rules.liberoCanServe) warn('libero_service_not_allowed');
        }
      if (p.observedServerMatchPlayerId == null) break;
      const serving = set.servingSide;
      if (!serving) {
        hard('invalid_court_state');
        break;
      }
      const observed = Number(p.observedServerMatchPlayerId),
        expected = serverPlayer(set, serving);
      if (state.matchPlayerIds && !state.matchPlayerIds[serving].includes(observed)) {
        hard('server_player_wrong_team');
        break;
      }
      if (observed !== expected)
        warn('unexpected_server', {
          expectedMatchPlayerId: expected,
          observedMatchPlayerId: observed,
        });
      if (state.declaredLiberoMatchPlayerIds[serving].includes(observed) && !rules.liberoCanServe)
        warn('libero_service_not_allowed', { observedMatchPlayerId: observed });
      break;
    }
    case 'LIBERO_ENTER': {
      if (state.trackLiberoReplacements === false) {
        hard('libero_tracking_disabled');
        break;
      }
      const libero = Number(p.liberoMatchPlayerId),
        replaced = Number(p.replacedMatchPlayerId),
        position = effective.indexOf(replaced);
      if (!liberos.includes(libero)) {
        hard('libero_not_declared');
        break;
      }
      if (position < 0 || !belongs(replaced)) {
        hard('libero_invalid_replaced_player');
        break;
      }
      if (effective.includes(libero)) {
        hard('libero_already_on_court');
        break;
      }
      if (effective.some((id, index) => index !== position && liberos.includes(id))) {
        hard('invalid_libero_replacement');
        break;
      }
      const active = set.liberoReplacements.filter((x) => x.side === side && x.active);
      if (active.length > 0 && !active.some((x) => x.position === position))
        hard('invalid_libero_replacement');
      const lastRegular = set.lastLiberoRegular?.[side];
      if (!active.some(x => x.position === position) && lastRegular != null && lastRegular !== regular[position])
        warn('libero_wrong_regular_replacement', { expectedMatchPlayerId: lastRegular });
      if (set.lastLiberoRally?.[side] === set.points.length)
        warn('libero_replacement_without_completed_rally');
      const physical = physicalPosition(
        position,
        side === 'HOME' ? set.homeRotationOffset : set.awayRotationOffset,
      );
      if ([2, 3, 4].includes(physical)) warn('libero_in_front_row', { physicalPosition: physical });
      if (physical === 1 && set.servingSide === side && !rules.liberoCanServe)
        warn('libero_service_not_allowed');
      break;
    }
    case 'LIBERO_EXIT':
      if (state.trackLiberoReplacements === false) hard('libero_tracking_disabled');
      if (
        !set.liberoReplacements.some(
          (x) =>
            x.side === side && x.active && x.liberoMatchPlayerId === Number(p.liberoMatchPlayerId),
        )
      )
        hard('invalid_libero_replacement');
      if (set.lastLiberoRally?.[side] === set.points.length)
        warn('libero_replacement_without_completed_rally');
      break;
    default:
      hard('sync_invalid_event_type');
  }
  return result();
}

export function assertEvaluation(
  evaluation: RuleEvaluation,
  command: MatchCommand,
  origin: 'DIRECT' | 'PERSISTED',
) {
  if (evaluation.hardViolations.length) throw new Error(evaluation.hardViolations[0].code);
  const confirmed = Array.isArray(command.payload.confirmedRuleWarnings)
    ? command.payload.confirmedRuleWarnings
    : [];
  if (origin === 'DIRECT' && evaluation.warnings.some((x) => !confirmed.includes(x.code)))
    throw new RuleConfirmationRequired(evaluation);
}
