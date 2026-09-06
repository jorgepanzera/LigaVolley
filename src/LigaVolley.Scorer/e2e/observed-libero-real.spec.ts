import { expect, test, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';

// Opt-in: use only a freshly reset demo match. All API responses come from the real backend.
const matchId = Number(process.env.OBSERVED_E2E_MATCH_ID);
const api = process.env.OBSERVED_E2E_API ?? 'http://127.0.0.1:5295';
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

test('real SQL demo: observed libero, court/bench, offline reload, sync, Public and takeover', async ({
  page,
  context,
  request,
}, info) => {
  test.skip(!matchId, 'Set OBSERVED_E2E_MATCH_ID after explicitly resetting the demo.');
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
    await expect(team.getByLabel('Líbero sugerido (opcional)').locator('option')).toHaveCount(2);
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
  const libero = initial.home.players.find((p: any) => initial.home.liberos.some((l: any) => l.matchPlayerId === p.matchPlayerId));
  const original = initial.home.players.find((p: any) => p.matchPlayerId === initial.operationalState.sets[0].lineups.HOME[4]);
  const court = (position: number) => page.locator('.team-court.home .court-position').filter({ has: page.locator('.position-label', { hasText: `P${position}` }) });
  const liveCourt = async () => (await (await request.get(`${api}/api/public/matches/${matchId}/live`)).json()).homeCourt.positions;
  expect(initial.operationalState.sets[0].liberoReplacements).toEqual([]);
  expect((await liveCourt()).find((p: any) => p.position === 5).player.isLibero).toBe(false);
  disconnected = true;
  await context.setOffline(true);
  await court(5).click();
  await page.getByRole('button', { name: new RegExp(`Ingresar líbero #${libero.jerseyNumber}`) }).click();
  await expect(court(5)).toContainText(`L #${libero.jerseyNumber}`);
  await expect(page.locator('.bench-side.home')).toContainText(original.displayName);
  await expect(page.locator('.bench-side.home')).not.toContainText(libero.displayName);
  expect((await liveCourt()).find((p: any) => p.position === 5).player.isLibero).toBe(false);
  const initialEvents = await stored(page, 'events');
  expect(initialEvents.filter(x => x.type === 'LIBERO_ENTER')).toHaveLength(1);
  expect(initialEvents.find(x => x.type === 'LIBERO_ENTER').syncStatus).toBe('PENDING');
  await page.reload();
  await expect(court(5)).toContainText(`L #${libero.jerseyNumber}`);
  expect(await stored(page, 'events')).toEqual(initialEvents);
  disconnected = false;
  await context.setOffline(false);
  await expect.poll(async () => (await stored(page, 'events')).every(x => x.syncStatus === 'ACCEPTED'), { timeout: 20_000 }).toBe(true);
  expect((await liveCourt()).find((p: any) => p.position === 5).player.isLibero).toBe(true);
  await page.locator('.point-button.away').click();
  await expect(page.locator('.point-button.home')).toBeEnabled();
  await page.locator('.point-button.home').click();
  await expect(court(4)).toContainText(`L #${libero.jerseyNumber}`);
  const beforeDiscard = await stored(page, 'events');
  await page.getByRole('button', {name:'Descartar sugerencia HOME P4'}).click();
  expect((await stored(page, 'events')).map(x=>x.eventUuid)).toEqual(beforeDiscard.map(x=>x.eventUuid));
  await court(4).click();
  await page.getByRole('button', {name:/Sale líbero \/ vuelve regular/}).click();
  await expect(court(4)).not.toContainText('L #');
  await expect(page.locator('.point-button.away')).toBeEnabled();
  await page.locator('.point-button.away').click();
  await court(1).click();
  await page.getByRole('button', {name:new RegExp(`Ingresar líbero #${libero.jerseyNumber}`)}).click();
  const warning = page.getByRole('dialog', {name:'Advertencia reglamentaria'});
  await expect(warning).toBeVisible();
  const beforeCancel = await stored(page, 'events');
  await warning.getByRole('button', {name:'Cancelar'}).click();
  expect((await stored(page, 'events')).map(x=>x.eventUuid)).toEqual(beforeCancel.map(x=>x.eventUuid));
  await court(1).click();
  await page.getByRole('button', {name:new RegExp(`Ingresar líbero #${libero.jerseyNumber}`)}).click();
  await warning.getByRole('button', {name:'Registrar igualmente'}).click();
  await expect(court(1)).toContainText(`L #${libero.jerseyNumber}`);
  await expect.poll(async () => (await stored(page, 'events')).every(x=>x.syncStatus==='ACCEPTED'), {timeout:20_000}).toBe(true);
  const offlineEvents = await stored(page, 'events');
  const central = await (await request.get(`${root}/sheet`)).json();
  expect(central.operationalState.sets[0].liberoReplacements.filter((x:any)=>x.active)).toHaveLength(1);
  expect(central.operationalState.sets[0].liberoReplacements.every((x:any)=>x.automatic===false)).toBe(true);
  expect(central.operationalState.sets[0].substitutions).toEqual([]);
  expect((await liveCourt()).find((p:any)=>p.position===1).player.isLibero).toBe(true);
  await page.screenshot({path:info.outputPath('observed-court.png')});
  await info.attach('canonical-sheet.json',{body:JSON.stringify(central,null,2),contentType:'application/json'});
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
    .toBe(central.currentState.homePoints + 1);
  expect(after.operationalState.sets[0].liberoReplacements).toEqual(central.operationalState.sets[0].liberoReplacements);
});
