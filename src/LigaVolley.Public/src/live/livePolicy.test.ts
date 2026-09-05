import { describe, expect, it } from 'vitest';
import { classifyLiveFreshness, formatLiveAge, liveAgeSeconds, livePresentation, normalDelay, retryDelay } from './livePolicy';

const serverTime = '2026-09-05T12:00:00Z';
describe('public livescore presentation policy', () => {
  it.each([[0, 'FRESH'], [30, 'FRESH'], [30.5, 'DELAYED'], [31, 'DELAYED'], [90, 'DELAYED'], [91, 'STALE']])('classifies %s seconds as %s', (age, expected) => {
    const updated = new Date(Date.parse(serverTime) - Number(age) * 1000).toISOString();
    expect(classifyLiveFreshness(serverTime, updated)).toBe(expected);
  });
  it('keeps null and invalid timestamps unknown', () => {
    expect(classifyLiveFreshness(serverTime, null)).toBe('UNKNOWN');
    expect(classifyLiveFreshness(serverTime, 'invalid')).toBe('UNKNOWN');
    expect(formatLiveAge(null)).toMatch(/no disponible/);
    expect(livePresentation('InProgress', 'UNKNOWN').label).not.toBe('EN VIVO');
  });
  it('advances relative time without consulting the client wall clock', () => {
    expect(liveAgeSeconds(serverTime, serverTime, 31000)).toBe(31);
    expect(classifyLiveFreshness(serverTime, serverTime, 91000)).toBe('STALE');
    expect(formatLiveAge(72)).toContain('1 min 12 s');
  });
  it('clamps future timestamps', () => expect(liveAgeSeconds(serverTime, '2026-09-05T12:01:00Z')).toBe(0));
  it('keeps sporting state independent from freshness', () => {
    expect(livePresentation('Suspended', 'FRESH').label).toBe('PARTIDO SUSPENDIDO');
    expect(livePresentation('Finished', 'STALE').label).toBe('FINAL');
    expect(livePresentation('InProgress', 'STALE').label).toBe('PARTIDO EN CURSO');
  });
  it('preserves polling and backoff', () => {
    expect(normalDelay('InProgress')).toBe(5000);
    expect(normalDelay('Suspended')).toBe(15000);
    expect(normalDelay('Finished')).toBeNull();
    expect([1, 2, 3, 4, 5].map(retryDelay)).toEqual([5000, 10000, 20000, 30000, 30000]);
  });
});
