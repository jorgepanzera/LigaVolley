// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PublicApiError, publicApi } from '../api/publicApi';
import { TeamLogo } from '../ui/TeamLogo';
import { LiveMatchView, PublicMatchLive } from './PublicMatchLive';
import { liveFixture, matchFixture } from './liveFixtures';

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date', 'performance'] });
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers(); });
const view = (overrides: Parameters<typeof liveFixture>[0] = {}) => render(<LiveMatchView live={liveFixture(overrides)} receivedAt={performance.now()} />);

describe('public match live UI', () => {
  it.each([[4, 'EN VIVO'], [31, 'actualización demorada'], [91, 'PARTIDO EN CURSO']])('shows freshness at %s seconds', (age, label) => {
    view({ lastUpdatedAt: new Date(Date.parse('2026-09-05T12:00:00Z') - Number(age) * 1000).toISOString() });
    expect(screen.getByRole('status').textContent).toContain(label);
    expect(within(screen.getByRole('group', { name: 'Puntos del set actual' })).getByLabelText('Olimpia: 18')).toBeTruthy();
  });
  it('does not claim freshness for historical null timestamps', () => {
    view({ lastUpdatedAt: null });
    expect(screen.getByText('Hora de actualización no disponible')).toBeTruthy();
    expect(screen.getByRole('status').textContent).not.toContain('EN VIVO');
  });
  it('renders the server supplied by the DTO, independently of P1', () => {
    view();
    const serve = screen.getByRole('group', { name: 'Saque actual' });
    expect(serve.textContent).toContain('#7');
    expect(serve.textContent).toContain('Pérez');
    expect(serve.textContent).not.toContain('Jugador 1');
  });
  it('keeps mobile court collapsed and renders exactly the received six positions per side', () => {
    const { container } = view();
    const details = container.querySelector('details')!;
    expect(details.open).toBe(false);
    fireEvent.click(screen.getByText('Cancha actual'));
    expect(details.open).toBe(true);
    for (const name of ['Olimpia', 'CBPS']) {
      const court = screen.getByRole('list', { name: `Cancha de ${name}` });
      expect(within(court).getAllByRole('listitem')).toHaveLength(6);
      for (let position = 1; position <= 6; position++) {
        expect(within(court).getByText(`P${position}`)).toBeTruthy();
        expect(within(court).getByText(`${name} Jugador ${position}`)).toBeTruthy();
      }
      expect(within(court).getByText('Líbero')).toBeTruthy();
    }
    fireEvent.click(screen.getByText('Cancha actual'));
    expect(details.open).toBe(false);
  });
  it('shows the court initially on desktop and collapses it when finished', () => {
    vi.mocked(window.matchMedia).mockReturnValue({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() } as unknown as MediaQueryList);
    const { container, rerender } = view();
    expect(container.querySelector('details')!.open).toBe(true);
    rerender(<LiveMatchView live={liveFixture({ status: 'Finished' })} receivedAt={performance.now()} />);
    expect(container.querySelector('details')!.open).toBe(false);
  });
  it('keeps suspended state and known score, without active serving', () => {
    view({ status: 'Suspended' });
    expect(screen.getByRole('status').textContent).toContain('PARTIDO SUSPENDIDO');
    expect(screen.getByRole('group', { name: 'Puntos del set actual' })).toBeTruthy();
    expect(screen.queryByRole('group', { name: 'Saque actual' })).toBeNull();
  });
  it('prioritizes final sets and hides serving even if received in the DTO', () => {
    view({ status: 'Finished', home: { teamEntryId: 1, teamName: 'Olimpia', setsWon: 3 } });
    expect(screen.getByRole('status').textContent).toBe('FINAL');
    expect(within(screen.getByRole('group', { name: 'Resultado final en sets' })).getByLabelText('Olimpia: 3')).toBeTruthy();
    expect(screen.queryByRole('group', { name: 'Puntos del set actual' })).toBeNull();
    expect(screen.queryByRole('group', { name: 'Saque actual' })).toBeNull();
    expect(screen.getByText('Última formación en cancha')).toBeTruthy();
  });
  it('hides the serve between sets and when the server is null', () => {
    const { rerender } = view({ servingPlayer: null });
    expect(screen.queryByRole('group', { name: 'Saque actual' })).toBeNull();
    rerender(<LiveMatchView live={liveFixture({ currentSetNumber: 2 })} receivedAt={performance.now()} />);
    expect(screen.queryByRole('group', { name: 'Saque actual' })).toBeNull();
  });
  it('uses a labelled fallback for absent and failed club logos', () => {
    const { rerender } = render(<TeamLogo team={{ teamName: 'Olimpia' }} />);
    expect(screen.getByRole('img', { name: 'Olimpia, sin logo' })).toBeTruthy();
    rerender(<TeamLogo team={{ teamName: 'Olimpia', clubLogoUrl: '/logo.png' }} />);
    fireEvent.error(screen.getByRole('img', { name: 'Logo del club de Olimpia' }));
    expect(screen.getByRole('img', { name: 'Olimpia, sin logo' })).toBeTruthy();
  });
  it.each(['Pending', 'Scheduled', 'Cancelled'] as const)('represents expected absence for %s without requesting Live', status => {
    const request = vi.spyOn(publicApi, 'live');
    render(<PublicMatchLive match={matchFixture({ status, liveAvailable: false })} />);
    expect(screen.getByRole('status').textContent).toMatch(status === 'Cancelled' ? /cancelado/ : /todavía no comenzó/);
    expect(request).not.toHaveBeenCalled();
  });
  it('handles a semantic 404 without an endless loader or technical error', async () => {
    vi.spyOn(publicApi, 'live').mockRejectedValue(new PublicApiError(404, 'public_live_match_not_available'));
    render(<PublicMatchLive match={matchFixture()} />);
    await act(async () => {});
    expect(screen.getByRole('status').textContent).toContain('no está disponible');
    expect(screen.queryByText(/Cargando/)).toBeNull();
  });
  it('preserves valid score after a network failure while its age continues advancing', async () => {
    const request = vi.spyOn(publicApi, 'live').mockResolvedValueOnce(liveFixture()).mockRejectedValue(new Error('network'));
    render(<PublicMatchLive match={matchFixture()} />);
    await act(async () => {});
    await act(async () => { await vi.advanceTimersByTimeAsync(91000); });
    expect(screen.getByRole('group', { name: 'Puntos del set actual' })).toBeTruthy();
    expect(screen.getByText('DATOS SIN ACTUALIZAR')).toBeTruthy();
    expect(screen.getByText(/No se pudo obtener una actualización/)).toBeTruthy();
    expect(request).toHaveBeenCalledTimes(6);
  });
});
