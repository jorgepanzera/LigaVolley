import { useSearchParams } from 'react-router-dom';
import type { Fixture, FixtureMatch, FixturePhase } from '../api/types';
import { MatchCard } from './components';

type MatchFilter = 'all' | 'upcoming' | 'results';
const filters: { value: MatchFilter; label: string }[] = [{ value: 'all', label: 'Todos' }, { value: 'upcoming', label: 'Próximos' }, { value: 'results', label: 'Resultados' }];
const matchesFilter = (match: FixtureMatch, filter: MatchFilter) => filter === 'all' || (filter === 'upcoming' ? match.status === 'Scheduled' : match.status === 'Finished');

function emptyMessage(filter: MatchFilter, phase?: FixturePhase, groupName?: string) {
  const scope = groupName ?? phase?.name;
  if (filter === 'upcoming') return scope ? `No hay próximos partidos programados en ${scope}.` : 'No hay próximos partidos programados.';
  if (filter === 'results') return scope ? `Todavía no hay resultados en ${scope}.` : 'Todavía no hay resultados.';
  return scope ? `No hay partidos para mostrar en ${scope}.` : 'No hay partidos para mostrar.';
}

function RoundList({ rounds, filter }: { rounds: { roundNumber: number; matches: FixtureMatch[] }[]; filter: MatchFilter }) {
  return <>{rounds.map(round => { const matches = round.matches.filter(match => matchesFilter(match, filter)); return matches.length ? <div className="fixture-round" key={round.roundNumber}><h3>Ronda {round.roundNumber}</h3>{matches.map(match => <MatchCard key={match.matchId} match={match} />)}</div> : null; })}</>;
}

function SeriesList({ phase, filter }: { phase: FixturePhase; filter: MatchFilter }) {
  return <div className="fixture-series-list">{phase.series.map(series => { const matches = series.matches.filter(match => matchesFilter(match, filter)); return matches.length ? <article className="fixture-series" key={series.seriesId}><h3>{series.name}</h3><p>{series.team1Wins}–{series.team2Wins} · al mejor de {series.winsRequired} victoria{series.winsRequired === 1 ? '' : 's'}{series.team1InitialWins + series.team2InitialWins > 0 ? ' · incluye ventaja inicial' : ''}</p>{matches.map(match => <MatchCard key={match.matchId} match={match} />)}</article> : null; })}</div>;
}

function PhaseFixture({ phase, groupId, filter }: { phase: FixturePhase; groupId?: number; filter: MatchFilter }) {
  const groups = groupId ? phase.groups.filter(group => group.phaseGroupId === groupId) : phase.groups;
  const groupsVisible = groups.some(group => group.rounds.some(round => round.matches.some(match => matchesFilter(match, filter))));
  const roundsVisible = !groupId && phase.rounds.some(round => round.matches.some(match => matchesFilter(match, filter)));
  const seriesVisible = !groupId && phase.series.some(series => series.matches.some(match => matchesFilter(match, filter)));
  if (!groupsVisible && !roundsVisible && !seriesVisible) return null;
  return <section className="fixture-phase"><h2>{phase.name}</h2>{roundsVisible && <RoundList rounds={phase.rounds} filter={filter} />}{groups.map(group => { const visible = group.rounds.some(round => round.matches.some(match => matchesFilter(match, filter))); return visible ? <section className="fixture-group" key={group.phaseGroupId}><h3>{group.name}</h3><RoundList rounds={group.rounds} filter={filter} /></section> : null; })}{seriesVisible && <section className="fixture-playoffs"><h3>Playoffs</h3><SeriesList phase={phase} filter={filter} /></section>}</section>;
}

export function FixtureResults({ fixture }: { fixture: Fixture }) {
  const [search, setSearch] = useSearchParams();
  const filter = filters.some(item => item.value === search.get('matches')) ? search.get('matches') as MatchFilter : 'all';
  const phaseId = Number(search.get('phase')) || undefined;
  const phase = fixture.phases.find(item => item.phaseId === phaseId);
  const selectedPhase = phase ?? (fixture.phases.length === 1 ? fixture.phases[0] : undefined);
  const groupId = Number(search.get('group')) || undefined;
  const selectedGroup = selectedPhase?.groups.find(group => group.phaseGroupId === groupId);
  const visiblePhases = selectedPhase ? [selectedPhase] : fixture.phases;
  const hasVisibleMatches = visiblePhases.some(item => {
    const groups = selectedGroup ? item.groups.filter(group => group.phaseGroupId === selectedGroup.phaseGroupId) : item.groups;
    return item.rounds.some(round => round.matches.some(match => matchesFilter(match, filter))) || groups.some(group => group.rounds.some(round => round.matches.some(match => matchesFilter(match, filter)))) || item.series.some(series => series.matches.some(match => matchesFilter(match, filter)));
  });
  const update = (changes: Record<string, string | undefined>) => { const next = new URLSearchParams(search); Object.entries(changes).forEach(([key, value]) => value ? next.set(key, value) : next.delete(key)); setSearch(next); };
  return <><div className="fixture-filters" aria-label="Filtros de fixture"><div className="fixture-status-filters" role="group" aria-label="Estado de partidos">{filters.map(item => <button className={filter === item.value ? 'active' : ''} key={item.value} aria-pressed={filter === item.value} onClick={() => update({ matches: item.value === 'all' ? undefined : item.value })}>{item.label}</button>)}</div>{fixture.phases.length > 1 && <label>Fase<select aria-label="Fase" value={selectedPhase?.phaseId ?? ''} onChange={event => update({ phase: event.target.value || undefined, group: undefined })}><option value="">Todas las fases</option>{fixture.phases.map(item => <option key={item.phaseId} value={item.phaseId}>{item.name}</option>)}</select></label>}{selectedPhase?.groups.length ? <label>Grupo<select aria-label="Grupo" value={selectedGroup?.phaseGroupId ?? ''} onChange={event => update({ group: event.target.value || undefined })}><option value="">Todos los grupos</option>{selectedPhase.groups.map(group => <option key={group.phaseGroupId} value={group.phaseGroupId}>{group.name}</option>)}</select></label> : null}</div>{hasVisibleMatches ? visiblePhases.map(item => <PhaseFixture key={item.phaseId} phase={item} groupId={selectedGroup?.phaseGroupId} filter={filter} />) : <p className="fixture-empty">{emptyMessage(filter, selectedPhase, selectedGroup?.name)}</p>}</>;
}
