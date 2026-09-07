import { describe, expect, it } from 'vitest';
import { applyCommand } from '../../domain/matchEngine';
import { initialState, type Side } from '../../domain/types';
import { legacyRules } from '../../domain/rulesAssistant';
import { suggestionsForSide } from './LiberoSuggestions';

function startedWithPlans() {
  let state = initialState();
  state.rulesSnapshot = { ...legacyRules, rulesProtocolVersion: 2, rulesSnapshotVersion: 2 };
  state.declaredLiberoMatchPlayerIds = { HOME: [14, 15], AWAY: [114, 115] };
  state.matchPlayerIds = { HOME: Array.from({ length: 15 }, (_, index) => index + 1), AWAY: Array.from({ length: 15 }, (_, index) => index + 101) };
  state = applyCommand(state, { type: 'PREPARE_SET', payload: {} });
  for (const side of ['HOME', 'AWAY'] as Side[]) state = applyCommand(state, { type: 'SET_LINEUP', payload: {
    side,
    ...Object.fromEntries(Array.from({ length: 6 }, (_, index) => [`p${index + 1}MatchPlayerId`, index + (side === 'HOME' ? 1 : 101)])),
    liberoMatchPlayerId: side === 'HOME' ? 14 : 114,
    liberoLogicalPositions: [0, 4, 5],
  } });
  return applyCommand(state, { type: 'START_SET', payload: { initialServingSide: 'HOME' } });
}

describe('libero suggestions by team side', () => {
  it('keeps HOME and AWAY suggestions in their own presentation collections', () => {
    const state = startedWithPlans();
    const home = suggestionsForSide(state, 'HOME');
    const away = suggestionsForSide(state, 'AWAY');

    expect(home).not.toHaveLength(0);
    expect(away).not.toHaveLength(0);
    expect(home.every((suggestion) => suggestion.side === 'HOME')).toBe(true);
    expect(away.every((suggestion) => suggestion.side === 'AWAY')).toBe(true);
    expect([...home, ...away]).toHaveLength(home.length + away.length);
  });
});
