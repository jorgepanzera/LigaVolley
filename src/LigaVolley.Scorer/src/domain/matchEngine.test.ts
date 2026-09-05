import { describe, expect, it } from 'vitest';
import { applyCommand, effectivePlayers, serverPlayer, validateLiberoPlan } from './matchEngine';
import { initialState, type MatchState, type Side } from './types';
const ready = () => {
  let s = applyCommand(initialState(), { type: 'PREPARE_SET', payload: {} });
  for (const side of ['HOME', 'AWAY'] as Side[])
    s = applyCommand(s, {
      type: 'SET_LINEUP',
      payload: {
        side,
        ...Object.fromEntries(
          [1, 2, 3, 4, 5, 6].map((_, i) => [
            `p${i + 1}MatchPlayerId`,
            (side === 'HOME' ? 10 : 20) + i,
          ]),
        ),
      },
    });
  return applyCommand(s, { type: 'START_SET', payload: { initialServingSide: 'HOME' } });
};
const points = (s: MatchState, side: Side, n: number) => {
  for (let i = 0; i < n; i++)
    s = applyCommand(s, { type: 'POINT', payload: { winningSide: side } });
  return s;
};
describe('local MatchEngine', () => {
  it('scores, changes service and rotates receiver', () => {
    let s = ready();
    s = applyCommand(s, { type: 'POINT', payload: { winningSide: 'HOME' } });
    expect(s.sets[0].homePoints).toBe(1);
    s = applyCommand(s, { type: 'POINT', payload: { winningSide: 'AWAY' } });
    expect(s.sets[0].awayRotationOffset).toBe(1);
    expect(serverPlayer(s.sets[0], 'AWAY')).toBe(21);
  });
  it('finishes normal and fifth sets with difference two', () => {
    let s = points(ready(), 'HOME', 25);
    expect(s.homeSets).toBe(1);
    for (let n = 2; n <= 4; n++) {
      s = applyCommand(s, { type: 'PREPARE_SET', payload: {} });
      for (const side of ['HOME', 'AWAY'] as Side[])
        s = applyCommand(s, {
          type: 'SET_LINEUP',
          payload: {
            side,
            ...Object.fromEntries(
              [1, 2, 3, 4, 5, 6].map((_, i) => [
                `p${i + 1}MatchPlayerId`,
                (side === 'HOME' ? 10 : 20) + i,
              ]),
            ),
          },
        });
      s = applyCommand(s, { type: 'START_SET', payload: { initialServingSide: 'HOME' } });
      s = points(s, n < 4 ? 'AWAY' : 'HOME', 25);
    }
    expect(s.sets[3].status).toBe('FINISHED');
  });
  it('corrects only the last effective point', () => {
    let s = applyCommand(ready(), { type: 'POINT', payload: { winningSide: 'AWAY' } });
    s = applyCommand(s, { type: 'CORRECT_LAST_POINT', payload: {} });
    expect(s.sets[0].awayPoints).toBe(0);
    expect(s.sets[0].awayRotationOffset).toBe(0);
  });
  it('tracks substitution, libero and timeout', () => {
    let s = ready();
    s = applyCommand(s, {
      type: 'SUBSTITUTION',
      payload: { side: 'HOME', playerOutMatchPlayerId: 10, playerInMatchPlayerId: 99 },
    });
    expect(s.sets[0].substitutions).toHaveLength(1);
    s = applyCommand(s, {
      type: 'LIBERO_ENTER',
      payload: { side: 'HOME', liberoMatchPlayerId: 88, replacedMatchPlayerId: 99 },
    });
    s = applyCommand(s, {
      type: 'LIBERO_EXIT',
      payload: { side: 'HOME', liberoMatchPlayerId: 88 },
    });
    s = applyCommand(s, { type: 'TIMEOUT', payload: { side: 'HOME' } });
    expect(s.sets[0].homeTimeouts).toBe(1);
    expect(s.sets[0].liberoReplacements[0].active).toBe(false);
  });
  it('rejects normal substitutions when either player is a declared libero', () => {
    const state = ready();
    state.declaredLiberoMatchPlayerIds.HOME = [88];
    expect(() =>
      applyCommand(state, {
        type: 'SUBSTITUTION',
        payload: { side: 'HOME', playerOutMatchPlayerId: 10, playerInMatchPlayerId: 88 },
      }),
    ).toThrow('substitution_player_is_libero');
    expect(() =>
      applyCommand(state, {
        type: 'SUBSTITUTION',
        payload: { side: 'HOME', playerOutMatchPlayerId: 88, playerInMatchPlayerId: 99 },
      }),
    ).toThrow('substitution_player_is_libero');
    expect(state.sets[0].substitutions).toHaveLength(0);
  });
  it('decides best of five and requires explicit close', () => {
    let s = ready();
    for (let set = 1; set <= 3; set++) {
      s = points(s, 'HOME', 25);
      if (set < 3) {
        s = applyCommand(s, { type: 'PREPARE_SET', payload: {} });
        for (const side of ['HOME', 'AWAY'] as Side[])
          s = applyCommand(s, {
            type: 'SET_LINEUP',
            payload: {
              side,
              ...Object.fromEntries(
                [1, 2, 3, 4, 5, 6].map((_, i) => [
                  `p${i + 1}MatchPlayerId`,
                  (side === 'HOME' ? 10 : 20) + i,
                ]),
              ),
            },
          });
        s = applyCommand(s, { type: 'START_SET', payload: { initialServingSide: 'HOME' } });
      }
    }
    expect(s.matchDecided).toBe(true);
    expect(s.closed).toBe(false);
    expect(applyCommand(s, { type: 'MATCH_CLOSE', payload: {} }).closed).toBe(true);
  });
  it('rejects a libero plan that could cover two positions simultaneously', () => {
    expect(() =>
      validateLiberoPlan(
        { enabled: true, liberoMatchPlayerId: 88, logicalPositions: [0, 1] },
        [10, 11, 12, 13, 14, 15],
      ),
    ).toThrow('ambiguous_libero_plan');
  });
  it('derives pre-serve libero state but keeps the regular player as server at P1', () => {
    let s = applyCommand(initialState(), { type: 'PREPARE_SET', payload: {} });
    s = applyCommand(s, {
      type: 'SET_LINEUP',
      payload: {
        side: 'HOME',
        p1MatchPlayerId: 10,
        p2MatchPlayerId: 11,
        p3MatchPlayerId: 12,
        p4MatchPlayerId: 13,
        p5MatchPlayerId: 14,
        p6MatchPlayerId: 15,
        liberoMatchPlayerId: 88,
        liberoLogicalPositions: [0, 3],
      },
    });
    s = applyCommand(s, {
      type: 'SET_LINEUP',
      payload: {
        side: 'AWAY',
        p1MatchPlayerId: 20,
        p2MatchPlayerId: 21,
        p3MatchPlayerId: 22,
        p4MatchPlayerId: 23,
        p5MatchPlayerId: 24,
        p6MatchPlayerId: 25,
      },
    });
    s = applyCommand(s, { type: 'START_SET', payload: { initialServingSide: 'AWAY' } });
    expect(effectivePlayers(s.sets[0], 'HOME')[0]).toBe(88);
    expect(serverPlayer(s.sets[0], 'AWAY')).toBe(20);
    s = applyCommand(s, { type: 'POINT', payload: { winningSide: 'HOME' } });
    expect(serverPlayer(s.sets[0], 'HOME')).not.toBe(88);
  });
  it('restores the current substituted regular when the libero leaves', () => {
    let s = ready();
    s = applyCommand(s, {
      type: 'SUBSTITUTION',
      payload: { side: 'HOME', playerOutMatchPlayerId: 10, playerInMatchPlayerId: 99 },
    });
    s = applyCommand(s, {
      type: 'LIBERO_ENTER',
      payload: { side: 'HOME', liberoMatchPlayerId: 88, replacedMatchPlayerId: 99 },
    });
    expect(effectivePlayers(s.sets[0], 'HOME')[0]).toBe(88);
    s = applyCommand(s, {
      type: 'LIBERO_EXIT',
      payload: { side: 'HOME', liberoMatchPlayerId: 88 },
    });
    expect(effectivePlayers(s.sets[0], 'HOME')[0]).toBe(99);
  });
});
