import type { Status } from '../api/types';

export const LIVE_FRESH_SECONDS = 30;
export const LIVE_STALE_SECONDS = 90;
export type LiveFreshness = 'FRESH' | 'DELAYED' | 'STALE' | 'UNKNOWN';

export function liveAgeSeconds(serverTime: string, lastUpdatedAt?: string | null, elapsedMs = 0): number | null {
  if (!lastUpdatedAt) return null;
  const age = (Date.parse(serverTime) - Date.parse(lastUpdatedAt) + elapsedMs) / 1000;
  return Number.isFinite(age) ? Math.max(0, age) : null;
}

export function classifyLiveFreshness(serverTime: string, lastUpdatedAt?: string | null, elapsedMs = 0): LiveFreshness {
  const age = liveAgeSeconds(serverTime, lastUpdatedAt, elapsedMs);
  if (age === null) return 'UNKNOWN';
  if (age <= LIVE_FRESH_SECONDS) return 'FRESH';
  return age <= LIVE_STALE_SECONDS ? 'DELAYED' : 'STALE';
}

export function formatLiveAge(age: number | null): string {
  if (age === null) return 'Hora de actualización no disponible';
  const seconds = Math.floor(age);
  const minutes = Math.floor(seconds / 60);
  const duration = minutes ? `${minutes} min${seconds % 60 ? ` ${seconds % 60} s` : ''}` : `${seconds} s`;
  return `${age <= LIVE_FRESH_SECONDS ? 'Actualizado' : 'Última actualización'} hace ${duration}`;
}

export function livePresentation(status: Status, freshness: LiveFreshness) {
  if (status === 'Finished') return { label: 'FINAL', tone: 'final' };
  if (status === 'Suspended') return { label: 'PARTIDO SUSPENDIDO', tone: 'suspended' };
  if (freshness === 'FRESH') return { label: 'EN VIVO', tone: 'live' };
  if (freshness === 'DELAYED') return { label: 'EN VIVO · actualización demorada', tone: 'delayed' };
  return { label: 'PARTIDO EN CURSO', tone: freshness === 'STALE' ? 'stale' : 'unknown' };
}

export const normalDelay = (status: Status) => status === 'Suspended' ? 15000 : status === 'InProgress' ? 5000 : null;
export const retryDelay = (failures: number) => Math.min(30000, 5000 * 2 ** Math.max(0, failures - 1));
export const noLiveMessage = (status: Status) => status === 'Cancelled' ? 'Partido cancelado' :
  status === 'Pending' || status === 'Scheduled' ? 'El partido todavía no comenzó' : 'La información en vivo no está disponible para este partido';
