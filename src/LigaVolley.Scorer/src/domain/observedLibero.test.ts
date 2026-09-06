import { describe, expect, it } from 'vitest';
import { applyCommand, effectivePlayers, liberoSuggestions, regularPlayers, replay } from './matchEngine';
import { evaluateCommand, legacyRules } from './rulesAssistant';
import { initialState, type MatchCommand, type MatchState, type Side } from './types';

function ready(plan = false) {
  let state = initialState();
  state.rulesSnapshot = { ...legacyRules, rulesProtocolVersion: 2, rulesSnapshotVersion: 2 };
  state.declaredLiberoMatchPlayerIds = { HOME: [14, 15], AWAY: [114, 115] };
  state.matchPlayerIds = { HOME: Array.from({ length: 15 }, (_, i) => i + 1), AWAY: Array.from({ length: 15 }, (_, i) => i + 101) };
  state = applyCommand(state, { type: 'PREPARE_SET', payload: {} });
  for (const side of ['HOME', 'AWAY'] as Side[]) state = applyCommand(state, { type: 'SET_LINEUP', payload: {
    side, ...Object.fromEntries(Array.from({ length: 6 }, (_, i) => [`p${i + 1}MatchPlayerId`, i + (side === 'HOME' ? 1 : 101)])),
    ...(plan ? { liberoMatchPlayerId: side === 'HOME' ? 14 : 114, liberoLogicalPositions: [0, 4, 5] } : {}),
  } });
  return state;
}
function start(plan = false) { return applyCommand(ready(plan), { type: 'START_SET', payload: { initialServingSide: 'HOME' } }); }
function confirmed(state: MatchState, command: MatchCommand) {
  return applyCommand(state, { ...command, payload: { ...command.payload, confirmedRuleWarnings: evaluateCommand(state, command).warnings.map(x => x.code) } });
}
describe('observed effective court', () => {
  it.each([false, true])('starting with plan=%s never generates replacements', plan => {
    const state = start(plan), before = structuredClone(state);
    expect(state.sets[0].liberoReplacements).toEqual([]);
    expect(liberoSuggestions(state).length).toBe(plan ? 5 : 0);
    expect(state).toEqual(before);
    expect(applyCommand(state, { type: 'POINT', payload: { winningSide: 'AWAY' } }).sets[0].liberoReplacements).toEqual([]);
  });
  it.each([['HOME', 5, 14], ['HOME', 6, 14], ['AWAY', 101, 114]] as const)('initial observed %s regular %s', (side, regular, libero) => {
    const state = applyCommand(start(), { type: 'LIBERO_ENTER', payload: { side, liberoMatchPlayerId: libero, replacedMatchPlayerId: regular } });
    expect(effectivePlayers(state.sets[0], side)).toContain(libero);
    expect(effectivePlayers(state.sets[0], side)).not.toContain(regular);
    expect(regularPlayers(state.sets[0], side)).toContain(regular);
    expect(state.sets[0].points).toEqual([]);
    expect(state.sets[0].substitutions).toEqual([]);
  });
  it('keeps the observed libero on rotation to front, suggests exit, warns on next point', () => {
    let state = applyCommand(start(), { type: 'LIBERO_ENTER', payload: { side: 'HOME', liberoMatchPlayerId: 14, replacedMatchPlayerId: 5 } });
    state = applyCommand(state, { type: 'POINT', payload: { winningSide: 'AWAY' } });
    state = applyCommand(state, { type: 'POINT', payload: { winningSide: 'HOME' } });
    expect(effectivePlayers(state.sets[0], 'HOME')[4]).toBe(14);
    expect(liberoSuggestions(state)[0].command.type).toBe('LIBERO_EXIT');
    expect(() => applyCommand(state, { type: 'POINT', payload: { winningSide: 'HOME' } })).toThrow('rule_confirmation_required');
    const next = confirmed(state, { type: 'POINT', payload: { winningSide: 'HOME' } });
    expect(next.sets[0].liberoReplacements).toEqual(state.sets[0].liberoReplacements);
  });
  it('swaps A to B to A, preserves a substituted regular, correction and replay', () => {
    const base = start();
    const commands: MatchCommand[] = [
      { type: 'LIBERO_ENTER', payload: { side: 'HOME', liberoMatchPlayerId: 14, replacedMatchPlayerId: 6 } },
      { type: 'POINT', payload: { winningSide: 'AWAY' } },
      { type: 'POINT', payload: { winningSide: 'HOME' } },
      { type: 'LIBERO_ENTER', payload: { side: 'HOME', liberoMatchPlayerId: 15, replacedMatchPlayerId: 14 } },
      { type: 'SUBSTITUTION', payload: { side: 'HOME', playerOutMatchPlayerId: 6, playerInMatchPlayerId: 7 } },
      { type: 'POINT', payload: { winningSide: 'HOME' } },
      { type: 'CORRECT_LAST_POINT', payload: {} },
      { type: 'LIBERO_ENTER', payload: { side: 'HOME', liberoMatchPlayerId: 14, replacedMatchPlayerId: 15, confirmedRuleWarnings: ['libero_replacement_without_completed_rally'] } },
      { type: 'LIBERO_EXIT', payload: { side: 'HOME', liberoMatchPlayerId: 14, confirmedRuleWarnings: ['libero_replacement_without_completed_rally'] } },
    ];
    const direct = commands.reduce((s, c) => applyCommand(s, c), base);
    expect(replay(base, commands)).toEqual(direct);
    expect(effectivePlayers(direct.sets[0], 'HOME')[5]).toBe(7);
    expect(direct.sets[0].lineups.HOME[5]).toBe(6);
    expect(direct.sets[0].liberoReplacements.map(x => x.liberoMatchPlayerId)).toEqual([14, 15, 14]);
    expect(direct.sets[0].liberoReplacements.every(x => x.automatic === false)).toBe(true);
  });
  it('historical unmarked replay retains automatic history; new marked commands preserve it', () => {
    const base = ready();
    base.rulesSnapshot = legacyRules;
    base.sets[0].liberoPlans.HOME = { enabled: true, liberoMatchPlayerId: 14, logicalPositions: [4] };
    const historical = replay(base, [{ type: 'START_SET', payload: { initialServingSide: 'HOME' } }]);
    expect(historical.sets[0].liberoReplacements[0]).toMatchObject({ automatic: true, active: true });
    const facts = structuredClone(historical.sets[0].liberoReplacements);
    const next = replay(historical, [{ type: 'POINT', payload: { winningSide: 'HOME', observedLiberoReplacements: true } }]);
    expect(next.sets[0].liberoReplacements).toEqual(facts);
    expect(historical.sets[0].liberoReplacements).toEqual(facts);
  });
});
