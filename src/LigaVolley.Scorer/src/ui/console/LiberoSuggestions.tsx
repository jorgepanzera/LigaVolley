import { useState } from 'react';
import { liberoSuggestions, regularPlayers, physicalPosition } from '../../domain/matchEngine';
import type { MatchCommand, MatchState, ServerSheetSnapshot } from '../../domain/types';
import { player } from './model';

export function LiberoSuggestions({ state, snapshot, disabled, onCommand }: {
  state: MatchState; snapshot: ServerSheetSnapshot; disabled: boolean;
  onCommand: (command: MatchCommand) => Promise<void>;
}) {
  const [dismissed, setDismissed] = useState<string[]>([]);
  const [error, setError] = useState('');
  const set = state.sets.find(x => x.setNumber === state.currentSetNumber);
  if (!set) return null;
  const revision = JSON.stringify([set.setNumber, set.points, set.substitutions, set.liberoReplacements]);
  return <div className="libero-suggestions" aria-label="Sugerencias de líbero">
    {liberoSuggestions(state).map(suggestion => {
      const key = revision + JSON.stringify(suggestion.command);
      if (dismissed.includes(key)) return null;
      const libero = player(snapshot, suggestion.side, Number(suggestion.command.payload.liberoMatchPlayerId));
      const regular = player(snapshot, suggestion.side, regularPlayers(set, suggestion.side)[suggestion.logical]);
      const physical = physicalPosition(suggestion.logical, suggestion.side === 'HOME' ? set.homeRotationOffset : set.awayRotationOffset);
      const text = suggestion.command.type === 'LIBERO_EXIT'
        ? `Sale líbero #${libero?.jerseyNumber} / vuelve #${regular?.jerseyNumber}`
        : `Ingresar líbero #${libero?.jerseyNumber} por #${regular?.jerseyNumber}`;
      return <div key={key}>
        <small>Sugerencia {suggestion.side} · P{physical}</small>
        <button className="libero-suggestion-apply" disabled={disabled} onClick={() => { setError(''); void onCommand(suggestion.command).catch(e => setError(e instanceof Error ? e.message : 'No se pudo registrar')); }}>{text}</button>
        <button className="libero-suggestion-dismiss" disabled={disabled} aria-label={`Descartar sugerencia ${suggestion.side} P${physical}`} onClick={() => setDismissed([...dismissed.filter(x => x.startsWith(revision)), key])}>Descartar</button>
      </div>;
    })}
    {error && <p role="alert">{error}</p>}
  </div>;
}
