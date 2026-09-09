import { useSearchParams } from 'react-router-dom';
import type { Standings, StandingsTable } from '../api/types';
import { StandingsTableView } from './components';

export function StandingsResults({ standings }: { standings: Standings }) {
  const [search, setSearch] = useSearchParams();
  const phaseId = Number(search.get('phase')) || undefined;
  const phaseTables = phaseId ? standings.tables.filter(table => table.phaseId === phaseId) : [];
  const selectedPhaseId = phaseTables.length ? phaseId! : standings.tables[0]?.phaseId;
  const tablesForPhase = standings.tables.filter(table => table.phaseId === selectedPhaseId);
  const groupId = Number(search.get('group')) || undefined;
  const selectedTable = (groupId ? tablesForPhase.find(table => table.phaseGroupId === groupId) : undefined) ?? tablesForPhase[0];
  const phases = standings.tables.reduce<StandingsTable[]>((items, table) => items.some(item => item.phaseId === table.phaseId) ? items : [...items, table], []);
  const hasGroups = tablesForPhase.some(table => table.phaseGroupId !== undefined);
  const update = (changes: Record<string, string | undefined>) => { const next = new URLSearchParams(search); Object.entries(changes).forEach(([key, value]) => value ? next.set(key, value) : next.delete(key)); setSearch(next); };
  if (!standings.tables.length) return <p className="standings-empty">Todavía no hay posiciones disponibles para esta fase.</p>;
  return <><div className="standings-filters" aria-label="Filtros de posiciones">{phases.length > 1 && <label>Fase<select aria-label="Fase" value={selectedPhaseId} onChange={event => update({ phase: event.target.value, group: undefined })}>{phases.map(table => <option key={table.phaseId} value={table.phaseId}>{table.phaseName}</option>)}</select></label>}{hasGroups && <label>Grupo<select aria-label="Grupo" value={selectedTable?.phaseGroupId ?? ''} onChange={event => update({ group: event.target.value || undefined })}>{tablesForPhase.map(table => <option key={table.phaseGroupId} value={table.phaseGroupId}>{table.phaseGroupName}</option>)}</select></label>}</div>{selectedTable ? selectedTable.rows.length ? <StandingsTableView table={selectedTable} /> : <p className="standings-empty">Todavía no hay posiciones disponibles para esta fase.</p> : <p className="standings-empty">Todavía no hay posiciones disponibles para esta fase.</p>}</>;
}
