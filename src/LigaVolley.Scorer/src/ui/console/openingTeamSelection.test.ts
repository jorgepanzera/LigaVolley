import { describe, expect, it } from 'vitest';
import type { OpenTeamSelection } from '../../api/scorerApi';
import { toggleLiberoDeclaration, toggleOpeningPlayer } from './openingTeamSelection';

const empty = (): OpenTeamSelection => ({ players: [], liberoCompetitionRosterPlayerIds: [], competitionRosterStaffIds: [9] });
const player = (id: number, role = 'Libero') => ({ competitionRosterPlayerId: id, displayName: `Player ${id}`, role, isHabitualLiberoCandidate: role.toUpperCase() === 'LIBERO' });

describe('opening libero declaration', () => {
  it.each(['Libero', 'LIBERO'])('declares a selected %s from roster identity before any jersey is assigned', role => {
    const candidate = player(17, role);
    const selection = toggleOpeningPlayer(empty(), candidate, [candidate], 2);
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([17]);
    expect(selection.players[0].jerseyNumber).toBeUndefined();
    expect(selection.competitionRosterStaffIds).toEqual([9]);
  });
  it('removes the declaration on deselection and never duplicates it on reselection', () => {
    const candidates = [player(17), player(18)];
    let selection = toggleOpeningPlayer(empty(), candidates[0], candidates, 2);
    selection = toggleOpeningPlayer(selection, candidates[1], candidates, 2);
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([17, 18]);
    selection = toggleOpeningPlayer(selection, candidates[0], candidates, 2);
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([18]);
    expect(selection.players.map(p => p.competitionRosterPlayerId)).toEqual([18]);
    selection = toggleOpeningPlayer(selection, candidates[0], candidates, 2);
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([17, 18]);
  });
  it.each([1, 5, 42, 99])('never infers a libero from jersey %s', jerseyNumber => {
    const setter = player(17, 'Setter');
    const candidate = player(18);
    const selection = toggleOpeningPlayer(empty(), setter, [setter, candidate], 2);
    selection.players[0].jerseyNumber = jerseyNumber;
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([]);
    const withLibero = toggleOpeningPlayer(selection, candidate, [setter, candidate], 2);
    withLibero.players[1].jerseyNumber = jerseyNumber;
    expect(withLibero.liberoCompetitionRosterPlayerIds).toEqual([18]);
  });
  it('does not choose arbitrarily when habitual candidates exceed the effective maximum and preserves an operator choice', () => {
    const candidates = [player(17), player(18), player(19)];
    let selection = empty();
    for (const candidate of candidates) selection = toggleOpeningPlayer(selection, candidate, candidates, 2);
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([]);
    selection = toggleLiberoDeclaration(selection, 18, 2);
    const lateCandidate = player(20);
    selection = toggleOpeningPlayer(selection, lateCandidate, [...candidates, lateCandidate], 2);
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([18]);
  });
});
