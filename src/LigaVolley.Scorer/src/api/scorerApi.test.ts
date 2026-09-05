import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiProblem, scorerApi } from './scorerApi';
import type { LocalEvent } from '../domain/types';

describe('scorerApi sync contract', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('serializes local upper snake case event names as API enum names', async () => {
    const fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    });
    vi.stubGlobal('fetch', fetch);
    const event = {
      eventUuid: crypto.randomUUID(),
      matchId: 160,
      sheetUuid: crypto.randomUUID(),
      sessionUuid: crypto.randomUUID(),
      sequence: 1,
      type: 'START_SET',
      occurredAt: new Date().toISOString(),
      payload: { setNumber: 1, initialServingSide: 'HOME' },
      syncStatus: 'PENDING',
      createdAt: new Date().toISOString(),
    } satisfies LocalEvent;

    await scorerApi.sync(160, {
      sheetUuid: event.sheetUuid,
      sessionUuid: event.sessionUuid,
      deviceId: 'test-device',
      events: [event],
    });

    const request = fetch.mock.calls[0][1] as RequestInit;
    const body = JSON.parse(request.body as string);
    expect(body.events[0].type).toBe('StartSet');
  });

  it('recovers legacy pending lineup events without a set number', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => ({}) });
    vi.stubGlobal('fetch', fetch);
    const common = {
      matchId: 160,
      sheetUuid: crypto.randomUUID(),
      sessionUuid: crypto.randomUUID(),
      occurredAt: new Date().toISOString(),
      syncStatus: 'PENDING' as const,
      createdAt: new Date().toISOString(),
    };
    const events: LocalEvent[] = [
      { ...common, eventUuid: crypto.randomUUID(), sequence: 1, type: 'PREPARE_SET', payload: {} },
      {
        ...common,
        eventUuid: crypto.randomUUID(),
        sequence: 2,
        type: 'SET_LINEUP',
        payload: { side: 'HOME' },
      },
    ];

    await scorerApi.sync(160, {
      sheetUuid: common.sheetUuid,
      sessionUuid: common.sessionUuid,
      deviceId: 'test-device',
      events,
    });

    const request = fetch.mock.calls[0][1] as RequestInit;
    const body = JSON.parse(request.body as string);
    expect(body.events[1].payload.setNumber).toBe(1);
  });

  it('keeps the rejected event identity from sync ProblemDetails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 409,
        statusText: 'Conflict',
        json: async () => ({
          code: 'substitution_player_is_libero',
          detail: 'Invalid substitution',
          eventUuid: 'event-42',
          localSequence: 42,
        }),
      }),
    );

    let problem: ApiProblem | undefined;
    try {
      await scorerApi.sheet(160);
    } catch (error) {
      problem = error as ApiProblem;
    }

    expect(problem).toBeInstanceOf(ApiProblem);
    expect(problem?.eventUuid).toBe('event-42');
    expect(problem?.localSequence).toBe(42);
  });
});
