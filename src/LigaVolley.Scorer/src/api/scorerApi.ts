import type { LocalEvent, ServerSheetSnapshot } from '../domain/types';
export class ApiProblem extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
  ) {
    super(message);
  }
}
async function call<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers },
      signal: AbortSignal.timeout(12000),
    });
  } catch (e) {
    throw new ApiProblem(0, 'sync_temporarily_unavailable', String(e));
  }
  if (!response.ok) {
    const p = await response.json().catch(() => ({}));
    throw new ApiProblem(response.status, p.code ?? 'api_error', p.detail ?? response.statusText);
  }
  return response.json();
}
export interface SyncResponse {
  sheetUuid: string;
  sessionUuid: string;
  lastAcceptedSequence: number;
  results: Array<{ eventUuid: string; sequence: number; status: 'APPLIED' | 'ALREADY_ACCEPTED' }>;
  snapshot: ServerSheetSnapshot;
}
export const scorerApi = {
  sheet: (matchId: number) => call<ServerSheetSnapshot>(`/api/scorer/matches/${matchId}/sheet`),
  sync: (
    matchId: number,
    body: { sheetUuid: string; sessionUuid: string; deviceId: string; events: LocalEvent[] },
  ) =>
    call<SyncResponse>(`/api/scorer/matches/${matchId}/sync`, {
      method: 'POST',
      body: JSON.stringify({
        ...body,
        events: body.events.map(({ eventUuid, sequence, type, occurredAt, payload }) => ({
          eventUuid,
          sequence,
          type,
          occurredAt,
          payload,
        })),
      }),
    }),
  takeOver: (
    matchId: number,
    body: {
      sheetUuid: string;
      expectedSessionUuid: string;
      deviceId: string;
      clientRequestId: string;
    },
  ) =>
    call<{ sessionUuid: string; snapshot: ServerSheetSnapshot }>(
      `/api/scorer/matches/${matchId}/take-over`,
      { method: 'POST', body: JSON.stringify(body) },
    ),
};
