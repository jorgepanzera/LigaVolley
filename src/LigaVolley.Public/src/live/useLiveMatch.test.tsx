// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { publicApi, PublicApiError } from '../api/publicApi';
import { useLiveMatch } from './useLiveMatch';
import { liveFixture } from './liveFixtures';

beforeEach(() => vi.useFakeTimers());
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.useRealTimers(); });
const tick = async (ms = 0) => act(async () => { await vi.advanceTimersByTimeAsync(ms); });

it('polls at 5s, then 15s when suspended, and stops on final including visibility refresh', async () => {
  const request = vi.spyOn(publicApi, 'live').mockResolvedValueOnce(liveFixture())
    .mockResolvedValueOnce(liveFixture({ status: 'Suspended' })).mockResolvedValue(liveFixture({ status: 'Finished' }));
  renderHook(() => useLiveMatch(1, true));
  await tick();
  await tick(4999); expect(request).toHaveBeenCalledTimes(1);
  await tick(1); expect(request).toHaveBeenCalledTimes(2);
  await tick(14999); expect(request).toHaveBeenCalledTimes(2);
  await tick(1); expect(request).toHaveBeenCalledTimes(3);
  await tick(60000);
  act(() => document.dispatchEvent(new Event('visibilitychange')));
  expect(request).toHaveBeenCalledTimes(3);
});

it('loads a finished resource once', async () => {
  const request = vi.spyOn(publicApi, 'live').mockResolvedValue(liveFixture({ status: 'Finished' }));
  renderHook(() => useLiveMatch(1, true));
  await tick(60000);
  expect(request).toHaveBeenCalledTimes(1);
});

it('uses 5/10/20/30s backoff, retains state, and resets the error and cadence on recovery', async () => {
  const initial = liveFixture();
  const request = vi.spyOn(publicApi, 'live').mockResolvedValueOnce(initial).mockRejectedValue(new Error('offline'));
  const { result } = renderHook(() => useLiveMatch(1, true));
  await tick();
  await tick(5000);
  expect(result.current.data).toBe(initial);
  expect(result.current.error).toBe(true);
  for (const delay of [5000, 10000, 20000, 30000]) {
    const calls = request.mock.calls.length;
    await tick(delay - 1); expect(request).toHaveBeenCalledTimes(calls);
    await tick(1); expect(request).toHaveBeenCalledTimes(calls + 1);
  }
  request.mockResolvedValue(liveFixture());
  await tick(30000); expect(result.current.error).toBe(false);
  const calls = request.mock.calls.length;
  await tick(5000); expect(request).toHaveBeenCalledTimes(calls + 1);
});

it('refreshes on visibility without overlapping or duplicating timers', async () => {
  const request = vi.spyOn(publicApi, 'live').mockResolvedValue(liveFixture());
  renderHook(() => useLiveMatch(1, true));
  await tick(2000);
  act(() => { document.dispatchEvent(new Event('visibilitychange')); document.dispatchEvent(new Event('visibilitychange')); });
  await tick();
  expect(request).toHaveBeenCalledTimes(2);
  await tick(4999); expect(request).toHaveBeenCalledTimes(2);
  await tick(1); expect(request).toHaveBeenCalledTimes(3);
});

it('aborts a departed match and ignores its late response', async () => {
  let resolveOld!: (value: ReturnType<typeof liveFixture>) => void;
  const request = vi.spyOn(publicApi, 'live').mockReturnValueOnce(new Promise(resolve => { resolveOld = resolve; }))
    .mockResolvedValue(liveFixture({ matchId: 2 }));
  const { result, rerender, unmount } = renderHook(({ id }) => useLiveMatch(id, true), { initialProps: { id: 1 } });
  rerender({ id: 2 });
  expect(request.mock.calls[0][1]?.aborted).toBe(true);
  await tick();
  await act(async () => resolveOld(liveFixture()));
  expect(result.current.data?.matchId).toBe(2);
  unmount();
  await tick(60000);
  expect(request).toHaveBeenCalledTimes(2);
});

it('distinguishes semantic absence from generic 404 and server failures', async () => {
  const request = vi.spyOn(publicApi, 'live').mockRejectedValue(new PublicApiError(404, 'public_match_not_found'));
  const { result } = renderHook(() => useLiveMatch(1, true));
  await tick(); expect(result.current.error).toBe(true); expect(result.current.unavailable).toBe(false);
  request.mockRejectedValue(new PublicApiError(404, 'public_live_match_not_available'));
  await tick(5000); expect(result.current.error).toBe(false); expect(result.current.unavailable).toBe(true);
  await tick(60000); expect(request).toHaveBeenCalledTimes(2);
});
