import type { OpenTeamContext, OpenTeamSelection } from '../../api/scorerApi';

// The roster function is only an editable opening convenience. MATCH_LIBERO is authoritative after POST /open.
export type OpeningSelectionState = OpenTeamSelection & { liberoSelectionTouched?: boolean };

export function reconcileOpeningLiberos(
  selection: OpeningSelectionState,
  players: OpenTeamContext['players'],
  maxLiberos: number,
): OpeningSelectionState {
  const selected = new Set(selection.players.map(x => x.competitionRosterPlayerId));
  const retained = selection.liberoCompetitionRosterPlayerIds.filter(id => selected.has(id));
  if (selection.liberoSelectionTouched) return { ...selection, liberoCompetitionRosterPlayerIds: retained };
  const candidates = players.filter(x => selected.has(x.competitionRosterPlayerId) && (x.isHabitualLiberoCandidate ?? x.role?.toUpperCase() === 'LIBERO'))
    .map(x => x.competitionRosterPlayerId);
  return { ...selection, liberoCompetitionRosterPlayerIds: candidates.length <= maxLiberos ? candidates : [] };
}

export function toggleOpeningPlayer(
  selection: OpeningSelectionState,
  player: OpenTeamContext['players'][number],
  players: OpenTeamContext['players'] = [player],
  maxLiberos = 2,
): OpeningSelectionState {
  const id = player.competitionRosterPlayerId;
  const selected = selection.players.some(candidate => candidate.competitionRosterPlayerId === id);
  return reconcileOpeningLiberos({
    ...selection,
    players: selected
      ? selection.players.filter(candidate => candidate.competitionRosterPlayerId !== id)
      : [...selection.players, { competitionRosterPlayerId: id, isMatchCaptain: false }],
  }, players, maxLiberos);
}

export function toggleLiberoDeclaration(
  selection: OpeningSelectionState,
  playerId: number,
  maxLiberos: number,
): OpeningSelectionState {
  const current = selection.liberoCompetitionRosterPlayerIds;
  const declared = current.includes(playerId);
  if (!declared && current.length >= maxLiberos) return { ...selection, liberoSelectionTouched: true };
  return { ...selection, liberoSelectionTouched: true,
    liberoCompetitionRosterPlayerIds: declared ? current.filter(id => id !== playerId) : [...current, playerId] };
}
