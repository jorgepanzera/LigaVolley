// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import { publicApi } from '../api/publicApi';

vi.mock('../api/publicApi', () => ({ publicApi: { seasons: vi.fn(), seasonHome: vi.fn(), competitions: vi.fn(), competition: vi.fn(), fixture: vi.fn(), standings: vi.fn(), match: vi.fn(), live: vi.fn() } }));
const api = publicApi as unknown as Record<string, ReturnType<typeof vi.fn>>;
const season = { seasonId: 7, year: 2026, name: 'LIVOSUR 2026' };
const competition = { competitionId: 3, name: 'Primera', season, division: { divisionId: 1, name: 'Mayores', levelOrder: 1, gender: 'Female' }, periodType: 'Annual', status: 'Scheduled', teams: [], phases: [], upcomingMatches: [], recentResults: [] };

describe('public shell context', () => {
  afterEach(cleanup);
  beforeEach(() => { window.history.replaceState({}, '', '/competitions/3'); Object.values(api).forEach(mock => mock.mockReset()); api.seasons.mockResolvedValue([season]); api.competition.mockResolvedValue(competition); });
  it('keeps the shell visible while a contextual route loads', () => { api.competition.mockReturnValue(new Promise(() => {})); render(<App />); expect(screen.getAllByText('LigaVolley')).toHaveLength(2); expect(screen.getByRole('status').textContent).toContain('Cargando información'); });
  it('renders contextual navigation and its active summary state', async () => { render(<App />); await waitFor(() => expect(screen.getByRole('link', { name: 'Resumen' }).className).toContain('active')); expect(screen.getByRole('link', { name: 'Fixture / Resultados' }).getAttribute('href')).toBe('/competitions/3/fixture'); expect(screen.getAllByRole('link', { name: 'Posiciones' })[0].getAttribute('href')).toBe('/competitions/3/standings'); });
  it('retains the canonical season route in the selector', async () => { window.history.replaceState({}, '', '/seasons/7'); api.seasonHome.mockResolvedValue({ season, activeCompetitions: [], liveMatches: [], upcomingMatches: [], recentResults: [], finishedCompetitions: [] }); render(<App />); await waitFor(() => expect(screen.getByRole('heading', { name: 'LIVOSUR 2026' })).toBeTruthy()); expect((screen.getByLabelText('Temporada') as HTMLSelectElement).value).toBe('7'); });
});
