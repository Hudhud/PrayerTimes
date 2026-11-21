import { chromium, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';

const origin = process.env.SMOKE_URL ?? 'https://salahtimes.dk';
const danishDate = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Copenhagen', dateStyle: 'short' });
const fixture = JSON.parse(await readFile(new URL('../Fixtures/local-provider-2026.json', import.meta.url), 'utf8'));
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_EXECUTABLE });
try {
    const page = await browser.newPage();
    const home = await page.goto(origin, { waitUntil: 'domcontentloaded', timeout: 30000 });
    expect(home?.ok()).toBeTruthy();
    await expect(page.getByRole('heading', { name: 'Næste bøn', exact: true })).toBeVisible();
    const placeSearch = page.getByRole('combobox', { name: 'By eller postnummer' });
    await expect(placeSearch).toBeHidden();
    await page.getByRole('button', { name: 'Skift sted', exact: true }).click();
    await placeSearch.fill('Købenahvn');
    await expect(page.getByRole('listbox', { name: 'Mente du?' }).getByRole('option', { name: /København/ }).first()).toBeVisible();
    await placeSearch.fill('8000');
    const dateBeforeNavigation = danishDate.format(new Date());
    await page.getByRole('option', { name: /8000 Aarhus C/ }).click();
    await expect(page).toHaveURL(/\/post-8000$/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('8000 Aarhus C');
    const today = page.locator('[data-current-place="post-8000"][data-refresh-url]');
    await expect(today).toBeVisible();
    const todayDate = await today.getAttribute('data-today');
    expect([dateBeforeNavigation, danishDate.format(new Date())]).toContain(todayDate);
    await expect(today.locator('.today-context time')).toHaveAttribute('datetime', todayDate);

    const row = page.locator(`tr[data-calendar-date="${todayDate}"] td`);
    await expect(row).toHaveCount(6);
    const dailyTimes = await today.locator('.prayer-list > div').evaluateAll(prayers =>
        prayers.map(prayer => prayer.querySelector('time')?.textContent ?? '- -'));
    expect(dailyTimes).toHaveLength(5);
    const sunrise = await today.locator('.sunrise time').textContent();
    const [fajr, dhuhr, asr, maghrib, isha] = dailyTimes;
    for (const [index, time] of [fajr, sunrise, dhuhr, asr, maghrib, isha].entries()) {
        expect((await row.nth(index).textContent()).trim().startsWith(time)).toBeTruthy();
    }
    for (const time of [sunrise, dhuhr, asr, maghrib]) {
        expect(time).toMatch(/^(?:[01]\d|2[0-3]):[0-5]\d$/);
    }
    expect(sunrise < dhuhr && dhuhr < asr && asr < maghrib).toBeTruthy();

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
    await page.setViewportSize({ width: 360, height: 720 });
    const referencePage = await page.goto(new URL('/koebenhavn?month=2026-11&week=2026-11-30#month-title', origin).href);
    expect(referencePage?.ok()).toBeTruthy();
    await expect(page.getByRole('heading', { level: 1 })).toContainText('København');
    await expect(page.getByRole('heading', { name: 'tirsdag 1. december' })).toBeVisible();
    await expect(page.locator('.week-day')).toHaveCount(7);
    const expected = fixture.find(day => day.city === 'koebenhavn' && day.date === '2026-12-01');
    const referenceDay = page.locator(`article[data-calendar-date="${expected.date}"]`);
    await expect(referenceDay.locator('h3 time')).toHaveAttribute('datetime', expected.date);
    await expect(referenceDay.locator('.prayer-list time')).toHaveText([
        expected.fajr, expected.dhuhr, expected.asr, expected.maghrib, expected.isha
    ]);
    await expect(referenceDay.locator('.sunrise time')).toHaveText(expected.sunrise);
    console.log('Verified current place and Danish date, prayer times against calendar and reference data, postcode navigation, calculation method, catalog compression and week navigation.');
} finally {
    await browser.close();
}
