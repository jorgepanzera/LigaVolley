import type { OpenTeamContext, OpenTeamSelection } from '../../api/scorerApi';

// Only the opening context supplies competitive roles. Once opened, MATCH_LIBERO is authoritative.
export function toggleOpeningPlayer(
  selection: OpenTeamSelection,
  player: OpenTeamContext['players'][number],
): OpenTeamSelection {
  const id = player.competitionRosterPlayerId;
  const selected = selection.players.some((candidate) => candidate.competitionRosterPlayerId === id);
  const liberos = selection.liberoCompetitionRosterPlayerIds.filter((candidate) => candidate !== id);
  return {
    ...selection,
    players: selected
      ? selection.players.filter((candidate) => candidate.competitionRosterPlayerId !== id)
      : [...selection.players, { competitionRosterPlayerId: id, isMatchCaptain: false }],
    liberoCompetitionRosterPlayerIds:
      !selected && player.role.toUpperCase() === 'LIBERO' ? [...liberos, id] : liberos,
  };
}
