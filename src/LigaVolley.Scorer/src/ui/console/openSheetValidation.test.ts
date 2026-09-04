import { describe, expect, it } from 'vitest';
import { isOpeningTeamValid, type OpeningPlayerSelection } from './openSheetValidation';

const players = (): OpeningPlayerSelection[] =>
  Array.from({ length: 6 }, (_, index) => ({
    competitionRosterPlayerId: index + 1,
    jerseyNumber: index + 1,
    isMatchCaptain: index === 0,
  }));

describe('OpenMatchSheet UI validation', () => {
  it('accepts six selected players with unique jerseys and exactly one captain', () => {
    expect(isOpeningTeamValid(players())).toBe(true);
  });

  it.each([
    [
      'missing jersey',
      (value: OpeningPlayerSelection[]) => {
        value[2].jerseyNumber = undefined;
      },
    ],
    [
      'duplicate jersey',
      (value: OpeningPlayerSelection[]) => {
        value[2].jerseyNumber = 2;
      },
    ],
    [
      'no captain',
      (value: OpeningPlayerSelection[]) => {
        value[0].isMatchCaptain = false;
      },
    ],
    [
      'two captains',
      (value: OpeningPlayerSelection[]) => {
        value[1].isMatchCaptain = true;
      },
    ],
  ])('rejects %s', (_, arrange) => {
    const value = players();
    arrange(value);
    expect(isOpeningTeamValid(value)).toBe(false);
  });
});
