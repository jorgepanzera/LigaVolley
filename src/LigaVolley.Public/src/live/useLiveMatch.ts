import { useEffect, useState } from 'react';
import { PublicApiError, publicApi } from '../api/publicApi';
import type { Live } from '../api/types';
import { normalDelay, retryDelay } from './livePolicy';

interface LiveState { data?: Live; receivedAt?: number; error: boolean; unavailable: boolean }
export function useLiveMatch(matchId: number, enabled: boolean) {
  const [state, setState] = useState<LiveState & { matchId: number }>({ matchId, error: false, unavailable: false });
  useEffect(() => {
    let disposed = false;
    let finished = false;
    let inFlight = false;
    let failures = 0;
    let timer: number | undefined;
    const controller = new AbortController();
    setState({ matchId, error: false, unavailable: false });
    if (!enabled) return;

    const refresh = async () => {
      if (disposed || finished || inFlight) return;
      clearTimeout(timer);
      inFlight = true;
      try {
        const data = await publicApi.live(matchId, controller.signal);
        if (disposed) return;
        setState({ matchId, data, receivedAt: performance.now(), error: false, unavailable: false });
        failures = 0;
        const delay = normalDelay(data.status);
        finished = delay === null;
        if (delay !== null) timer = window.setTimeout(refresh, delay);
      } catch (error) {
        if (disposed) return;
        const unavailable = error instanceof PublicApiError && error.status === 404 && error.code === 'public_live_match_not_available';
        setState(previous => ({ ...previous, error: !unavailable, unavailable }));
        if (unavailable) finished = true;
        else timer = window.setTimeout(refresh, retryDelay(++failures));
      } finally {
        inFlight = false;
      }
    };
    const visible = () => { if (document.visibilityState === 'visible') void refresh(); };
    void refresh();
    document.addEventListener('visibilitychange', visible);
    return () => {
      disposed = true;
      controller.abort();
      clearTimeout(timer);
      document.removeEventListener('visibilitychange', visible);
    };
  }, [matchId, enabled]);
  return state.matchId === matchId ? state : { error: false, unavailable: false } as LiveState;
}
