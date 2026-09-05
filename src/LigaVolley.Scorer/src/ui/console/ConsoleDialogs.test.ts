import { describe, expect, it } from 'vitest';
import { applyCommand } from '../../domain/matchEngine';
import { initialState, type ServerSheetSnapshot, type Side } from '../../domain/types';
import {
  canNormalSubstituteFromPosition,
  normalSubstitutionBlockReason,
  normalSubstitutionCandidates,
} from './ConsoleDialogs';

const snapshot = {
  home: {
    teamName: 'HOME',
    players: [10, 11, 12, 13, 14, 15, 16, 88].map((matchPlayerId) => ({
      matchPlayerId,
      displayName: `P${matchPlayerId}`,
      jerseyNumber: matchPlayerId,
      isMatchCaptain: matchPlayerId === 10,
    })),
    liberos: [{ matchPlayerId: 88 }],
  },
  away: { teamName: 'AWAY', players: [], liberos: [] },
} as unknown as ServerSheetSnapshot;

function playing() {
  let state = applyCommand(initialState(), { type: 'PREPARE_SET', payload: {} });
  for (const side of ['HOME', 'AWAY'] as Side[])
    state = applyCommand(state, {
      type: 'SET_LINEUP',
      payload: {
        side,
        ...Object.fromEntries(
          [1, 2, 3, 4, 5, 6].map((position, index) => [
            `p${position}MatchPlayerId`,
            (side === 'HOME' ? 10 : 20) + index,
          ]),
        ),
      },
    });
  return applyCommand(state, { type: 'START_SET', payload: { initialServingSide: 'AWAY' } });
}

describe('normal substitution candidates', () => {
  it('excludes a declared libero even while the libero is outside the effective court', () => {
    const candidates = normalSubstitutionCandidates(snapshot, 'HOME', playing().sets[0], 0);
    expect(candidates.map((player) => player.matchPlayerId)).toEqual([16]);
  });
  it('offers only the paired starter when a regular substitute is on court', () => {
    let state = playing();
    state = applyCommand(state, {
      type: 'SUBSTITUTION',
      payload: { side: 'HOME', playerOutMatchPlayerId: 10, playerInMatchPlayerId: 16 },
    });
    expect(
      normalSubstitutionCandidates(snapshot, 'HOME', state.sets[0], 0).map((x) => x.matchPlayerId),
    ).toEqual([10]);
  });
  it('does not allow an active libero to be selected as the outgoing normal player', () => {
    const state = playing();
    state.sets[0].liberoPlans.HOME = {
      enabled: true,
      liberoMatchPlayerId: 88,
      logicalPositions: [0],
    };
    state.sets[0].liberoReplacements.push({
      side: 'HOME',
      position: 0,
      liberoMatchPlayerId: 88,
      replacedMatchPlayerId: 10,
      active: true,
    });
    expect(canNormalSubstituteFromPosition(state.sets[0], 'HOME', 0)).toBe(false);
    expect(normalSubstitutionBlockReason(state.sets[0], 'HOME', 0)).toContain(
      'no pueden participar en sustituciones normales',
    );
  });
});
