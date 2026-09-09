import{Link}from'react-router-dom';import type{FixtureMatch,PlayoffSeries,StandingsTable}from'../api/types';import{TeamLogo}from'./TeamLogo';export{TeamLogo}from'./TeamLogo';
export const label=(s:string)=>({Pending:'Pendiente',Scheduled:'Programado',InProgress:'En juego',Suspended:'Suspendido',Finished:'Finalizado',Cancelled:'Cancelado'}[s]??s);
const fixtureDate=(date?:string)=>date?new Intl.DateTimeFormat('es-UY',{day:'2-digit',month:'2-digit',hour:'2-digit',minute:'2-digit'}).format(new Date(date)):'Fecha a confirmar';
export function MatchCard({match}:{match:FixtureMatch}){
  const result=match.score?`${match.score.homeSets}–${match.score.awaySets}`:'—';
  return <Link className={`match match-${match.status.toLowerCase()}`} to={`/matches/${match.matchId}`} aria-label={`${match.homeTeam.teamName} contra ${match.awayTeam.teamName}, ${label(match.status)}`}>
    <div className="match-teams">
      <span className="match-team match-home"><TeamLogo team={match.homeTeam}/><b>{match.homeTeam.teamName}</b></span>
      <span className="match-versus">vs</span>
      <span className="match-team match-away"><b>{match.awayTeam.teamName}</b><TeamLogo team={match.awayTeam}/></span>
    </div>
    <div className="match-summary">
      <strong className="match-result" aria-label={`Resultado: ${result}`}>{result}</strong>
      <span className="match-status">{label(match.status)}</span>
      <small className="match-meta">{fixtureDate(match.matchDate)} <span aria-hidden="true">·</span> {match.venue?.name??'Sede a confirmar'}</small>
    </div>
  </Link>
}
export function StandingsTableView({table}:{table:StandingsTable}){return <section className="standings-table-section"><h2>{table.phaseGroupName??table.phaseName}</h2>{!table.isFinal&&<p className="note">Posiciones provisorias</p>}<div className="table-scroll"><table className="standings-table"><thead><tr><th>Pos</th><th>Equipo</th><th>PJ</th><th>PG</th><th>PP</th><th className="standing-secondary">Sets</th><th className="standing-secondary">Ratio sets</th><th className="standing-secondary">Puntos</th><th className="standing-secondary">Ratio pts</th><th>PT</th></tr></thead><tbody>{table.rows.map(r=><tr key={r.teamEntryId}><td>{r.position}{r.isTied?'=':''}</td><th>{r.teamName}</th><td>{r.played}</td><td>{r.won}</td><td>{r.lost}</td><td className="standing-secondary">{r.setsWon}/{r.setsLost}</td><td className="standing-secondary">{r.setRatio?.toFixed(3)??'—'}</td><td className="standing-secondary">{r.pointsWon}/{r.pointsLost}</td><td className="standing-secondary">{r.pointRatio?.toFixed(3)??'—'}</td><td><b>{r.tablePoints}</b></td></tr>)}</tbody></table></div></section>}
export function Bracket({series}:{series:PlayoffSeries[]}){return <div className="bracket">{series.map(s=><article className="series" key={s.seriesId}><header>{s.name}</header><div>{s.team1.team?.teamName??s.team1.source?.displayName??'A definir'} <b>{s.team1SeriesWins}</b></div><div>{s.team2.team?.teamName??s.team2.source?.displayName??'A definir'} <b>{s.team2SeriesWins}</b></div><small>Al mejor de {s.winsRequired} victoria{s.winsRequired===1?'':'s'}{s.team1InitialWins+s.team2InitialWins>0?' · incluye ventaja inicial':''}</small>{s.matches.map(m=><MatchCard key={m.matchId} match={m}/>)}</article>)}</div>}
