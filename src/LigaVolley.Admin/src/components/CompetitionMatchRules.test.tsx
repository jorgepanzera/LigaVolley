import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, expect, it, vi } from 'vitest';
import { CompetitionMatchRulesEditor } from './CompetitionMatchRules';
import { adminApi } from '../api/adminApiClient';
import type { Competition } from '../types/admin';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
function show(status = 'InProgress') {
  const competition = { competitionId: 9, status, matchRules: {
    maxSubstitutionsPerSetOverride: null, maxTimeoutsPerSetOverride: 3,
    defaultMaxSubstitutionsPerSet: 6, defaultMaxTimeoutsPerSet: 2,
    effectiveMaxSubstitutionsPerSet: 6, effectiveMaxTimeoutsPerSet: 3,
  } } as Competition;
  render(<QueryClientProvider client={new QueryClient()}><CompetitionMatchRulesEditor competition={competition} /></QueryClientProvider>);
}
it('edits an override and restores inheritance with nullable values', async () => {
  const put = vi.spyOn(adminApi, 'put').mockResolvedValue({});
  show();
  expect(screen.getByText('Heredado del formato: 6')).toBeTruthy();
  fireEvent.change(screen.getByLabelText('Override de sustituciones'), { target: { value: '8' } });
  fireEvent.change(screen.getByLabelText('Override de timeouts'), { target: { value: '' } });
  fireEvent.click(screen.getByRole('button', { name: 'Guardar reglas' }));
  await waitFor(() => expect(put).toHaveBeenCalledWith('/competitions/9/match-rules', {
    maxSubstitutionsPerSetOverride: 8, maxTimeoutsPerSetOverride: null,
  }));
});
it('keeps completed competition rules read-only', () => {
  show('Finished');
  expect((screen.getByLabelText('Override de sustituciones') as HTMLInputElement).disabled).toBe(true);
  expect(screen.queryByRole('button', { name: 'Guardar reglas' })).toBeNull();
});
