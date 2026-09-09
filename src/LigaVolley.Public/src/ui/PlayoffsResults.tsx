import type { Competition } from '../api/types';
import { Bracket } from './components';

export function PlayoffsResults({ competition }: { competition: Competition }) {
  const phases = competition.phases.filter(phase => phase.playoffSeries.length);
  return phases.length ? <>{phases.map(phase => <section className="playoff-phase" key={phase.phaseId}><h2>{phase.name}</h2><Bracket series={phase.playoffSeries} /></section>)}</> : <p className="playoffs-empty">Los playoffs todavía no están disponibles.</p>;
}
