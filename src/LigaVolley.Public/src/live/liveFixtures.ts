import type { Court, Live, MatchDetail } from '../api/types';

export function liveFixture(overrides: Partial<Live> = {}): Live {
  const court = (team: string): Court => ({ positions: [6, 3, 1, 5, 2, 4].map(position => ({ position,
    player: { jerseyNumber: String(10 + position), displayName: `${team} Jugador ${position}`, isLibero: position === 6 } })) });
  return { matchId: 1, status: 'InProgress', home: { teamEntryId: 1, teamName: 'Olimpia', setsWon: 1 },
    away: { teamEntryId: 2, teamName: 'CBPS', setsWon: 1 }, currentSetNumber: 3,
    sets: [{ setNumber: 1, status: 'Finished', homePoints: 25, awayPoints: 19, winnerSide: 'Home' },
      { setNumber: 2, status: 'Finished', homePoints: 21, awayPoints: 25, winnerSide: 'Away' },
      { setNumber: 3, status: 'InProgress', homePoints: 18, awayPoints: 20 }],
    servingSide: 'Away', servingPlayer: { jerseyNumber: 7, displayName: 'Pérez' },
    homeCourt: court('Olimpia'), awayCourt: court('CBPS'), serverTime: '2026-09-05T12:00:00Z', lastUpdatedAt: '2026-09-05T11:59:56Z', ...overrides };
}

export function matchFixture(overrides: Partial<MatchDetail> = {}): MatchDetail {
  const live = liveFixture();
  return { matchId: 1, competition: { competitionId: 1, competitionName: 'LIVOSUR', seasonYear: 2026, divisionName: 'Primera', gender: 'Female' },
    scope: { phaseName: 'Regular' }, homeTeam: live.home, awayTeam: live.away, status: 'InProgress', liveAvailable: true, ...overrides };
}
