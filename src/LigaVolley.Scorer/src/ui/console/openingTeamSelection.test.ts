import { describe, expect, it } from 'vitest';
import type { OpenTeamSelection } from '../../api/scorerApi';
import { toggleOpeningPlayer } from './openingTeamSelection';

const empty = (): OpenTeamSelection => ({ players: [], liberoCompetitionRosterPlayerIds: [], competitionRosterStaffIds: [9] });
const player = (id: number, role = 'Libero') => ({ competitionRosterPlayerId: id, displayName: `Player ${id}`, role });

describe('opening libero declaration', () => {
  it.each(['Libero', 'LIBERO'])('declares a selected %s from roster identity before any jersey is assigned', role => {
    const selection = toggleOpeningPlayer(empty(), player(17, role));
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([17]);
    expect(selection.players[0].jerseyNumber).toBeUndefined();
    expect(selection.competitionRosterStaffIds).toEqual([9]);
  });
  it('removes the declaration on deselection and never duplicates it on reselection', () => {
    let selection = toggleOpeningPlayer(empty(), player(17));
    selection = toggleOpeningPlayer(selection, player(18));
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([17, 18]);
    selection = toggleOpeningPlayer(selection, player(17));
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([18]);
    expect(selection.players.map(p => p.competitionRosterPlayerId)).toEqual([18]);
    selection = toggleOpeningPlayer(selection, player(17));
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([18, 17]);
  });
  it.each([1, 5, 42, 99])('never infers a libero from jersey %s', jerseyNumber => {
    const selection = toggleOpeningPlayer(empty(), player(17, 'Setter'));
    selection.players[0].jerseyNumber = jerseyNumber;
    expect(selection.liberoCompetitionRosterPlayerIds).toEqual([]);
    const withLibero = toggleOpeningPlayer(selection, player(18));
    withLibero.players[1].jerseyNumber = jerseyNumber;
    expect(withLibero.liberoCompetitionRosterPlayerIds).toEqual([18]);
  });
});
