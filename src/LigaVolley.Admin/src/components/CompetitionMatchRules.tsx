import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../api/adminApiClient';
import type { Competition } from '../types/admin';
import { ProblemDetailsAlert } from './feedback';

export interface CompetitionMatchRules {
  maxSubstitutionsPerSetOverride: number | null;
  maxTimeoutsPerSetOverride: number | null;
  defaultMaxSubstitutionsPerSet: number;
  defaultMaxTimeoutsPerSet: number;
  effectiveMaxSubstitutionsPerSet: number;
  effectiveMaxTimeoutsPerSet: number;
}
export function CompetitionMatchRulesEditor({ competition }: { competition: Competition }) {
  const rules = competition.matchRules;
  const [substitutions, setSubstitutions] = useState('');
  const [timeouts, setTimeouts] = useState('');
  const queryClient = useQueryClient();
  useEffect(() => {
    setSubstitutions(rules?.maxSubstitutionsPerSetOverride?.toString() ?? '');
    setTimeouts(rules?.maxTimeoutsPerSetOverride?.toString() ?? '');
  }, [rules]);
  const save = useMutation({
    mutationFn: () => adminApi.put(`/competitions/${competition.competitionId}/match-rules`, {
      maxSubstitutionsPerSetOverride: substitutions === '' ? null : Number(substitutions),
      maxTimeoutsPerSetOverride: timeouts === '' ? null : Number(timeouts),
    }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['competition', String(competition.competitionId)] }),
  });
  if (!rules) return null;
  const readOnly = ['Finished', 'Cancelled'].includes(competition.status);
  return <form className="card form-grid" onSubmit={event => { event.preventDefault(); save.mutate(); }}>
    <h3>Reglas del partido</h3>
    <label>Sustituciones por set
      <input aria-label="Override de sustituciones" type="number" min={1} max={99} disabled={readOnly} value={substitutions} placeholder={`Heredado: ${rules.defaultMaxSubstitutionsPerSet}`} onChange={e => setSubstitutions(e.target.value)} />
      <small>{substitutions === '' ? `Heredado del formato: ${rules.defaultMaxSubstitutionsPerSet}` : `Override: ${substitutions}`}</small>
    </label>
    <label>Timeouts por set
      <input aria-label="Override de timeouts" type="number" min={1} max={99} disabled={readOnly} value={timeouts} placeholder={`Heredado: ${rules.defaultMaxTimeoutsPerSet}`} onChange={e => setTimeouts(e.target.value)} />
      <small>{timeouts === '' ? `Heredado del formato: ${rules.defaultMaxTimeoutsPerSet}` : `Override: ${timeouts}`}</small>
    </label>
    <p>Dejá un campo vacío para heredar el formato. Las actas ya abiertas conservan sus reglas.</p>
    {save.error && <ProblemDetailsAlert error={save.error} />}
    {!readOnly && <button disabled={save.isPending}>Guardar reglas</button>}
  </form>;
}
