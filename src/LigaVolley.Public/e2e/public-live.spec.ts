import { expect, test } from '@playwright/test';
import { liveFixture, matchFixture } from '../src/live/liveFixtures';

for (const width of [320, 390, 768, 1024, 1440]) {
  test(`score hierarchy and court interaction at ${width}px`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width, height: width >= 1024 ? 900 : 844 });
    await page.route('**/api/public/matches/1', route => route.fulfill({ json: matchFixture() }));
    await page.route('**/api/public/matches/1/live', route => route.fulfill({ json: liveFixture() }));
    await page.goto('/matches/1');
    await expect(page.getByRole('group', { name: 'Puntos del set actual' })).toBeVisible();
    await expect(page.getByRole('group', { name: 'Saque actual' })).toContainText('Pérez');
    const score = await page.locator('.live-points').boundingBox();
    const history = await page.locator('.set-history').boundingBox();
    const courtSummary = page.locator('.live-court summary');
    const court = await courtSummary.boundingBox();
    expect(score!.y).toBeLessThan(history!.y);
    expect(history!.y).toBeLessThan(court!.y);
    const home = await page.getByRole('heading', { name: 'Olimpia', exact: true }).first().boundingBox();
    const away = await page.getByRole('heading', { name: 'CBPS', exact: true }).first().boundingBox();
    expect(home!.x).toBeLessThan(away!.x);
    const positions = page.getByRole('list', { name: 'Cancha de Olimpia' });
    if (width < 1024) {
      await expect(positions).not.toBeVisible();
      await courtSummary.focus();
      await page.keyboard.press('Enter');
    }
    await expect(positions).toBeVisible();
    await expect(positions.getByRole('listitem')).toHaveCount(6);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`live-${width}-expanded.png`), fullPage: true });
    await courtSummary.click();
    await expect(positions).not.toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`live-${width}-collapsed.png`), fullPage: true });
  });
}

test('long team/player names preserve the mobile layout and logo failure fallback', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 844 });
  const live = liveFixture();
  live.home.teamName = 'Club Social y Deportivo de Nombre Muy Extenso';
  live.home.clubLogoUrl = '/missing-club-logo.png';
  live.homeCourt!.positions[0].player.displayName = 'JugadorConUnNombreExtraordinariamenteLargoSinEspacios';
  await page.route('**/api/public/matches/1', route => route.fulfill({ json: matchFixture() }));
  await page.route('**/api/public/matches/1/live', route => route.fulfill({ json: live }));
  await page.route('**/missing-club-logo.png', route => route.fulfill({ status: 404 }));
  await page.goto('/matches/1');
  await expect(page.getByRole('img', { name: `${live.home.teamName}, sin logo` })).toBeVisible();
  await page.locator('.live-court summary').click();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test('final route loads Live once and displays final sets without serving', async ({ page }) => {
  let calls = 0;
  const live = liveFixture({ status: 'Finished' });
  live.home.setsWon = 3;
  await page.route('**/api/public/matches/1', route => route.fulfill({ json: matchFixture({ status: 'Finished', result: { homeSets: 3, awaySets: 1, sets: [] } }) }));
  await page.route('**/api/public/matches/1/live', route => { calls++; return route.fulfill({ json: live }); });
  await page.goto('/matches/1');
  await expect(page.getByRole('status')).toHaveText('FINAL');
  await expect(page.getByRole('group', { name: 'Resultado final en sets' })).toContainText('3');
  await expect(page.getByRole('group', { name: 'Saque actual' })).toHaveCount(0);
  await expect(page.getByRole('list', { name: 'Cancha de Olimpia' })).not.toBeVisible();
  expect(calls).toBe(1);
});

test('scheduled detail explains expected absence without fetching Live', async ({ page }) => {
  let calls = 0;
  await page.route('**/api/public/matches/1', route => route.fulfill({ json: matchFixture({ status: 'Scheduled', liveAvailable: false }) }));
  await page.route('**/api/public/matches/1/live', route => { calls++; return route.fulfill({ status: 404 }); });
  await page.goto('/matches/1');
  await expect(page.getByRole('status')).toContainText('todavía no comenzó');
  expect(calls).toBe(0);
});
