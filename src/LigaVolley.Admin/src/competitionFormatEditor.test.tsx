import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CompetitionFormatsPage, initialDefinition } from './competitionFormatPages';
import { FormatDefinitionEditor, type Definition } from './competitionFormatEditorSections';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe('Competition Format Editor', () => {
  it('shows the empty state and first-format action with an empty database', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('[]', { status: 200, headers: { 'Content-Type': 'application/json' } })));
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter><CompetitionFormatsPage /></MemoryRouter></QueryClientProvider>);
    await waitFor(() => expect(screen.getByText('No hay datos.')).toBeTruthy());
    expect(screen.getByText('+ Nuevo formato')).toBeTruthy();
  });

  it('adds and removes phases as observable local changes', () => {
    let current: Definition = structuredClone(initialDefinition);
    const Wrapper = () => <FormatDefinitionEditor value={current} onChange={next => { current = next; rerender(<Wrapper />); }} />;
    const { rerender } = render(<Wrapper />);
    fireEvent.click(screen.getByText('Agregar fase'));
    expect(current.phases).toHaveLength(2);
    expect(screen.getByText('PHASE_2')).toBeTruthy();
    fireEvent.click(screen.getAllByText('Quitar')[0]);
    expect(current.phases).toHaveLength(1);
    expect(current.phases[0].sequence).toBe(1);
  });

  it('applies scoring presets and reorders tiebreaks', () => {
    let current: Definition = { ...structuredClone(initialDefinition), tiebreakRules: [{ sequence: 1, criterion: 'TablePoints', sortDirection: 'Desc' }, { sequence: 2, criterion: 'MatchWins', sortDirection: 'Desc' }] };
    const Wrapper = () => <FormatDefinitionEditor value={current} onChange={next => { current = next; rerender(<Wrapper />); }} />;
    const { rerender } = render(<Wrapper />);
    fireEvent.click(screen.getByText('Preset 2/1'));
    expect(current.scoringRules.every(rule => rule.winnerTablePoints === 2 && rule.loserTablePoints === 1)).toBe(true);
    const downButtons = screen.getAllByText('↓');
    fireEvent.click(downButtons[downButtons.length - 2]);
    expect(current.tiebreakRules[0].criterion).toBe('MatchWins');
    expect(current.tiebreakRules.map(rule => rule.sequence)).toEqual([1, 2]);
  });
});
