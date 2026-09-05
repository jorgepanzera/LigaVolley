import type { LocalEvent, ServerSheetSnapshot } from '../domain/types';

const syncEventTypes = {
  PREPARE_SET: 'PrepareSet',
  SET_LINEUP: 'SetLineup',
  START_SET: 'StartSet',
  POINT: 'Point',
  CORRECT_LAST_POINT: 'CorrectLastPoint',
  SUBSTITUTION: 'Substitution',
  LIBERO_ENTER: 'LiberoEnter',
  LIBERO_EXIT: 'LiberoExit',
  TIMEOUT: 'Timeout',
  MATCH_CLOSE: 'MatchClose',
} as const satisfies Record<LocalEvent['type'], string>;

function serializeEvents(events: LocalEvent[]) {
  let preparedSetNumber = 0;
  return events.map(({ eventUuid, sequence, type, occurredAt, payload }) => {
    if (type === 'PREPARE_SET') preparedSetNumber += 1;
    const requiresSetNumber = !['PREPARE_SET', 'MATCH_CLOSE'].includes(type);
    return {
      eventUuid,
      sequence,
      type: syncEventTypes[type],
      occurredAt,
      payload:
        requiresSetNumber && payload.setNumber == null
          ? { ...payload, setNumber: preparedSetNumber }
          : payload,
    };
  });
}
export class ApiProblem extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
    public eventUuid?: string,
    public localSequence?: number,
  ) {
    super(message);
  }
}
export interface OpenMatchContext {
  match: { matchId: number; status: string; homeTeamEntryId: number; awayTeamEntryId: number };
  competition: {
    competitionId: number;
    competitionName: string;
    season: string;
    division: string;
    phase: string;
  };
  home: OpenTeamContext;
  away: OpenTeamContext;
  matchOfficials: Array<{ role: string; displayName: string }>;
  warnings: string[];
  existingMatchSheet?: {
    matchSheetId: number;
    sheetUuid: string;
    status: string;
    openedAt: string;
  };
}
export interface OpenTeamContext {
  teamEntryId: number;
  teamName: string;
  competitionRosterId: number;
  rosterStatus: string;
  players: Array<{ competitionRosterPlayerId: number; displayName: string; role: string }>;
  staff: Array<{ competitionRosterStaffId: number; displayName: string }>;
}
export interface OpenMatchRequest {
  clientRequestId: string;
  deviceId: string;
  home: OpenTeamSelection;
  away: OpenTeamSelection;
  trackSubstitutions?: boolean;
  trackLiberoReplacements?: boolean;
}
export interface OpenTeamSelection {
  players: Array<{
    competitionRosterPlayerId: number;
    jerseyNumber?: number;
    isMatchCaptain: boolean;
  }>;
  liberoCompetitionRosterPlayerIds: number[];
  competitionRosterStaffIds: number[];
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
    throw new ApiProblem(
      response.status,
      p.code ?? 'api_error',
      p.detail ?? response.statusText,
      p.eventUuid,
      typeof p.localSequence === 'number' ? p.localSequence : undefined,
    );
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
  openContext: (matchId: number) =>
    call<OpenMatchContext>(`/api/scorer/matches/${matchId}/open-context`),
  open: (matchId: number, body: OpenMatchRequest) =>
    call<{ alreadyOpen: boolean; matchSheet: ServerSheetSnapshot }>(
      `/api/scorer/matches/${matchId}/open`,
      {
        method: 'POST',
        body: JSON.stringify(body),
      },
    ),
  sync: (
    matchId: number,
    body: { sheetUuid: string; sessionUuid: string; deviceId: string; events: LocalEvent[] },
  ) =>
    call<SyncResponse>(`/api/scorer/matches/${matchId}/sync`, {
      method: 'POST',
      body: JSON.stringify({
        ...body,
        events: serializeEvents(body.events),
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
