import type { ServerSheetSnapshot, Side } from '../../domain/types';
export type Player = ServerSheetSnapshot['home']['players'][number];
export function team(snapshot: ServerSheetSnapshot | undefined, side: Side) {
  return side === 'HOME' ? snapshot?.home : snapshot?.away;
}
export function player(
  snapshot: ServerSheetSnapshot | undefined,
  side: Side,
  id: number | undefined,
) {
  return team(snapshot, side)?.players.find((x) => x.matchPlayerId === id);
}
export function shortName(name: string | undefined) {
  if (!name) return '—';
  const parts = name.trim().split(/\s+/);
  return (parts.at(-1) ?? name).toUpperCase();
}
