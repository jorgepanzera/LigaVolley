import { describe, expect, it } from 'vitest';
import vectors from '../../../../tests/shared/rules-assistant-v1.json';
import {
  applyCommand,
  effectivePlayers,
  regularPlayers,
  replay,
  serverPlayer,
} from './matchEngine';
import { evaluateCommand, legacyRules } from './rulesAssistant';
import { initialState, type MatchCommand, type MatchState, type Side } from './types';

export function rulesPlaying(): MatchState {
  const state = initialState();
  state.rulesSnapshot = { ...legacyRules, rulesSnapshotVersion: 1, maxSubstitutionsPerSet: 6 };
  state.matchPlayerIds = {
    HOME: Array.from({ length: 15 }, (_, i) => i + 1),
    AWAY: Array.from({ length: 15 }, (_, i) => i + 101),
  };
  state.declaredLiberoMatchPlayerIds = { HOME: [14, 15], AWAY: [114, 115] };
  let next = applyCommand(state, { type: 'PREPARE_SET', payload: {} });
  for (const side of ['HOME', 'AWAY'] as Side[])
    next = applyCommand(next, {
      type: 'SET_LINEUP',
      payload: {
        side,
        ...Object.fromEntries(
          Array.from({ length: 6 }, (_, i) => [
            `p${i + 1}MatchPlayerId`,
            i + (side === 'HOME' ? 1 : 101),
          ]),
        ),
      },
    });
  return applyCommand(next, { type: 'START_SET', payload: { initialServingSide: 'HOME' } });
}

describe('shared Rules Assistant v1 vectors', () => {
  it('correcting a point preserves the preceding manual libero decision and logical regular', () => {
    let state = rulesPlaying();
    state = applyCommand(state, {
      type: 'LIBERO_ENTER',
      payload: { side: 'HOME', liberoMatchPlayerId: 14, replacedMatchPlayerId: 5 },
    });
    state = applyCommand(state, {
      type: 'SUBSTITUTION_REQUEST',
      payload: {
        side: 'HOME',
        replacements: [{ playerOutMatchPlayerId: 5, playerInMatchPlayerId: 7 }],
      },
    });
    state = applyCommand(state, { type: 'POINT', payload: { winningSide: 'AWAY' } });
    state = applyCommand(state, { type: 'CORRECT_LAST_POINT', payload: {} });
    expect(regularPlayers(state.sets[0], 'HOME')[4]).toBe(7);
    expect(effectivePlayers(state.sets[0], 'HOME')[4]).toBe(14);
    expect(state.sets[0].awayPoints).toBe(0);
  });
  for (const vector of vectors)
    it(vector.name, () => {
      const state = rulesPlaying(),
        set = state.sets[0];
      state.closed = vector.closed ?? false;
      state.rulesSnapshot!.maxSubstitutionsPerSet =
        'maxSubstitutions' in vector ? vector.maxSubstitutions! : 6;
      set.substitutions = (vector.substitutions ?? []).map((x) => ({ ...x, side: 'HOME' }));
      set.liberoReplacements = (vector.activeLiberos ?? []).map((x) => ({
        ...x,
        side: 'HOME',
        active: true,
        automatic: false,
      }));
      set.homeTimeouts = vector.timeouts ?? 0;
      set.points = Array.from({ length: vector.rallyCount ?? 0 }, () => 'HOME');
      set.homePoints = set.points.length;
      set.lastLiberoRally = { HOME: vector.lastLiberoRally };
      set.lastLiberoRegular = { HOME: vector.lastLiberoRegular };
      if (vector.setNumber) {
        set.setNumber = vector.setNumber;
        state.currentSetNumber = vector.setNumber;
      }
      const command: MatchCommand = {
        type: vector.command.type as MatchCommand['type'],
        payload: { ...vector.command, winningSide: vector.command.side },
      };
      const evaluation = evaluateCommand(state, command);
      expect(evaluation.hardViolations.map((x) => x.code)).toEqual(vector.hard);
      expect(evaluation.warnings.map((x) => x.code)).toEqual(vector.warnings);
      expect(Object.fromEntries(evaluation.warnings.map((x) => [x.code, x.context]))).toEqual(
        vector.warningContexts,
      );
      if (vector.hard.length) {
        expect(() => replay(state, [command])).toThrow(vector.hard[0]);
        return;
      }
      const confirmed = {
        ...command,
        payload: {
          ...command.payload,
          confirmedRuleWarnings: evaluation.warnings.map((x) => x.code),
        },
      };
      const applied = applyCommand(state, confirmed);
      expect(replay(state, [command])).toEqual(applied);
      expect(new Set(effectivePlayers(applied.sets[0], 'HOME')).size).toBe(6);
      if (command.type === 'TIMEOUT')
        expect(applied.sets[0].homeTimeouts).toBe(set.homeTimeouts + 1);
      if (vector.name === 'covered_regular') {
        expect(regularPlayers(applied.sets[0], 'HOME')[4]).toBe(11);
        expect(effectivePlayers(applied.sets[0], 'HOME')[4]).toBe(14);
      }
      if (command.type === 'POINT') expect(serverPlayer(applied.sets[0], 'HOME')).toBe(1);
      if (vector.name === 'court_change')
        expect(applied.sets[0].lastConsequences).toContainEqual({
          kind: 'REMINDER',
          text: 'Cambio de campo',
        });
    });
});
