// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import type { Standings, StandingsTable } from '../api/types';
import { StandingsResults } from './StandingsResults';

afterEach(cleanup);

const table = (phaseId: number, phaseName: string, groupId?: number, groupName?: string, rows: StandingsTable['rows'] = [{ position: 1, teamEntryId: phaseId * 10 + (groupId ?? 0), teamName: `${phaseName} ${groupName ?? 'General'}`, played: 3, won: 2, lost: 1, setsWon: 7, setsLost: 4, setRatio: null, pointsWon: 200, pointsLost: 180, pointRatio: null, tablePoints: 5, isTied: false }]): StandingsTable => ({ phaseId, phaseName, phaseSequence: phaseId, phaseGroupId: groupId, phaseGroupName: groupName, phaseGroupSequence: groupId, isFinal: false, rows });
const standings: Standings = { competitionId: 1, competitionName: 'LIVOSUR', tables: [table(1, 'Regular'), table(2, 'Segunda fase', 21, 'Championship'), table(2, 'Segunda fase', 22, 'Relegation')] };
function Location() { return <output data-testid="location">{useLocation().search}</output>; }
function view(data = standings, entry = '/competitions/1/standings') { return render(<MemoryRouter initialEntries={[entry]}><StandingsResults standings={data} /><Location /></MemoryRouter>); }

describe('public standings', () => {
  it('selects the first canonical table without unnecessary selectors for a single phase', () => {
    view({ ...standings, tables: [standings.tables[0]] });
    expect(screen.queryByLabelText('Fase')).toBeNull();
    expect(screen.queryByLabelText('Grupo')).toBeNull();
    expect(screen.getByText('Regular General')).toBeTruthy();
  });

  it('selects phase and group without combining tables', () => {
    view();
    expect(screen.getByLabelText('Fase')).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Fase'), { target: { value: '2' } });
    expect(screen.getByLabelText('Grupo')).toBeTruthy();
    expect(screen.getByText('Segunda fase Championship')).toBeTruthy();
    expect(screen.queryByText('Segunda fase Relegation')).toBeNull();
    fireEvent.change(screen.getByLabelText('Grupo'), { target: { value: '22' } });
    expect(screen.getByText('Segunda fase Relegation')).toBeTruthy();
    expect(screen.queryByText('Segunda fase Championship')).toBeNull();
  });

  it('disables the group selector with an explicit label until public groups can be selected', () => {
    view({ ...standings, tables: [table(1, 'Regular'), table(1, 'Regular', 11)] });
    const group = screen.getByLabelText('Grupo') as HTMLSelectElement;
    expect(group.disabled).toBe(true);
    expect(group.options[0].text).toBe('Grupos aún no disponibles');
  });

  it('clears group on phase changes and restores a valid query selection', () => {
    view(standings, '/competitions/1/standings?phase=2&group=22');
    expect(screen.getByText('Segunda fase Relegation')).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Fase'), { target: { value: '1' } });
    expect(screen.queryByLabelText('Grupo')).toBeNull();
    expect(screen.getByTestId('location').textContent).toBe('?phase=1');
  });

  it('normalizes invalid query selections to the first public table', () => {
    view(standings, '/competitions/1/standings?phase=99&group=22');
    expect(screen.getByText('Regular General')).toBeTruthy();
    expect(screen.queryByLabelText('Grupo')).toBeNull();
  });

  it('preserves backend ties, order, and nullable ratios', () => {
    const rows = [{ ...standings.tables[0].rows[0], position: 2, teamEntryId: 1, teamName: 'Primero', isTied: true }, { ...standings.tables[0].rows[0], position: 2, teamEntryId: 2, teamName: 'Segundo', isTied: true }];
    view({ ...standings, tables: [table(1, 'Regular', undefined, undefined, rows)] });
    expect(screen.getAllByText('2=')).toHaveLength(2);
    expect(screen.getAllByText('—')).toHaveLength(4);
    const names = screen.getAllByRole('row').slice(1).map(row => row.textContent);
    expect(names[0]).toContain('Primero');
    expect(names[1]).toContain('Segundo');
  });

  it('shows a valid empty-state when no standings exist', () => {
    view({ competitionId: 1, competitionName: 'LIVOSUR', tables: [] });
    expect(screen.getByText('Todavía no hay posiciones disponibles para esta fase.')).toBeTruthy();
  });
});
