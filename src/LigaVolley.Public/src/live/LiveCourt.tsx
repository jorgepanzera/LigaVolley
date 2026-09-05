import { useEffect, useState } from 'react';
import type { Court, Live } from '../api/types';

// Physical positions placed facing the net; this orders the received projection only.
const HOME_POSITIONS = [5, 4, 6, 3, 1, 2];
const AWAY_POSITIONS = [2, 1, 3, 6, 4, 5];

function LivePlayer({ position }: { position: Court['positions'][number] }) {
  return <li className={`live-player${position.player.isLibero ? ' is-libero' : ''}`}>
    <span className="player-position">P{position.position}</span>
    <strong>#{position.player.jerseyNumber}</strong><span className="player-name">{position.player.displayName}</span>
    {position.player.isLibero && <small>Líbero</small>}
  </li>;
}

export function LiveCourtSide({ name, court, side }: { name: string; court?: Court | null; side: 'home' | 'away' }) {
  const order = side === 'home' ? HOME_POSITIONS : AWAY_POSITIONS;
  return <div className={`live-court-side ${side}`}><h3>{name}</h3>
    {court ? <ol aria-label={`Cancha de ${name}`}>
      {[...court.positions].sort((a, b) => order.indexOf(a.position) - order.indexOf(b.position)).map(position =>
        <LivePlayer key={position.position} position={position} />)}
    </ol> : <p>Cancha aún no disponible</p>}
  </div>;
}

export function LiveCourt({ live }: { live: Live }) {
  const finished = live.status === 'Finished';
  const [expanded, setExpanded] = useState(() => !finished && window.matchMedia('(min-width: 1024px)').matches);
  useEffect(() => {
    const media = window.matchMedia('(min-width: 1024px)');
    const changed = () => setExpanded(!finished && media.matches);
    changed();
    media.addEventListener('change', changed);
    return () => media.removeEventListener('change', changed);
  }, [finished]);
  return <details className="live-court" open={expanded} onToggle={event => setExpanded(event.currentTarget.open)}>
    <summary>{finished ? 'Última formación en cancha' : 'Cancha actual'}<span aria-hidden="true">{expanded ? '−' : '+'}</span></summary>
    <div className="live-court-floor">
      <LiveCourtSide name={live.home.teamName} court={live.homeCourt} side="home" />
      <div className="live-net" aria-hidden="true" />
      <LiveCourtSide name={live.away.teamName} court={live.awayCourt} side="away" />
    </div>
  </details>;
}
