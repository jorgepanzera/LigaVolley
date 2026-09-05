import type { Live, MatchDetail } from '../api/types';
import { LiveCourt } from './LiveCourt';
import { LiveFreshness, LiveStatusBadge, useLiveFreshness } from './LiveFreshness';
import { LiveScoreboard, LiveTeam, ServeIndicator } from './LiveScoreboard';
import { noLiveMessage } from './livePolicy';
import { useLiveMatch } from './useLiveMatch';
import './live.css';

export function LiveMatchView({ live, receivedAt, error = false, unavailable = false }: {
  live: Live; receivedAt: number; error?: boolean; unavailable?: boolean;
}) {
  const freshness = useLiveFreshness(live, receivedAt);
  return <section className="public-live" aria-label="Marcador del partido">
    <LiveStatusBadge live={live} classification={freshness.classification} />
    <LiveScoreboard home={live.home} away={live.away} homeSets={live.home.setsWon} awaySets={live.away.setsWon}
      current={live.sets.find(set => set.setNumber === live.currentSetNumber)} finished={live.status === 'Finished'}
      sets={live.sets.filter(set => set.status === 'Finished')} />
    <ServeIndicator live={live} />
    {(live.homeCourt || live.awayCourt) && <LiveCourt live={live} />}
    <LiveFreshness age={freshness.age} error={error} unavailable={unavailable} />
  </section>;
}

export function PublicMatchLive({ match }: { match: MatchDetail }) {
  const live = useLiveMatch(match.matchId, match.liveAvailable);
  if (live.data) return <LiveMatchView key={match.matchId} live={live.data} receivedAt={live.receivedAt!}
    error={live.error} unavailable={live.unavailable} />;
  return <section className="public-live" aria-label="Marcador del partido">
    {match.status === 'Finished' && match.result ? <>
      <p className="live-status"><span className="live-badge final">FINAL</span></p>
      <LiveScoreboard home={match.homeTeam} away={match.awayTeam} homeSets={match.result.homeSets}
        awaySets={match.result.awaySets} finished sets={match.result.sets} />
    </> : <div className="live-teams"><LiveTeam team={match.homeTeam} /><LiveTeam team={match.awayTeam} /></div>}
    <p role="status" className="live-empty">{!match.liveAvailable || live.unavailable ? noLiveMessage(match.status) :
      live.error ? 'No se pudo cargar la información en vivo. Reintentando…' : 'Cargando información en vivo…'}</p>
  </section>;
}
