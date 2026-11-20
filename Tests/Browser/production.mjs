import { chromium, expect } from '@playwright/test';

const origin = process.env.SMOKE_URL ?? 'https://salahtimes.dk';
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_EXECUTABLE });
try {
    const page = await browser.newPage();
    const home = await page.goto(origin, { waitUntil: 'domcontentloaded', timeout: 30000 });
    expect(home?.ok()).toBeTruthy();
    await expect(page.getByRole('heading', { name: 'Næste bøn', exact: true })).toBeVisible();
    await page.getByRole('combobox', { name: 'By eller postnummer' }).fill('8000');
    await page.getByRole('option', { name: /8000 Aarhus C/ }).click();
    await expect(page).toHaveURL(/\/post-8000$/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('8000 Aarhus C');
    await page.getByRole('link', { name: 'Dansk lokal tid og beregningsmetode' }).click();
    await expect(page.getByRole('heading', { name: 'Sådan beregnes bønnetiderne' })).toBeVisible();
    const catalog = await page.evaluate(async () => {
        const response = await fetch('/data/prayer-places.json');
        await response.arrayBuffer();
        const timing = performance.getEntriesByName(response.url).at(-1);
        return { ok: response.ok, transferred: timing?.encodedBodySize, decoded: timing?.decodedBodySize };
    });
    expect(catalog.ok).toBeTruthy();
    expect(catalog.transferred).toBeGreaterThan(0);
    expect(catalog.transferred).toBeLessThan(catalog.decoded);
    console.log('Verified prayer times, postcode navigation, calculation method and catalog compression.');
} finally {
    await browser.close();
}
