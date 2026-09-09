// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import type { Fixture, FixtureMatch } from '../api/types';
import { FixtureResults } from './FixtureResults';

afterEach(cleanup);

const match = (id: number, status: FixtureMatch['status'], home: string): FixtureMatch => ({ matchId: id, matchNumber: id, status, homeTeam: { teamEntryId: id, teamName: home }, awayTeam: { teamEntryId: id + 20, teamName: `Away ${id}` }, matchDate: '2026-09-10T20:00:00Z' });
const fixture: Fixture = { competitionId: 1, competitionName: 'LIVOSUR', phases: [
  { phaseId: 1, code: 'REG', name: 'Regular', phaseType: 'RoundRobin', phaseRole: 'Regular', sequence: 1, rounds: [{ roundNumber: 1, matches: [match(1, 'Scheduled', 'Serbia'), match(2, 'Finished', 'China')] }, { roundNumber: 2, matches: [match(3, 'InProgress', 'Türkiye')] }], groups: [], series: [] },
  { phaseId: 2, code: 'GROUPS', name: 'Segunda fase', phaseType: 'GroupStage', phaseRole: 'Championship', sequence: 2, rounds: [], groups: [{ phaseGroupId: 21, code: 'A', name: 'Grupo A', groupRole: 'Championship', sequence: 1, rounds: [{ roundNumber: 1, matches: [match(4, 'Scheduled', 'Argentina')] }] }, { phaseGroupId: 22, code: 'B', name: 'Grupo B', groupRole: 'Relegation', sequence: 2, rounds: [{ roundNumber: 1, matches: [match(5, 'Finished', 'Brasil')] }] }], series: [] },
  { phaseId: 3, code: 'PO', name: 'Playoffs', phaseType: 'Playoff', phaseRole: 'Final', sequence: 3, rounds: [], groups: [], series: [{ seriesId: 31, code: 'SF1', name: 'Semifinal 1', sequence: 1, status: 'InProgress', team1InitialWins: 1, team2InitialWins: 0, team1Wins: 1, team2Wins: 0, winsRequired: 2, matches: [match(6, 'Scheduled', 'Uruguay')] }] }
] };

function Location() { return <output data-testid="location">{useLocation().search}</output>; }
function view(entry = '/competitions/1/fixture') { return render(<MemoryRouter initialEntries={[entry]}><FixtureResults fixture={fixture} /><Location /></MemoryRouter>); }

describe('public fixture and results', () => {
  it('shows all statuses and preserves phase and round structure', () => {
    view();
    expect(screen.getByRole('link', { name: /Serbia contra Away 1/i })).toBeTruthy();
    expect(screen.getByRole('link', { name: /China contra Away 2/i })).toBeTruthy();
    expect(screen.getByRole('link', { name: /Türkiye contra Away 3/i })).toBeTruthy();
    expect(screen.getAllByRole('heading', { name: 'Ronda 1' })).toHaveLength(3);
    expect(screen.getByRole('heading', { name: 'Regular' })).toBeTruthy();
  });

  it('filters upcoming and results without leaving empty structural containers', () => {
    view();
    fireEvent.click(screen.getByRole('button', { name: 'Próximos' }));
    expect(screen.getByRole('link', { name: /Serbia contra Away 1/i })).toBeTruthy();
    expect(screen.queryByRole('link', { name: /China contra Away 2/i })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Ronda 2' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Resultados' }));
    expect(screen.getByRole('link', { name: /China contra Away 2/i })).toBeTruthy();
    expect(screen.queryByRole('link', { name: /Serbia contra Away 1/i })).toBeNull();
  });

  it('keeps group rounds and playoff series distinct', () => {
    view();
    expect(screen.getByRole('heading', { name: 'Grupo A' })).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Semifinal 1' })).toBeTruthy();
    expect(screen.queryByText('Ronda 6')).toBeNull();
  });

  it('filters by phase and resets the selected group when phase changes', () => {
    view('/competitions/1/fixture?phase=2&group=21');
    expect(screen.getByRole('link', { name: /Argentina contra Away 4/i })).toBeTruthy();
    expect(screen.queryByRole('link', { name: /Brasil contra Away 5/i })).toBeNull();
    fireEvent.change(screen.getByLabelText('Fase'), { target: { value: '1' } });
    expect(screen.getByRole('link', { name: /Serbia contra Away 1/i })).toBeTruthy();
    expect(screen.getByTestId('location').textContent).toBe('?phase=1');
  });

  it('shows one contextual empty state and preserves match detail navigation', () => {
    view('/competitions/1/fixture?phase=3&matches=results');
    expect(screen.getByText('Todavía no hay resultados en Playoffs.')).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Semifinal 1' })).toBeNull();
    cleanup();
    view();
    expect(screen.getByRole('link', { name: /Serbia contra Away 1/i }).getAttribute('href')).toBe('/matches/1');
  });

  it('restores status filters from the URL', () => {
    view('/competitions/1/fixture?matches=results');
    expect(screen.getByRole('button', { name: 'Resultados' }).getAttribute('aria-pressed')).toBe('true');
    expect(screen.getByTestId('location').textContent).toBe('?matches=results');
  });
});
