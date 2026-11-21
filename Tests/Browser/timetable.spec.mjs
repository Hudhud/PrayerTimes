import { expect, test } from '@playwright/test';

const search = page => page.getByRole('combobox', { name: 'By eller postnummer' });
const openPicker = page => page.getByRole('button', { name: 'Skift sted', exact: true }).click();

test('compact place picker opens with focus and closes with Escape', async ({ page, isMobile }) => {
    if (isMobile) {
        await page.setViewportSize({ width: 320, height: 720 });
    }
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/');
    const toggle = page.getByRole('button', { name: 'Skift sted', exact: true });
    await expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await expect(search(page)).toBeHidden();
    await expect(page.getByRole('heading', { name: 'Næste bøn', exact: true })).toBeInViewport();
    if (isMobile) {
        await toggle.tap();
        await expect(search(page)).toBeVisible();
        await expect(search(page)).not.toBeFocused();
        await page.getByRole('button', { name: 'Luk stedvælger', exact: true }).tap();
    }
    await page.evaluate(() => {
        const styles = document.documentElement.style;
        styles.fontSize = '200%';
        styles.fontFamily = '"DejaVu Sans", system-ui, sans-serif';
    });
    await toggle.press('Enter');
    await expect(search(page)).toBeFocused();
    const panel = page.locator('#place-picker-panel');
    await expect(panel).toHaveCSS('animation-name', 'none');
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize().width);
    await search(page).press('Escape');
    await expect(toggle).toBeFocused();
    await expect(search(page)).toBeHidden();
});

test('search by postcode and return to a recent place', async ({ page }) => {
    await page.goto('/');
    await openPicker(page);
    await search(page).fill('8000');
    await expect(page.getByRole('option', { name: /8000 Aarhus C/ })).toBeVisible();
    await search(page).press('ArrowDown');
    await search(page).press('Enter');
    await expect(page).toHaveURL(/\/post-8000$/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('8000 Aarhus C');

    await openPicker(page);
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
        await page.getByRole('link', { name: 'Skift sted' }).click();
        await page.getByRole('searchbox', { name: 'By eller postnummer' }).fill('6700');
        await page.getByRole('button', { name: 'Søg', exact: true }).click();
        await expect(page).toHaveURL(/\/post-6700$/);
    } finally {
        await context.close();
    }
});

test('a spelling suggestion requires an explicit choice', async ({ page }) => {
    await page.goto('/');
    await openPicker(page);
    await search(page).fill('Købenahvn');
    const suggestions = page.getByRole('listbox', { name: 'Mente du?' });
    await expect(suggestions.getByRole('option', { name: /København/ }).first()).toBeVisible();
    await search(page).press('Enter');
    await expect(page).toHaveURL(/\/steder\?q=/);
    await expect(page.getByRole('heading', { name: 'Mente du?' })).toBeVisible();
    await page.getByRole('link', { name: 'København', exact: true }).click();
    await expect(page).toHaveURL(/\/koebenhavn$/);
});

test('location opens and refreshes the nearby timetable, then respects a manual choice', async ({ page }) => {
    await page.addInitScript(() => {
        Object.defineProperty(navigator, 'geolocation', {
            value: {
                getCurrentPosition: success => {
                    const coords = localStorage.getItem('mock-moved') === 'yes'
                        ? { latitude: 55.403756, longitude: 10.40237 }
                        : { latitude: 56.162939, longitude: 10.203921 };
                    success({ coords });
                }
            }
        });
        Object.defineProperty(navigator, 'permissions', {
            value: { query: async () => ({ state: 'granted' }) }
        });
    });
    await page.goto('/');
    await openPicker(page);
    await page.getByRole('button', { name: 'Brug min placering' }).click();
    await expect(page).toHaveURL(/\/aarhus$/);
    await openPicker(page);
    await expect(page.getByRole('button', { name: 'Stop automatisk placering' })).toBeVisible();
    await page.evaluate(() => {
        const preference = JSON.parse(localStorage.getItem('salahtimes-auto-location'));
        preference.checkedAt = 0;
        localStorage.setItem('salahtimes-auto-location', JSON.stringify(preference));
        localStorage.setItem('mock-moved', 'yes');
    });
    await page.reload();
    await expect(page).toHaveURL(/\/odense$/);
    await openPicker(page);
    await expect(page.getByRole('button', { name: 'Stop automatisk placering' })).toBeVisible();
    await search(page).fill('6700');
    await page.getByRole('option', { name: /6700 Esbjerg/ }).click();
    await expect(page).toHaveURL(/\/post-6700$/);
    await openPicker(page);
    await expect(page.getByRole('button', { name: 'Brug min placering' })).toBeVisible();
});

test('denied location keeps search usable', async ({ page }) => {
    await page.addInitScript(() => {
        Object.defineProperty(navigator, 'geolocation', {
            value: { getCurrentPosition: (success, failure) => failure({ code: 1 }) }
        });
    });
    await page.goto('/');
    await openPicker(page);
    await page.getByRole('button', { name: 'Brug min placering' }).click();
    await expect(page.getByText(/Browseren giver ikke adgang/)).toBeVisible();
    await search(page).fill('Viby Aarhus');
    await expect(page.getByRole('option', { name: /Viby Aarhus Kommune/ })).toBeVisible();
});

test('a slow place search announces its status and keeps keyboard focus', async ({ page }) => {
    let releaseResponse;
    const pendingResponse = new Promise(resolve => { releaseResponse = resolve; });
    await page.route('**/steder/forslag?*', async route => {
        await pendingResponse;
        await route.continue();
    });

    await page.goto('/');
    await openPicker(page);
    await search(page).fill('Viby');
    const status = page.locator('.place-search-status');
    try {
        await expect(status).toHaveText('Søger efter steder…');
        await expect(status).not.toHaveClass(/visually-hidden/);
        await expect(search(page)).toBeFocused();
    } finally {
        releaseResponse();
    }

    await expect(page.getByRole('option', { name: /Viby Aarhus Kommune/ })).toBeVisible();
    await expect(status).toHaveClass(/visually-hidden/);
    await expect(search(page)).toBeFocused();
});

test('the public location catalog is compressed and keeps its entries', async ({ request }) => {
    const response = await request.get('/data/prayer-places.json', {
        headers: { 'accept-encoding': 'gzip' }
    });
    expect(response.ok()).toBeTruthy();
    expect(response.headers()['content-encoding']).toBe('gzip');
    expect(await response.json()).toContainEqual(expect.objectContaining({ id: 'aarhus', name: 'Aarhus' }));
});

test('shared month, mobile calendar, method and calendar export', async ({ page, isMobile }) => {
    if (isMobile) {
        await page.setViewportSize({ width: 320, height: 720 });
    }
    await page.goto('/koebenhavn?month=2026-06&week=2026-06-21');
    if (isMobile) {
        await expect(page.getByRole('heading', { name: /15.*21.*jun.*2026/ })).toBeVisible();
        await expect(page.getByRole('article').first()).toBeVisible();
        await expect(page.getByRole('table')).toBeHidden();
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize().width);
    } else {
        await expect(page.getByRole('heading', { name: 'juni 2026', exact: true })).toBeVisible();
        await expect(page.getByRole('table')).toBeVisible();
    }
    await page.getByRole('button', { name: 'Vis Hanafi' }).click();
    await expect(page.getByRole('button', { name: 'Vis standard' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Hent kalenderfil' })).toBeHidden();
    await page.getByText('Måned og eksport', { exact: true }).click();
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

test('mobile week navigation crosses month and year boundaries', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/koebenhavn?month=2026-11&week=2026-11-30#month-title');
    const days = page.locator('.week-day');
    await expect(page.getByRole('heading', { name: /30.*nov.*6.*dec.*2026/ })).toBeVisible();
    await expect(days).toHaveCount(7);
    const firstOfDecember = days.filter({ has: page.getByRole('heading', { name: 'tirsdag 1. december' }) });
    await expect(firstOfDecember.locator('dl > div').filter({ hasText: 'Fajr' })).toContainText(/\d\d:\d\d/);
    await expect(page.getByRole('link', { name: 'Næste måned' })).toBeHidden();
    await page.getByRole('link', { name: 'Næste uge' }).click();
    await expect(page).toHaveURL(/month=2026-12&week=2026-12-07#month-title$/);
    await page.getByRole('link', { name: 'Forrige uge' }).click();
    await expect(page).toHaveURL(/month=2026-11&week=2026-11-30#month-title$/);

    await page.goto('/koebenhavn?month=2026-12&week=2026-12-31#month-title');
    await expect(page.getByRole('heading', { name: /28.*dec.*2026.*3.*jan.*2027/ })).toBeVisible();
    await expect(days).toHaveCount(7);
    await expect(days.filter({ has: page.getByRole('heading', { name: 'fredag 1. januar' }) })).toBeVisible();
    await page.getByRole('link', { name: 'Næste uge' }).click();
    await expect(page).toHaveURL(/month=2027-01&week=2027-01-04#month-title$/);
});

test('live timetable refresh keeps the next prayer above today’s date and preserves the active search', async ({ page }) => {
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
    await openPicker(page);
    await search(page).fill('Viby');
    await expect(page.getByRole('heading', { name: 'Næste bøn opdateret' })).toBeVisible();
    const [card, date] = await Promise.all([
        page.locator('[data-next-prayer]').boundingBox(),
        page.locator('.today-context time').boundingBox()
    ]);
    expect(card.y + card.height).toBeLessThan(date.y);
    await expect(search(page)).toBeFocused();
    await expect(search(page)).toHaveValue('Viby');
});
