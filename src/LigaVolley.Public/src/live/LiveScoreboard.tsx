import type { Live, SetResult, Team } from '../api/types';
import { TeamLogo } from '../ui/TeamLogo';

export function LiveTeam({ team }: { team: Team }) {
  return <div className="live-team"><TeamLogo team={team} size={72} lazy={false} /><h2>{team.teamName}</h2></div>;
}

export function SetScoreHistory({ sets }: { sets: SetResult[] }) {
  return <div className="set-history" aria-label="Resultados de sets">
    {sets.length ? <ol>{[...sets].sort((a, b) => a.setNumber - b.setNumber).map(set =>
      <li key={set.setNumber}><span>Set {set.setNumber}</span><b>{set.homePoints} <span>–</span> {set.awayPoints}</b></li>
    )}</ol> : <p>Aún no hay sets finalizados</p>}
  </div>;
}

export function LiveScoreboard({ home, away, homeSets, awaySets, current, finished, sets }: {
  home: Team; away: Team; homeSets: number; awaySets: number;
  current?: SetResult; finished: boolean; sets: SetResult[];
}) {
  return <div className={`live-scoreboard${finished ? ' is-final' : ''}`}>
    <div className="live-teams"><LiveTeam team={home} /><LiveTeam team={away} /></div>
    <div className="live-points" role="group" aria-label={finished ? 'Resultado final en sets' : 'Puntos del set actual'}>
      <strong aria-label={`${home.teamName}: ${finished ? homeSets : current?.homePoints ?? 'sin datos'}`}>{finished ? homeSets : current?.homePoints ?? '—'}</strong>
      <span aria-hidden="true" className="score-divider">:</span>
      <strong aria-label={`${away.teamName}: ${finished ? awaySets : current?.awayPoints ?? 'sin datos'}`}>{finished ? awaySets : current?.awayPoints ?? '—'}</strong>
    </div>
    {finished ? <p className="live-sets-label">Resultado en sets</p> : <div className="live-sets" role="group" aria-label="Sets ganados">
      <span><b>{homeSets}</b> {homeSets === 1 ? 'set' : 'sets'}</span><span><b>{awaySets}</b> {awaySets === 1 ? 'set' : 'sets'}</span>
    </div>}
    <SetScoreHistory sets={sets} />
  </div>;
}

export function ServeIndicator({ live }: { live: Live }) {
  const current = live.sets.find(set => set.setNumber === live.currentSetNumber);
  if (live.status !== 'InProgress' || current?.status !== 'InProgress' || !live.servingSide || !live.servingPlayer) return null;
  const team = live.servingSide === 'Home' ? live.home : live.away;
  return <div className="serve-indicator" role="group" aria-label="Saque actual">
    <b><span aria-hidden="true">🏐</span> {team.teamName}</b>
    <span>Saca <strong>#{live.servingPlayer.jerseyNumber}</strong> {live.servingPlayer.displayName}</span>
  </div>;
}
