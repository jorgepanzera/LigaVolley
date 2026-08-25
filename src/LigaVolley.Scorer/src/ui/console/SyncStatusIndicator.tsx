import type { RuntimeState } from '../../domain/types';
export function SyncStatusIndicator({
  runtime,
  pending,
  onClick,
}: {
  runtime: RuntimeState;
  pending: number;
  onClick: () => void;
}) {
  const text =
    runtime === 'SYNCING'
      ? 'Sincronizando'
      : runtime === 'OFFLINE'
        ? `Offline · ${pending} pendientes`
        : runtime === 'BLOCKED'
          ? 'Sesión bloqueada'
          : runtime === 'CLOSED' && pending
            ? `Cerrado · ${pending} pendientes`
            : 'Sincronizado';
  return (
    <button className={`sync-indicator ${runtime.toLowerCase()}`} onClick={onClick}>
      <span aria-hidden>●</span>
      {text}
    </button>
  );
}
