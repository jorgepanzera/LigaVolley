import { test, expect } from '@playwright/test';
const sheet = {
  sheet: {
    matchSheetId: 1,
    sheetUuid: '11111111-1111-4111-8111-111111111111',
    status: 'OPEN',
    openedAt: new Date().toISOString(),
  },
  match: { matchId: 1, status: 'SCHEDULED', homeTeamEntryId: 1, awayTeamEntryId: 2 },
  home: {
    teamName: 'HOME',
    players: Array.from({ length: 6 }, (_, i) => ({ matchPlayerId: i + 1, displayName: `H${i}` })),
    liberos: [],
  },
  away: {
    teamName: 'AWAY',
    players: Array.from({ length: 6 }, (_, i) => ({ matchPlayerId: i + 11, displayName: `A${i}` })),
    liberos: [],
  },
  session: {
    sessionUuid: '22222222-2222-4222-8222-222222222222',
    deviceId: 'device-a',
    status: 'ACTIVE',
    lastAcceptedSequence: 0,
    startedAt: new Date().toISOString(),
  },
  currentState: {
    homeSets: 0,
    awaySets: 0,
    homePoints: 0,
    awayPoints: 0,
    homeRotationOffset: 0,
    awayRotationOffset: 0,
    homeTimeouts: 0,
    awayTimeouts: 0,
  },
};
test('bootstrap, offline mutation and offline reentry use IndexedDB', async ({ page, context }) => {
  await page.route('**/api/scorer/matches/1/sheet', (r) => r.fulfill({ json: sheet }));
  await page.route('**/api/scorer/matches/1/sync', (r) => r.abort('timedout'));
  await page.goto('/?matchId=1');
  await expect(page.getByText('Sincronizado')).toBeVisible();
  await page.getByRole('button', { name: 'Preparar + iniciar set' }).click();
  await expect(page.getByText('SET 1')).toBeVisible();
  await context.setOffline(true);
  await page.getByRole('button', { name: '+ Punto HOME' }).click();
  await expect(page.locator('.score article').first().locator('strong')).toHaveText('1');
  await page.reload();
  await expect(page.getByText('SET 1')).toBeVisible();
  await expect(page.locator('.score article').first().locator('strong')).toHaveText('1');
  await expect(page.getByText(/Sin conexión/)).toBeVisible();
});
test('takeover adopts the new server session', async ({ page }) => {
  await page.route('**/api/scorer/matches/1/sheet', (r) => r.fulfill({ json: sheet }));
  await page.route('**/api/scorer/matches/1/take-over', (r) =>
    r.fulfill({
      json: {
        sessionUuid: '33333333-3333-4333-8333-333333333333',
        snapshot: {
          ...sheet,
          session: {
            ...sheet.session,
            sessionUuid: '33333333-3333-4333-8333-333333333333',
            deviceId: 'device-b',
          },
        },
      },
    }),
  );
  await page.goto('/?matchId=1');
  await page.getByRole('button', { name: 'TakeOver' }).click();
  await expect(page.getByText('33333333')).toBeVisible();
});
