import { expect, test } from '@playwright/test';

const search = page => page.getByRole('combobox', { name: 'By eller postnummer' });

test('search by postcode and return to a recent place', async ({ page }) => {
    await page.goto('/');
    await search(page).fill('8000');
    await expect(page.getByRole('option', { name: /8000 Aarhus C/ })).toBeVisible();
    await search(page).press('ArrowDown');
    await search(page).press('Enter');
    await expect(page).toHaveURL(/\/post-8000$/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('8000 Aarhus C');

    await search(page).focus();
    await expect(page.getByRole('listbox', { name: 'Senest brugte steder' })).toBeVisible();
    await page.getByRole('option', { name: 'København' }).click();
    await expect(page).toHaveURL(/\/koebenhavn$/);
});

test('a one-result search also works without JavaScript', async ({ browser }) => {
    const context = await browser.newContext({ javaScriptEnabled: false });
    try {
        const page = await context.newPage();
        await page.goto('/');
        await page.getByRole('searchbox', { name: 'By eller postnummer' }).fill('6700');
        await page.getByRole('button', { name: 'Søg', exact: true }).click();
        await expect(page).toHaveURL(/\/post-6700$/);
    } finally {
        await context.close();
    }
});

test('location opens a nearby timetable and a manual place disables it', async ({ page }) => {
    await page.addInitScript(() => {
        Object.defineProperty(navigator, 'geolocation', {
            value: { getCurrentPosition: success => success({ coords: { latitude: 56.162939, longitude: 10.203921 } }) }
        });
        Object.defineProperty(navigator, 'permissions', {
            value: { query: async () => ({ state: 'granted' }) }
        });
    });
    await page.goto('/');
    await page.getByRole('button', { name: 'Brug min placering' }).click();
    await expect(page).toHaveURL(/\/aarhus$/);
    await expect(page.getByRole('button', { name: 'Stop automatisk placering' })).toBeVisible();
    await search(page).fill('6700');
    await page.getByRole('option', { name: /6700 Esbjerg/ }).click();
    await expect(page).toHaveURL(/\/post-6700$/);
    await expect(page.getByRole('button', { name: 'Brug min placering' })).toBeVisible();
});

test('denied location keeps search usable', async ({ page }) => {
    await page.addInitScript(() => {
        Object.defineProperty(navigator, 'geolocation', {
            value: { getCurrentPosition: (success, failure) => failure({ code: 1 }) }
        });
    });
    await page.goto('/');
    await page.getByRole('button', { name: 'Brug min placering' }).click();
    await expect(page.getByText(/Browseren giver ikke adgang/)).toBeVisible();
    await search(page).fill('Viby Aarhus');
    await expect(page.getByRole('option', { name: /Viby Aarhus Kommune/ })).toBeVisible();
});

test('shared month, mobile calendar, method and calendar export', async ({ page, isMobile }) => {
    if (isMobile) {
        await page.setViewportSize({ width: 320, height: 720 });
    }
    await page.goto('/koebenhavn?month=2026-06&week=2026-06-21');
    await expect(page.getByRole('heading', { name: 'juni 2026', exact: true })).toBeVisible();
    if (isMobile) {
        await expect(page.getByRole('article').first()).toBeVisible();
        await expect(page.getByRole('table')).toBeHidden();
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize().width);
    } else {
        await expect(page.getByRole('table')).toBeVisible();
    }
    await page.getByRole('button', { name: 'Vis Hanafi' }).click();
    await expect(page.getByRole('button', { name: 'Vis standard' })).toBeVisible();
    const url = await page.getByRole('link', { name: 'Hent kalenderfil' }).getAttribute('href');
    const exported = await page.request.get(url);
    expect(exported.ok()).toBeTruthy();
    expect(exported.headers()['content-type']).toContain('text/calendar');
    const ics = await exported.text();
    expect(ics).toContain('UID:koebenhavn-2026-06-21-asr@salahtimes.dk');
    expect(ics).not.toContain('UID:koebenhavn-2026-06-21-fajr@salahtimes.dk');
    await page.emulateMedia({ media: 'print' });
    await expect(page.getByRole('table')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Hent kalenderfil' })).toBeHidden();
    await page.emulateMedia({ media: 'screen' });
    await page.getByRole('link', { name: 'Dansk lokal tid og beregningsmetode' }).click();
    await expect(page.getByRole('heading', { name: 'Sådan beregnes bønnetiderne' })).toBeVisible();
});

test('live timetable refresh keeps the active search intact', async ({ page }) => {
    await page.route('/', async route => {
        const response = await route.fetch();
        const body = (await response.text())
            .replace(/data-rendered-at="[^"]+"/, 'data-rendered-at="2026-11-20T20:00:01Z"')
            .replace(/data-refresh-at="[^"]+"/, 'data-refresh-at="2026-11-20T20:00:02Z"');
        await route.fulfill({ response, body });
    }, { times: 1 });
    await page.route('**/koebenhavn/i-dag', async route => {
        const response = await route.fetch();
        const body = (await response.text()).replace('Næste bøn</h2>', 'Næste bøn opdateret</h2>');
        await route.fulfill({ response, body });
    }, { times: 1 });

    await page.goto('/');
    await search(page).fill('Viby');
    await expect(page.getByRole('heading', { name: 'Næste bøn opdateret' })).toBeVisible();
    await expect(search(page)).toBeFocused();
    await expect(search(page)).toHaveValue('Viby');
});
