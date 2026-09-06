import { expect, test, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';

// Opt-in: use only a freshly reset demo match. All API responses come from the real backend.
const matchId = Number(process.env.RULES_E2E_MATCH_ID);
const api = process.env.RULES_E2E_API ?? 'http://127.0.0.1:5295';
async function stored(page: Page, store: string): Promise<any[]> {
  return page.evaluate(
    (store) =>
      new Promise<any[]>((resolve, reject) => {
        const open = indexedDB.open('LigaVolleyScorer');
        open.onsuccess = () => {
          const db = open.result,
            tx = db.transaction(store),
            request = tx.objectStore(store).getAll();
          request.onsuccess = () => resolve(request.result);
          tx.oncomplete = () => db.close();
          request.onerror = () => reject(request.error?.message);
        };
        open.onerror = () => reject(open.error?.message);
      }),
    store,
  );
}

test('real SQL demo: warnings, cancel/confirm, offline reentry, sync, Public and takeover', async ({
  page,
  context,
  request,
}, info) => {
  test.skip(!matchId, 'Set RULES_E2E_MATCH_ID after explicitly resetting the demo.');
  test.setTimeout(120_000);
  await page.setViewportSize({ width: 1280, height: 800 });
  let disconnected = false;
  await page.route('**/api/**', async (route) => {
    if (disconnected) return route.abort('internetdisconnected');
    const url = new URL(route.request().url());
    const response = await route.fetch({ url: api + url.pathname + url.search });
    await route.fulfill({ response });
  });
  const root = `${api}/api/scorer/matches/${matchId}`;
  const opening = await (await request.get(`${root}/open-context`)).json();
  expect(opening.existingMatchSheet).toBeNull();
  await page.goto(`/?matchId=${matchId}`);
  for (const side of ['home', 'away']) {
    for (const [index, player] of opening[side].players.entries()) {
      const row = page
        .locator(`.open-team.${side} .open-player`)
        .filter({ hasText: player.displayName });
      await row.getByRole('checkbox').check();
      await row.getByRole('spinbutton').fill(String(index + 1));
      if (index === 0) await row.getByRole('radio').check();
    }
  }
  await page.getByRole('button', { name: /Abrir acta/ }).click();
  await page.getByRole('button', { name: 'Preparar Set 1' }).click();
  for (const [index, side] of ['home', 'away'].entries()) {
    const team = page.locator('.prep-grid article').nth(index);
    await expect(team.getByLabel('Líbero del set').locator('option')).toHaveCount(2);
    if (index === 1) await team.locator('.lineup-slots button').filter({ hasText: 'P1' }).click();
    for (const player of opening[side].players
      .filter((x: any) => x.role.toUpperCase() !== 'LIBERO')
      .slice(0, 6))
      await team.getByRole('button', { name: new RegExp(player.displayName) }).click();
    await team.getByRole('button', { name: /Guardar/ }).click();
  }
  await page.getByRole('button', { name: 'HOME SACA', exact: true }).click();
  await page.getByRole('button', { name: /Iniciar set/i }).click();
  await expect(page.locator('.point-button.home')).toBeVisible();
  await expect
    .poll(async () => (await stored(page, 'events')).every((x) => x.syncStatus === 'ACCEPTED'))
    .toBe(true);
  const initial = await (await request.get(`${root}/sheet`)).json();
  expect(initial.rulesSnapshot.maxSubstitutionsPerSet).toBe(6);
  const regulars = opening.home.players.filter((x: any) => x.role.toUpperCase() !== 'LIBERO');
  const original = regulars[0].displayName,
    substitute = regulars[6].displayName;
  disconnected = true;
  await context.setOffline(true);
  for (let count = 1; count <= 7; count++) {
    await page
      .locator('.team-court.home .court-position')
      .filter({ has: page.locator('.position-label', { hasText: 'P1' }) })
      .click();
    const action = page.locator('.action-sheet');
    if (count >= 3)
      await action.getByRole('button', { name: 'Otra opción por decisión del juez' }).click();
    await action
      .getByRole('button', { name: new RegExp(count % 2 ? substitute : original) })
      .click();
    await action.getByRole('button', { name: 'Confirmar sustitución' }).click();
    if (count >= 3) {
      const warning = page.getByRole('dialog', { name: 'Advertencia reglamentaria' });
      await expect(warning).toBeVisible();
      if (count === 7) {
        const before = await stored(page, 'events');
        await expect(warning).toContainText('6/6 a 7/6');
        await page.screenshot({ path: info.outputPath('seventh-substitution-warning.png') });
        await warning.getByRole('button', { name: 'Cancelar' }).click();
        expect(await stored(page, 'events')).toEqual(before);
        await page
          .locator('.team-court.home .court-position')
          .filter({ hasText: original.split(' ').at(-1)! })
          .click();
        await action.getByRole('button', { name: 'Otra opción por decisión del juez' }).click();
        await action.getByRole('button', { name: new RegExp(substitute) }).click();
        await action.getByRole('button', { name: 'Confirmar sustitución' }).click();
      }
      await warning.getByRole('button', { name: 'Registrar igualmente' }).click();
    }
    await expect
      .poll(
        async () =>
          (await stored(page, 'events')).filter((x) => x.type === 'SUBSTITUTION_REQUEST').length,
      )
      .toBe(count);
  }
  for (let count = 1; count <= 3; count++) {
    await page
      .locator('.secondary-actions')
      .getByRole('button', { name: /Timeout/ })
      .click();
    await page.getByRole('button', { name: 'Timeout HOME', exact: true }).click();
    if (count === 3) await page.getByRole('button', { name: 'Registrar igualmente' }).click();
  }
  await page.locator('.point-button.home').click();
  const offlineEvents = await stored(page, 'events');
  expect(
    offlineEvents.filter((x) =>
      x.payload.confirmedRuleWarnings?.includes('substitution_limit_exceeded'),
    ),
  ).toHaveLength(1);
  await page.reload();
  await expect(page.locator('.secondary-actions')).toContainText('7/6');
  expect(await stored(page, 'events')).toEqual(offlineEvents);
  disconnected = false;
  await context.setOffline(false);
  await expect
    .poll(async () => (await stored(page, 'events')).every((x) => x.syncStatus === 'ACCEPTED'), {
      timeout: 20_000,
    })
    .toBe(true);
  const central = await (await request.get(`${root}/sheet`)).json();
  expect(central.currentState.homeTimeouts).toBe(3);
  expect(central.rulesSnapshot).toEqual(initial.rulesSnapshot);
  expect(central.operationalState.sets[0].substitutions).toHaveLength(7);
  const live = await (await request.get(`${api}/api/public/matches/${matchId}/live`)).json();
  expect(live.sets[0].homePoints).toBe(1);
  expect(live.servingPlayer.displayName).toBe(substitute);
  await info.attach('canonical-live.json', {
    body: JSON.stringify(live, null, 2),
    contentType: 'application/json',
  });
  await page.screenshot({ path: info.outputPath('accepted-offline-decisions.png') });

  // Another real device takes authority; the first device's next event must BLOCK, not merge.
  const takeover = await request.post(`${root}/take-over`, {
    data: {
      sheetUuid: central.sheet.sheetUuid,
      expectedSessionUuid: central.session.sessionUuid,
      deviceId: 'rules-e2e-other-device',
      clientRequestId: randomUUID(),
    },
  });
  expect(takeover.ok()).toBe(true);
  await page.locator('.point-button.home').click();
  await expect(
    page.getByRole('button', { name: 'Continuar desde estado central', exact: true }),
  ).toBeVisible();
  await expect(page.locator('.point-button.home')).toBeDisabled();
  await page.getByRole('button', { name: 'Continuar desde estado central', exact: true }).click();
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Continuar desde estado central' })
    .click();
  await expect(page.locator('.point-button.home')).toBeEnabled();
  const after = await (await request.get(`${root}/sheet`)).json();
  expect(after.home.liberos).toEqual(central.home.liberos);
  expect(after.rulesSnapshot).toEqual(central.rulesSnapshot);
  expect(
    (await stored(page, 'events')).filter((x) => x.sessionUuid === central.session.sessionUuid),
  ).toHaveLength(offlineEvents.length + 1);
  await page.locator('.point-button.home').click();
  await expect
    .poll(async () => (await (await request.get(`${root}/sheet`)).json()).currentState.homePoints)
    .toBe(2);
});
