// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import type { Competition, FixtureMatch, PlayoffSeries } from '../api/types';
import { Bracket } from './components';
import { PlayoffsResults } from './PlayoffsResults';

afterEach(cleanup);
const match = (id: number): FixtureMatch => ({ matchId: id, matchNumber: id, status: 'Scheduled', homeTeam: { teamEntryId: id, teamName: `Home ${id}` }, awayTeam: { teamEntryId: id + 10, teamName: `Away ${id}` } });
const series = (overrides: Partial<PlayoffSeries> = {}): PlayoffSeries => ({ seriesId: 1, code: 'SF1', name: 'Semifinal 1', sequence: 1, status: 'InProgress', team1: { side: 1, team: { teamEntryId: 1, teamName: 'Olimpia' } }, team2: { side: 2, source: { sourceType: 'SeriesWinner', sourceSeriesCode: 'SF2', displayName: 'Ganador de Semifinal 2' } }, team1InitialWins: 1, team2InitialWins: 0, team1RealWins: 1, team2RealWins: 0, team1SeriesWins: 2, team2SeriesWins: 0, winsRequired: 2, matches: [match(1)], ...overrides });
const competition = (phases: Competition['phases']): Competition => ({ competitionId: 1, name: 'LIVOSUR', season: { seasonId: 1, year: 2026, name: '2026' }, division: { divisionId: 1, name: 'Primera', levelOrder: 1, gender: 'Female' }, periodType: 'Annual', status: 'InProgress', teams: [], phases, upcomingMatches: [], recentResults: [] });
const viewBracket = (items: PlayoffSeries[]) => render(<MemoryRouter><Bracket series={items} /></MemoryRouter>);

describe('public playoffs', () => {
  it('renders series in received order with teams, source, wins, status and initial advantage', () => {
    viewBracket([series(), series({ seriesId: 2, code: 'F', name: 'Final', sequence: 2, status: 'Ready', matches: [] })]);
    expect(screen.getAllByText('Olimpia')).toHaveLength(2);
    expect(screen.getAllByText('Ganador de Semifinal 2 (SF2)')).toHaveLength(2);
    expect(screen.getByText('En juego')).toBeTruthy();
    expect(screen.getAllByText(/Ventaja inicial: 1–0/)).toHaveLength(2);
    expect(screen.getByText('Sin partidos materializados todavía.')).toBeTruthy();
    const titles = screen.getAllByRole('article').map(item => item.textContent);
    expect(titles[0]).toContain('Semifinal 1');
    expect(titles[1]).toContain('Final');
  });

  it('keeps materialized match navigation and does not create a fictitious match for the advantage', () => {
    viewBracket([series({ matches: [match(7)] })]);
    expect(screen.getByRole('link', { name: /Home 7 contra Away 7/i }).getAttribute('href')).toBe('/matches/7');
    expect(screen.queryByRole('link', { name: /Home 2 contra/i })).toBeNull();
  });

  it('uses only the explicit winner and keeps unresolved sides neutral', () => {
    viewBracket([series({ status: 'Finished', winnerTeamEntryId: 1, team2: { side: 2 }, matches: [] })]);
    expect(screen.getByText('Por definir')).toBeTruthy();
    expect(screen.getByText('Finalizado')).toBeTruthy();
    expect(screen.getByText('Olimpia').parentElement?.className).toContain('winner');
  });

  it('creates desktop layout relations only from explicit winner and loser sources', () => {
    const sf1 = series({ seriesId: 1, code: 'SF1', name: 'Serie A', team2: { side: 2, team: { teamEntryId: 2, teamName: 'Rival A' } } });
    const sf2 = series({ seriesId: 2, code: 'SF2', name: 'Serie B', team1: { side: 1, team: { teamEntryId: 3, teamName: 'Equipo B' } }, team2: { side: 2, team: { teamEntryId: 4, teamName: 'Rival B' } } });
    const final = series({ seriesId: 3, code: 'FINAL', name: 'Definición', team1: { side: 1, source: { sourceType: 'SeriesWinner', sourceSeriesCode: 'SF1', displayName: 'Ganador A' } }, team2: { side: 2, source: { sourceType: 'SeriesWinner', sourceSeriesCode: 'SF2', displayName: 'Ganador B' } }, matches: [] });
    const third = series({ seriesId: 4, code: 'THIRD', name: 'Consolación', team1: { side: 1, source: { sourceType: 'SeriesLoser', sourceSeriesCode: 'SF1', displayName: 'Perdedor A' } }, team2: { side: 2, source: { sourceType: 'SeriesLoser', sourceSeriesCode: 'SF2', displayName: 'Perdedor B' } }, matches: [] });
    viewBracket([sf1, sf2, final, third]);
    expect(document.querySelector('.bracket-layout')).toBeTruthy();
    expect(document.querySelector('[data-series-code="FINAL"]')?.parentElement?.getAttribute('data-relations')).toBe('winner:SF1,winner:SF2');
    expect(document.querySelector('[data-series-code="THIRD"]')?.parentElement?.getAttribute('data-relations')).toBe('loser:SF1,loser:SF2');
    expect(document.querySelectorAll('.bracket-connector-winner')).toHaveLength(2);
    expect(document.querySelectorAll('.bracket-connector-loser')).toHaveLength(2);
  });

  it('does not infer a layout relation from series names, codes, or order', () => {
    viewBracket([series({ code: 'SF1', name: 'Semifinal 1', team2: { side: 2 } }), series({ seriesId: 2, code: 'FINAL', name: 'Final', team1: { side: 1 }, team2: { side: 2 }, matches: [] })]);
    expect(document.querySelector('.bracket-list')).toBeTruthy();
    expect(document.querySelector('.bracket-connector')).toBeNull();
  });

  it('shows phases in order and a semantic empty state without playoff series', () => {
    const phases: Competition['phases'] = [{ phaseId: 2, code: 'SF', name: 'Semifinales', phaseType: 'Playoff', phaseRole: 'Semifinal', sequence: 1, status: 'InProgress', groups: [], playoffSeries: [series()] }, { phaseId: 3, code: 'F', name: 'Final', phaseType: 'Playoff', phaseRole: 'Final', sequence: 2, status: 'Pending', groups: [], playoffSeries: [series({ seriesId: 2, name: 'Final', code: 'F' })] }];
    render(<MemoryRouter><PlayoffsResults competition={competition(phases)} /></MemoryRouter>);
    expect(screen.getAllByRole('heading', { level: 2 }).map(item => item.textContent)).toEqual(['Semifinales', 'Final']);
    cleanup();
    render(<MemoryRouter><PlayoffsResults competition={competition([])} /></MemoryRouter>);
    expect(screen.getByText('Los playoffs todavía no están disponibles.')).toBeTruthy();
  });
});
