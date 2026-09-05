import { expect, test } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import type { OpenMatchContext, OpenMatchRequest } from '../src/api/scorerApi';
import type { ServerSheetSnapshot } from '../src/domain/types';

for (const liberoCount of [0, 1, 2]) {
  test(`opening declares ${liberoCount} liberos and PrepareSet preserves them across reentry`, async ({ page }) => {
    const openingTeam = (side: string, base: number) => ({
      teamEntryId: base, teamName: side, competitionRosterId: base, rosterStatus: 'Active', staff: [],
      players: Array.from({ length: 6 + liberoCount }, (_, i) => ({
        competitionRosterPlayerId: base + i, displayName: `${side} ${i < 6 ? 'Regular' : 'Libero'} ${i + 1}`,
        role: i < 6 ? 'Setter' : 'Libero',
      })),
    });
    const opening: OpenMatchContext = {
      match: { matchId: 1, status: 'Scheduled', homeTeamEntryId: 10, awayTeamEntryId: 20 },
      competition: { competitionId: 1, competitionName: 'Test', season: '2026', division: 'A', phase: 'Regular' },
      home: openingTeam('HOME', 10), away: openingTeam('AWAY', 20), warnings: [],
    };
    let opened: ServerSheetSnapshot | undefined;
    let sent: OpenMatchRequest | undefined;
    await page.route('**/api/scorer/matches/1/sheet', route => opened
      ? route.fulfill({ json: opened })
      : route.fulfill({ status: 404, json: { code: 'match_sheet_not_found' } }));
    await page.route('**/api/scorer/matches/1/open-context', route => route.fulfill({ json: opening }));
    await page.route('**/api/scorer/matches/1/sync', route => route.abort('timedout'));
    await page.route('**/api/scorer/matches/1/open', route => {
      sent = route.request().postDataJSON() as OpenMatchRequest;
      const project = (side: 'home' | 'away') => ({ teamName: opening[side].teamName,
        players: sent![side].players.map(p => ({ matchPlayerId: p.competitionRosterPlayerId + 100,
          jerseyNumber: p.jerseyNumber, isMatchCaptain: p.isMatchCaptain,
          displayName: opening[side].players.find(x => x.competitionRosterPlayerId === p.competitionRosterPlayerId)!.displayName,
        })), liberos: sent![side].liberoCompetitionRosterPlayerIds.map(id => ({ matchPlayerId: id + 100 })),
      });
      opened = {
        sheet: { matchSheetId: 1, sheetUuid: randomUUID(), status: 'OPEN', openedAt: new Date().toISOString() },
        match: opening.match, competition: opening.competition, home: project('home'), away: project('away'),
        session: { sessionUuid: randomUUID(), deviceId: sent.deviceId, status: 'ACTIVE', lastAcceptedSequence: 0, startedAt: new Date().toISOString() },
        currentState: { homeSets: 0, awaySets: 0, homePoints: 0, awayPoints: 0, homeRotationOffset: 0, awayRotationOffset: 0, homeTimeouts: 0, awayTimeouts: 0 },
      };
      return route.fulfill({ status: 201, json: { alreadyOpen: false, matchSheet: opened } });
    });
    await page.goto('/?matchId=1');
    await expect(page.getByRole('heading', { name: 'Abrir acta' })).toBeVisible();
    for (const side of ['home', 'away'] as const) {
      const rows = page.locator(`.open-team.${side} .open-player`);
      for (let index = 0; index < 6 + liberoCount; index++) {
        const row = rows.nth(index);
        await row.getByRole('checkbox').check();
        // Exercise deselection so an old declaration cannot linger or duplicate.
        if (index >= 6) { await row.getByRole('checkbox').uncheck(); await row.getByRole('checkbox').check(); }
        await row.getByRole('spinbutton').fill(String(index < 6 ? index + 10 : index === 6 ? 42 : 99));
        if (index === 0) await row.getByRole('radio').check();
      }
    }
    await page.getByRole('button', { name: /Abrir acta/ }).click();
    await expect(page.getByRole('button', { name: 'Preparar Set 1' })).toBeVisible();
    for (const side of ['home', 'away'] as const)
      expect(sent![side].liberoCompetitionRosterPlayerIds).toEqual(opening[side].players.filter(p => p.role === 'Libero').map(p => p.competitionRosterPlayerId));
    await page.getByRole('button', { name: 'Preparar Set 1' }).click();
    for (const [index, side] of (['home', 'away'] as const).entries()) {
      const team = page.locator('.prep-grid article').nth(index);
      const select = team.getByLabel('Líbero del set');
      await expect(select.locator('option')).toHaveCount(liberoCount + 1);
      await expect(select.locator('option')).toHaveText(['Ninguno', ...[42, 99].slice(0, liberoCount).map(n => `#${n}`)]);
      if (index === 1) await team.locator('.lineup-slots button').filter({ hasText: 'P1' }).click();
      for (let player = 1; player <= 6; player++) await team.getByRole('button', { name: new RegExp(`${side.toUpperCase()} Regular ${player}`) }).click();
      if (liberoCount) {
        await select.selectOption(String(opening[side].players[6].competitionRosterPlayerId + 100));
        await team.locator('.libero-config button').filter({ hasText: 'P1' }).click();
      }
      await team.getByRole('button', { name: /Guardar/ }).click();
    }
    await page.reload();
    for (const [index, side] of (['home', 'away'] as const).entries()) {
      const select = page.locator('.prep-grid article').nth(index).getByLabel('Líbero del set');
      await expect(select.locator('option')).toHaveCount(liberoCount + 1);
      await expect(select).toHaveValue(liberoCount ? String(opening[side].players[6].competitionRosterPlayerId + 100) : '');
    }
    // Server unavailable: the app shell is cached, candidates and selection come only from IndexedDB.
    await page.route('**/api/scorer/matches/1/sheet', route => route.abort('internetdisconnected'));
    await page.reload();
    for (const [index, side] of (['home', 'away'] as const).entries()) {
      const select = page.locator('.prep-grid article').nth(index).getByLabel('Líbero del set');
      await expect(select.locator('option')).toHaveCount(liberoCount + 1);
      await expect(select).toHaveValue(liberoCount ? String(opening[side].players[6].competitionRosterPlayerId + 100) : '');
    }
  });
}
