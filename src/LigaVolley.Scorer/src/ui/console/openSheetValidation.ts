export interface OpeningPlayerSelection {
  competitionRosterPlayerId: number;
  jerseyNumber?: number;
  isMatchCaptain: boolean;
}

export function isOpeningTeamValid(players: OpeningPlayerSelection[]) {
  const jerseys = players.map((player) => player.jerseyNumber);
  return (
    players.length >= 6 &&
    jerseys.every((jersey) => jersey !== undefined && jersey >= 1 && jersey <= 99) &&
    new Set(jerseys).size === jerseys.length &&
    players.filter((player) => player.isMatchCaptain).length === 1
  );
}
