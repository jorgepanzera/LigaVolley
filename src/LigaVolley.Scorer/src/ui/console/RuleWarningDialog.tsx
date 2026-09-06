import type { RuleEvaluation, RuleIssue } from '../../domain/rulesAssistant';
import { ConfirmDialog } from './ConsoleDialogs';

const messages: Record<string, string> = {
  substitution_reentry_irregular: 'El reingreso no sigue la pareja de sustitución esperada.',
  substitution_player_already_bound: 'El jugador ya está vinculado a otra plaza del set.',
  substitution_original_player_mismatch: 'Se espera el regreso del titular original de esta plaza.',
  libero_wrong_regular_replacement: 'El líbero está reemplazando a otro regular.',
  libero_irregular_second_libero_replacement: 'La secuencia del segundo líbero es irregular.',
  libero_replacement_without_completed_rally:
    'No hay un rally registrado entre los reemplazos de líbero.',
  libero_in_front_row: 'El líbero ocupará una posición delantera.',
  libero_service_not_allowed:
    'El jugador indicado es líbero y las reglas del partido no permiten su saque.',
  libero_used_as_regular_substitute: 'Un líbero declarado participará como sustituto regular.',
  unexpected_server:
    'El servidor observado difiere del esperado. Se conservará el orden de rotación.',
};
function message(issue: RuleIssue) {
  const c = issue.context;
  if (issue.code === 'substitution_limit_exceeded')
    return `La solicitud contiene ${c.requested} sustituciones y llevaría al equipo de ${c.used}/${c.maximum} a ${c.projected}/${c.maximum}.`;
  if (issue.code === 'timeout_limit_exceeded')
    return `El equipo ya utilizó ${c.used}/${c.maximum} timeouts.`;
  return messages[issue.code] ?? issue.code;
}
export function RuleWarningDialog({
  evaluation,
  onConfirm,
  onCancel,
}: {
  evaluation: RuleEvaluation;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const specificService = evaluation.warnings.some((x) => x.code === 'libero_service_not_allowed');
  return (
    <ConfirmDialog
      title="Advertencia reglamentaria"
      confirmLabel="Registrar igualmente"
      onConfirm={onConfirm}
      onClose={onCancel}
    >
      {evaluation.warnings
        .filter((x) => !specificService || x.code !== 'unexpected_server')
        .map((x) => (
          <p key={x.code}>{message(x)}</p>
        ))}
      <p>La decisión deportiva corresponde al juez.</p>
    </ConfirmDialog>
  );
}
