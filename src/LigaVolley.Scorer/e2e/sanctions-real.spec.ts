import { expect, test, type Page } from '@playwright/test';

// Opt-in: run only after `dotnet run --project src/LigaVolley.Api -- --seed-demo-match`.
// The seed resets this MatchSheet and exposes the DEMO/LV-DEMO-* roster deterministically.
const matchId = Number(process.env.SANCTIONS_E2E_MATCH_ID);
const api = process.env.SANCTIONS_E2E_API ?? 'http://127.0.0.1:5295';

async function stored(page: Page, store: string): Promise<any[]> {
  return page.evaluate(
    (store) =>
      new Promise<any[]>((resolve, reject) => {
        const open = indexedDB.open('LigaVolleyScorer');
        open.onsuccess = () => {
          const db = open.result;
          const transaction = db.transaction(store);
          const request = transaction.objectStore(store).getAll();
          request.onsuccess = () => resolve(request.result);
          request.onerror = () => reject(request.error?.message);
          transaction.oncomplete = () => db.close();
        };
        open.onerror = () => reject(open.error?.message);
      }),
    store,
  );
}

async function accepted(page: Page) {
  await expect.poll(async () => (await stored(page, 'events')).every((event) => event.syncStatus === 'ACCEPTED'), {
    timeout: 20_000,
  }).toBe(true);
}

async function sanction(
  page: Page,
  values: { side?: 'HOME' | 'AWAY'; type: string; subject: 'Player' | 'Staff' | 'Team'; person?: string },
) {
  await page.getByRole('button', { name: /Sanciones/ }).click();
  const dialog = page.getByRole('dialog', { name: 'SANCIONES' });
  if (values.side) await dialog.getByLabel('Lado').selectOption(values.side);
  await dialog.getByLabel('Tipo').selectOption(values.type);
  await dialog.getByLabel('Sujeto').selectOption(values.subject);
  if (values.subject !== 'Team') await dialog.getByLabel('Persona').selectOption({ label: values.person! });
  await dialog.getByRole('button', { name: 'Confirmar', exact: true }).click();
}

async function prepareAndStart(page: Page, opening: any) {
  await page.goto(`/?matchId=${matchId}`);
  for (const side of ['home', 'away']) {
    for (const [index, player] of opening[side].players.entries()) {
      const row = page.locator(`.open-team.${side} .open-player`).filter({ hasText: player.displayName });
      await row.getByRole('checkbox').first().check();
      await row.getByRole('spinbutton').fill(String(index + 1));
      if (index === 0) await row.getByRole('radio').check();
    }
    const staff = opening[side].staff[0];
    expect(staff, `${side} demo roster needs frozen staff`).toBeTruthy();
    await page.locator(`.open-team.${side} .opening-staff`).getByText(staff.displayName).click();
  }
  await page.getByRole('button', { name: /Abrir acta/ }).click();
  await page.getByRole('button', { name: 'Preparar Set 1' }).click();
  for (const [index, side] of ['home', 'away'].entries()) {
    const team = page.locator('.prep-grid article').nth(index);
    if (index === 1) await team.locator('.lineup-slots button').filter({ hasText: 'P1' }).click();
    for (const player of opening[side].players.filter((player: any) => player.role?.toUpperCase() !== 'LIBERO').slice(0, 6))
      await team.getByRole('button', { name: new RegExp(player.displayName) }).click();
    await team.getByRole('button', { name: /Guardar/ }).click();
  }
  await page.getByRole('button', { name: 'HOME SACA', exact: true }).click();
  await page.getByRole('button', { name: /Iniciar set/i }).click();
  await expect(page.locator('.point-button.home')).toBeVisible();
  await accepted(page);
}

async function attemptIneligibleSubstitution(page: Page, playerName: string) {
  await page.locator('.team-court.home .court-position').filter({ hasText: 'P1' }).click();
  const action = page.locator('.action-sheet');
  await action.getByRole('button', { name: 'Otra opción por decisión del juez' }).click();
  await action.getByRole('button', { name: new RegExp(playerName) }).click();
  await action.getByRole('button', { name: 'Confirmar sustitución' }).click();
  await expect(action.getByRole('alert')).toContainText('No se pudo registrar la sustitución');
  await action.getByRole('button', { name: '×' }).click();
}

test('real SQL demo: sanctions connect UI, local pipeline, sync and canonical reload', async ({ page, context, request }) => {
  test.skip(!matchId, 'Set SANCTIONS_E2E_MATCH_ID after explicitly resetting the demo.');
  test.setTimeout(120_000);
  await page.setViewportSize({ width: 1280, height: 800 });
  let disconnected = false;
  await page.route('**/api/**', async (route) => {
    if (disconnected) return route.abort('internetdisconnected');
    const url = new URL(route.request().url());
    await route.fulfill({ response: await route.fetch({ url: api + url.pathname + url.search }) });
  });
  const root = `${api}/api/scorer/matches/${matchId}`;
  const opening = await (await request.get(`${root}/open-context`)).json();
  expect(opening.existingMatchSheet).toBeNull();
  expect(opening.home.players.length).toBeGreaterThanOrEqual(7);
  await prepareAndStart(page, opening);

  // Opening the operational dialog exposes HOME/AWAY, type, subject and frozen people.
  await page.getByRole('button', { name: /Sanciones/ }).click();
  const dialog = page.getByRole('dialog', { name: 'SANCIONES' });
  await expect(dialog.getByLabel('Lado')).toHaveText(/HOME.*AWAY/);
  await expect(dialog.getByLabel('Tipo')).toHaveText(/Advertencia por conducta.*Expulsión/);
  await expect(dialog.getByLabel('Sujeto')).toHaveText(/Jugador.*Staff.*Equipo/);
  await expect(dialog.getByLabel('Persona')).toContainText(opening.home.players[0].displayName);
  const beforeCancel = await stored(page, 'events');
  await dialog.getByLabel('Persona').selectOption({ label: `#1 ${opening.home.players[0].displayName}` });
  await dialog.getByRole('button', { name: 'Cancelar' }).click();
  await expect(dialog).toBeHidden();
  expect(await stored(page, 'events')).toEqual(beforeCancel);
  await expect(page.locator('.team-score.home > strong')).toHaveText('0');
  await expect(page.getByRole('button', { name: 'Historial' })).toBeVisible();

  disconnected = true;
  await context.setOffline(true);
  await sanction(page, { type: 'MisconductWarning', subject: 'Player', person: `#1 ${opening.home.players[0].displayName}` });
  await expect.poll(async () => (await stored(page, 'events')).filter((event) => event.type === 'SANCTION').length).toBe(1);
  await expect.poll(async () => (await stored(page, 'events')).find((event) => event.type === 'SANCTION')?.syncStatus).toBe('PENDING');
  await page.getByRole('button', { name: 'Historial' }).click();
  await expect(page.locator('.history')).toContainText(`MisconductWarning · HOME · ${opening.home.players[0].displayName}`);
  await expect(page.locator('.history')).toContainText('Sanción');
  await page.locator('.history .close').click();
  await expect(page.locator('.team-score.home > strong')).toHaveText('0');
  disconnected = false;
  await context.setOffline(false);
  await accepted(page);

  await page.getByRole('button', { name: /Sanciones/ }).click();
  const penalty = page.getByRole('dialog', { name: 'SANCIONES' });
  await penalty.getByLabel('Tipo').selectOption('MisconductPenalty');
  await penalty.getByLabel('Sujeto').selectOption('Team');
  await expect(penalty).toContainText('Consecuencia: +1 punto y saque para AWAY');
  await penalty.getByRole('button', { name: 'Confirmar', exact: true }).click();
  await expect(page.locator('.team-score.away > strong')).toHaveText('1');
  await accepted(page);
  expect((await stored(page, 'events')).filter((event) => event.type === 'POINT')).toHaveLength(0);

  await sanction(page, { type: 'ImproperRequest', subject: 'Team' });
  const staff = opening.away.staff[0].displayName;
  await sanction(page, { side: 'AWAY', type: 'DelayWarning', subject: 'Staff', person: staff });
  await accepted(page);
  await page.getByRole('button', { name: 'Historial' }).click();
  await expect(page.locator('.history')).toContainText(`DelayWarning · AWAY · ${staff}`);
  await expect(page.locator('.history')).toContainText('ImproperRequest · HOME');
  await page.locator('.history .close').click();

  const expelledPlayer = opening.home.players.filter((player: any) => player.role?.toUpperCase() !== 'LIBERO')[6];
  const expelled = expelledPlayer.displayName;
  const expelledJersey = opening.home.players.indexOf(expelledPlayer) + 1;
  await sanction(page, { type: 'Expulsion', subject: 'Player', person: `#${expelledJersey} ${expelled}` });
  await attemptIneligibleSubstitution(page, expelled);
  await sanction(page, { type: 'Disqualification', subject: 'Player', person: `#${expelledJersey} ${expelled}` });
  await accepted(page);

  // Finish this set using real point controls, then prove the disqualified player remains unavailable next set.
  for (let point = 1; point < 25; point++) {
    await page.locator('.point-button.away').click();
    await page.waitForTimeout(450);
  }
  await expect(page.getByRole('button', { name: 'Preparar Set 2' })).toBeVisible();
  await page.getByRole('button', { name: 'Preparar Set 2' }).click();
  for (const [index, side] of ['home', 'away'].entries()) {
    const team = page.locator('.prep-grid article').nth(index);
    if (index === 1) await team.locator('.lineup-slots button').filter({ hasText: 'P1' }).click();
    for (const player of opening[side].players.filter((player: any) => player.role?.toUpperCase() !== 'LIBERO').slice(0, 6))
      await team.getByRole('button', { name: new RegExp(player.displayName) }).click();
    await team.getByRole('button', { name: /Guardar/ }).click();
  }
  await page.getByRole('button', { name: 'HOME SACA', exact: true }).click();
  await page.getByRole('button', { name: /Iniciar set/i }).click();
  await attemptIneligibleSubstitution(page, expelled);
  await accepted(page);

  await page.reload();
  await expect(page.locator('.team-score.away > strong')).toHaveText('0');
  await page.getByRole('button', { name: 'Historial' }).click();
  await expect(page.locator('.history')).toContainText(`Disqualification · HOME · ${expelled}`);
  await expect(page.locator('.history')).toContainText(`MisconductPenalty · HOME · ${opening.home.teamName}`);
  await page.locator('.history .close').click();
  await attemptIneligibleSubstitution(page, expelled);
  const canonical = await (await request.get(`${root}/sheet`)).json();
  expect(canonical.operationalState.disciplinaryEvents).toHaveLength(6);
  expect(canonical.operationalState.matchIneligiblePlayerIds).toContain(
    canonical.home.players.find((player: any) => player.displayName === expelled).matchPlayerId,
  );
});
