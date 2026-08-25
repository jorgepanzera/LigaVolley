import { expect, test, type Page } from '@playwright/test';
const sheet = {
  sheet: {
    matchSheetId: 1,
    sheetUuid: '11111111-1111-4111-8111-111111111111',
    status: 'OPEN',
    openedAt: new Date().toISOString(),
  },
  match: { matchId: 1, status: 'SCHEDULED', homeTeamEntryId: 1, awayTeamEntryId: 2 },
  competition: {
    competitionId: 1,
    competitionName: 'Liga Test',
    season: '2026',
    division: 'A',
    phase: 'Regular',
  },
  home: {
    teamName: 'HOME',
    players: Array.from({ length: 7 }, (_, i) => ({
      matchPlayerId: i + 1,
      jerseyNumber: i + 1,
      displayName: `H${i}`,
    })),
    liberos: [],
  },
  away: {
    teamName: 'AWAY',
    players: Array.from({ length: 7 }, (_, i) => ({
      matchPlayerId: i + 11,
      jerseyNumber: i + 11,
      displayName: `A${i}`,
    })),
    liberos: [],
  },
  trackSubstitutions: true,
  trackLiberoReplacements: true,
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
async function bootstrap(page: Page) {
  await page.route('**/api/scorer/matches/1/sheet', (r) => r.fulfill({ json: sheet }));
  await page.route('**/api/scorer/matches/1/sync', (r) => r.abort('timedout'));
  await page.goto('/?matchId=1');
  await expect(page.getByRole('button', { name: 'Preparar Set 1' })).toBeVisible();
}
async function prepareAndStart(page: Page) {
  await page.getByRole('button', { name: 'Preparar Set 1' }).click();
  const home = page.locator('.prep-grid article').nth(0),
    away = page.locator('.prep-grid article').nth(1);
  for (let i = 0; i < 6; i++) await home.getByRole('button', { name: new RegExp(`H${i}`) }).click();
  await home.getByRole('button', { name: 'Guardar HOME' }).click();
  await away.locator('.lineup-slots button').first().click();
  for (let i = 0; i < 6; i++) await away.getByRole('button', { name: new RegExp(`A${i}`) }).click();
  await away.getByRole('button', { name: 'Guardar AWAY' }).click();
  await page.getByRole('button', { name: 'HOME SACA' }).click();
  await page.getByRole('button', { name: 'Iniciar Set 1' }).click();
  await expect(page.getByRole('button', { name: /PUNTO HOME/ })).toBeEnabled();
}
test('PrepareSet, puntos, timeout, corrección y reentrada offline conservan IndexedDB', async ({
  page,
  context,
}) => {
  await bootstrap(page);
  await prepareAndStart(page);
  await context.setOffline(true);
  await page.getByRole('button', { name: /PUNTO AWAY/ }).click();
  await expect(page.locator('.team-score.away > strong')).toHaveText('1');
  await page.getByRole('button', { name: /Corregir último punto/ }).click();
  await page.getByRole('button', { name: 'Corregir', exact: true }).click();
  await expect(page.locator('.team-score.away > strong')).toHaveText('0');
  await page.getByRole('button', { name: /Timeout HOME/ }).click();
  await page.getByRole('button', { name: 'Registrar timeout' }).click();
  await page.reload();
  await expect(page.locator('.team-score.away > strong')).toHaveText('0');
  await expect(page.getByRole('button', { name: /Timeout HOME 1\/2/ })).toBeVisible();
  await expect(page.getByText(/Offline/).first()).toBeVisible();
});
test('la consola actualiza localmente y protege el doble toque breve', async ({ page }) => {
  await bootstrap(page);
  await prepareAndStart(page);
  const point = page.getByRole('button', { name: /PUNTO HOME/ });
  await point.dblclick({ delay: 20 });
  await expect(page.locator('.team-score.home > strong')).toHaveText('1');
  await expect(point).toBeDisabled();
});
