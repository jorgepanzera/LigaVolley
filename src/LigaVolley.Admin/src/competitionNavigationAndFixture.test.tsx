import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import { CompetitionTable, RoundMatchList } from './pages';
import type { CompetitionSummary, Match } from './types/admin';

afterEach(cleanup);

const competition = (status: CompetitionSummary['status']): CompetitionSummary => ({ competitionId: 24, name: 'LIVOSUR', seasonYear: 2026, divisionName: 'Mayores', gender: 'Female', formatName: 'Regular', periodType: 'Annual', status });
const match = (matchId: number, roundNumber: number | null, home: string): Match => ({ matchId, roundNumber, matchNumber: matchId, homeTeam: { teamEntryId: matchId, teamName: home, status: 'Active' }, awayTeam: { teamEntryId: matchId + 10, teamName: `Rival ${matchId}`, status: 'Active' }, status: 'Scheduled' });

describe('competition next actions', () => {
  it.each([
    ['Draft', 'Preparar competición', '/admin/competitions/24/entries'],
    ['Scheduled', 'Ver fixture', '/admin/competitions/24/fixture'],
    ['InProgress', 'Ver progreso', '/admin/competitions/24/progression'],
    ['Finished', 'Ver resumen', '/admin/competitions/24/overview'],
    ['Cancelled', 'Ver resumen', '/admin/competitions/24/overview']
  ] as const)('links %s to its workspace destination', (status, label, href) => {
    render(<MemoryRouter><CompetitionTable rows={[competition(status)]} /></MemoryRouter>);
    expect(screen.getByRole('link', { name: label }).getAttribute('href')).toBe(href);
  });
});

describe('fixture rounds', () => {
  it('groups matches by round and keeps unassigned matches visible', () => {
    render(<MemoryRouter><RoundMatchList title="Regular" matches={[match(1, 1, 'Serbia'), match(2, 1, 'China'), match(3, 2, 'Türkiye'), match(4, null, 'Argentina')]} /></MemoryRouter>);
    expect(screen.getAllByRole('heading', { name: 'Round 1' })).toHaveLength(1);
    expect(screen.getAllByRole('heading', { name: 'Round 2' })).toHaveLength(1);
    expect(screen.getByRole('heading', { name: 'Sin ronda' })).toBeTruthy();
    const roundOne = screen.getByRole('heading', { name: 'Round 1' }).parentElement!;
    expect(within(roundOne).getByText('Serbia vs Rival 1')).toBeTruthy();
    expect(within(roundOne).getByText('China vs Rival 2')).toBeTruthy();
    const roundTwo = screen.getByRole('heading', { name: 'Round 2' }).parentElement!;
    expect(within(roundTwo).getByText('Türkiye vs Rival 3')).toBeTruthy();
    expect(screen.getByText('Argentina vs Rival 4')).toBeTruthy();
  });
});
