import { useEffect, useState } from 'react';
import type { Live } from '../api/types';
import { classifyLiveFreshness, formatLiveAge, liveAgeSeconds, livePresentation, type LiveFreshness as Freshness } from './livePolicy';

export function useLiveFreshness(live: Live, receivedAt: number) {
  const [now, setNow] = useState(() => performance.now());
  useEffect(() => {
    setNow(performance.now());
    const timer = window.setInterval(() => setNow(performance.now()), 1000);
    return () => clearInterval(timer);
  }, [receivedAt]);
  const elapsed = Math.max(0, now - receivedAt);
  return { age: liveAgeSeconds(live.serverTime, live.lastUpdatedAt, elapsed),
    classification: classifyLiveFreshness(live.serverTime, live.lastUpdatedAt, elapsed) };
}

export function LiveStatusBadge({ live, classification }: { live: Live; classification: Freshness }) {
  const status = livePresentation(live.status, classification);
  const current = live.sets.find(set => set.setNumber === live.currentSetNumber);
  return <div className="live-status" role="status">
    <span className={`live-badge ${status.tone}`}>{status.label}</span>
    {live.status !== 'Finished' && live.currentSetNumber != null && <span className="live-set-number">SET {live.currentSetNumber}
      {current?.status === 'Finished' ? ' · terminado' : current?.status === 'Ready' ? ' · por comenzar' : ''}</span>}
    {live.status === 'InProgress' && classification === 'STALE' && <span className="live-stale-label">DATOS SIN ACTUALIZAR</span>}
    {live.status === 'InProgress' && classification === 'UNKNOWN' && <span className="live-stale-label">Frescura desconocida</span>}
  </div>;
}

export function LiveFreshness({ age, error, unavailable }: { age: number | null; error: boolean; unavailable: boolean }) {
  return <footer className="live-freshness"><p>{formatLiveAge(age)}</p>
    {error && <p className="live-transport" role="status">No se pudo obtener una actualización reciente. Reintentando…</p>}
    {unavailable && <p role="status">La información en vivo dejó de estar disponible.</p>}
    <small>Última información recibida del partido</small>
  </footer>;
}
