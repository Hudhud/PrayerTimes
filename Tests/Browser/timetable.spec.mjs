import { expect, test } from '@playwright/test';
import { once } from 'node:events';
import { createServer, request as httpRequest } from 'node:http';
import { request as httpsRequest } from 'node:https';

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
    await expect(page.getByText('Standardsted', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Brug min placering', exact: true })).toBeVisible();
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
    await expect(page.getByText('Standardsted', { exact: true })).toBeHidden();
    await expect(page.getByRole('button', { name: 'Brug min placering', exact: true })).toBeHidden();

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
                        ? { latitude: 55.403756, longitude: 10.40237, accuracy: 30 }
                        : { latitude: 56.162939, longitude: 10.203921, accuracy: 30 };
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
    await expect(page.getByText('Automatisk valgt', { exact: true })).toBeVisible();
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
    await expect(page.getByText('Automatisk valgt', { exact: true })).toBeVisible();
    await openPicker(page);
    await expect(page.getByRole('button', { name: 'Stop automatisk placering' })).toBeVisible();
    await search(page).fill('6700');
    await page.getByRole('option', { name: /6700 Esbjerg/ }).click();
    await expect(page).toHaveURL(/\/post-6700$/);
    await expect(page.getByText('Automatisk valgt', { exact: true })).toBeHidden();
    await openPicker(page);
    await expect(page.getByRole('button', { name: 'Brug min placering' })).toBeVisible();
});

test('an imprecise automatic location keeps the current place until confirmed', async ({ page }) => {
    await page.addInitScript(() => {
        localStorage.setItem('salahtimes-auto-location', JSON.stringify({ placeId: 'koebenhavn', checkedAt: 0 }));
        Object.defineProperty(navigator, 'geolocation', {
            value: { getCurrentPosition: success => success({
                coords: { latitude: 56.162939, longitude: 10.203921, accuracy: 4500 }
            }) }
        });
        Object.defineProperty(navigator, 'permissions', {
            value: { query: async () => ({ state: 'granted' }) }
        });
    });

    await page.goto('/koebenhavn');
    const suggestion = page.getByRole('button', { name: 'Vælg Aarhus' });
    await expect(suggestion).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toContainText('København');
    await expect(page).toHaveURL(/\/koebenhavn$/);
    await suggestion.click();
    await expect(page).toHaveURL(/\/aarhus$/);
    await expect(page.getByText('Automatisk valgt', { exact: true })).toBeHidden();
});

test('the first-visit location shortcut keeps search usable when permission is denied', async ({ page }) => {
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
    await expect(page.getByRole('heading', { name: 'juni 2026', exact: true })).toBeVisible();
    if (isMobile) {
        await expect(page.getByRole('table')).toBeHidden();
        const brightNight = page.locator('details[data-calendar-date="2026-06-21"] .prayer-list');
        await expect(brightNight).toBeVisible();
        for (const prayer of ['Fajr', 'Isha']) {
            const entry = brightNight.locator('div').filter({ has: page.getByText(prayer, { exact: true }) });
            await expect(entry).toContainText('Solens vinkel nås ikke.');
            await expect(entry.getByRole('link', { name: 'Læs om lyse nætter' })).toHaveAttribute('href', '/beregning#lyse-naetter');
        }
        expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize().width);
    } else {
        await expect(page.getByRole('table')).toBeVisible();
    }
    await page.getByRole('button', { name: 'Vis Hanafi' }).click();
    await expect(page.getByRole('button', { name: 'Vis standard' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Hent kalenderfil' })).toBeHidden();
    await page.getByText('Udskriv og eksportér', { exact: true }).click();
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
    await page.getByRole('contentinfo').getByRole('link', { name: 'Dansk lokal tid og beregningsmetode' }).click();
    await expect(page.getByRole('heading', { name: 'Sådan beregnes bønnetiderne' })).toBeVisible();
});

test('saved Fajr and Isha angles apply to the calendar and export and can be reset', async ({ page, isMobile }) => {
    await page.goto('/koebenhavn?month=2026-06&week=2026-06-21');
    const settings = page.locator('#fajr-isha');
    const date = page.locator(`${isMobile ? 'details' : 'tr'}[data-calendar-date="2026-06-21"]`);
    await expect(date).toContainText('lyse nætter', { ignoreCase: true });
    await settings.locator('summary').click();
    await settings.getByLabel('Fajr (°)').fill('5,5');
    await settings.getByLabel('Isha (°)').fill('6.5');
    await settings.getByRole('button', { name: 'Gem vinkler' }).click();
    await expect(settings.locator('summary')).toContainText('Fajr 5,5° · Isha 6,5°');
    await expect(date).not.toContainText('lyse nætter', { ignoreCase: true });
    await page.reload();
    await expect(page.locator('[data-current-place="koebenhavn"]')).toHaveAttribute('data-angles', '5.5:6.5');

    const refreshUrl = await page.locator('[data-refresh-url]').getAttribute('data-refresh-url');
    const refresh = await page.request.get(refreshUrl);
    expect(await refresh.text()).toContain('data-angles="5.5:6.5"');

    await page.getByText('Udskriv og eksportér', { exact: true }).click();
    const exportUrl = await page.getByRole('link', { name: 'Hent kalenderfil' }).getAttribute('href');
    const exported = await page.request.get(exportUrl);
    expect(exported.ok()).toBeTruthy();
    const ics = await exported.text();
    expect(ics).toContain('UID:koebenhavn-2026-06-21-fajr@salahtimes.dk');
    expect(ics).toContain('UID:koebenhavn-2026-06-21-isha@salahtimes.dk');

    await settings.locator('summary').click();
    await settings.getByRole('button', { name: 'Nulstil til 18° og 17°' }).click();
    await expect(settings.locator('summary')).toContainText('Fajr 18° · Isha 17°');
    await expect(date).toContainText('lyse nætter', { ignoreCase: true });
});

test('the month heading opens a date picker and jumps directly to the chosen day', async ({ page, isMobile }) => {
    await page.setViewportSize({ width: isMobile ? 388 : 1164, height: 800 });
    await page.goto('/post-5000?month=2026-11#month-title');
    const trigger = page.getByRole('button', { name: 'november 2026', exact: true });
    const dialog = page.getByRole('dialog', { name: 'Vælg dato' });
    await trigger.click();
    await expect(dialog.getByLabel('Måned')).toBeFocused();
    await dialog.getByRole('button', { name: 'Annuller' }).click();
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();

    await trigger.click();
    await dialog.getByLabel('År').fill('2028');
    await dialog.getByLabel('Måned').selectOption('2');
    await dialog.getByLabel('Dag').selectOption('29');
    await dialog.getByLabel('År').fill('2027');
    await expect(dialog.getByLabel('Dag')).toHaveValue('28');
    await dialog.getByLabel('Måned').selectOption('6');
    await dialog.getByLabel('Dag').selectOption('21');
    await dialog.getByRole('button', { name: 'Vis bønnetider' }).click();

    await expect(page).toHaveURL(/\/post-5000\?month=2027-06&week=2027-06-21#calendar-selection$/);
    await expect(page.getByRole('heading', { name: 'juni 2027', exact: true })).toBeVisible();
    if (isMobile) {
        const day = page.locator('details[data-calendar-date="2027-06-21"]');
        await expect(day.locator('summary')).toBeFocused();
        await expect(day.locator('.prayer-list')).toBeInViewport();
        await expect(day.getByRole('link', { name: 'Læs om lyse nætter' })).toHaveCount(2);
        await expect(page.locator('.calendar-place')).toContainText('Odense C');
        await expect.poll(() => day.locator('summary').evaluate(summary =>
            summary.getBoundingClientRect().top >= document.querySelector('.calendar-header').getBoundingClientRect().bottom
        )).toBeTruthy();
    } else {
        const row = page.locator('tr[data-calendar-date="2027-06-21"]');
        await expect(row).toBeFocused();
        await expect(row).toBeInViewport();
        await expect(page.getByRole('columnheader', { name: 'Fajr' })).toBeInViewport();
    }
});

test('opening the next day keeps its date below the sticky calendar controls', async ({ page, isMobile }) => {
    test.skip(!isMobile, 'The desktop calendar uses a month table.');
    await page.setViewportSize({ width: 388, height: 800 });
    await page.goto('/post-5000?month=2026-11&week=2026-11-24#calendar-selection');
    const day = page.locator('details[data-calendar-date="2026-11-25"]');
    const summary = day.locator('summary');
    await summary.evaluate(element => element.scrollIntoView({ block: 'start' }));
    await summary.click();
    await expect(day.locator('.prayer-list')).toBeInViewport();
    await expect.poll(() => summary.evaluate(element =>
        element.getBoundingClientRect().top >= document.querySelector('.calendar-header').getBoundingClientRect().bottom
    )).toBeTruthy();
});

test('the mobile calendar link opens today and keeps the location and month controls in view', async ({ page, isMobile }) => {
    test.skip(!isMobile, 'The desktop calendar uses a month table.');
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/aarhus');
    const calendar = page.getByRole('region', { name: 'Bønnekalender' });
    const today = calendar.locator('.month-day.current-day');

    await today.locator('summary').click();
    await expect(today.locator('.prayer-list')).toBeHidden();
    await page.getByRole('link', { name: 'Kalender' }).click();

    await expect(page).toHaveURL(/\/aarhus#week-today$/);
    await expect(today.locator('.prayer-list')).toBeInViewport();
    await expect(calendar.locator('.calendar-place')).toHaveText('Aarhus');
    await expect(calendar.locator('.calendar-place')).toBeInViewport();
    await expect(calendar.getByRole('link', { name: 'Næste måned' })).toBeInViewport();
    await expect.poll(() => page.evaluate(() =>
        document.querySelector('.month-day.current-day').getBoundingClientRect().top >=
        document.querySelector('.calendar-header').getBoundingClientRect().bottom)).toBeTruthy();
});

test('on a narrow screen, the whole month is browsable and today is one tap away', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/koebenhavn?month=2025-12#month-title');
    const days = page.locator('.month-day');
    await expect(page.getByRole('heading', { name: 'december 2025', exact: true })).toBeVisible();
    await expect(days).toHaveCount(31);
    const first = days.first();
    const last = days.last();
    await expect(first.locator('summary time')).toHaveAttribute('datetime', '2025-12-01');
    await expect(last.locator('summary time')).toHaveAttribute('datetime', '2025-12-31');
    await expect(first.locator('.prayer-list')).toBeHidden();
    await first.locator('summary').press('Enter');
    await expect(first.locator('.prayer-list time')).toHaveCount(5);
    await expect(first.locator('.sunrise time')).toBeVisible();
    await last.locator('summary').click();
    await expect(last.locator('.prayer-list')).toBeVisible();
    await expect(first.locator('.prayer-list')).toBeHidden();

    await page.getByRole('link', { name: 'Næste måned' }).click();
    await expect(page).toHaveURL(/month=2026-01#month-title$/);
    await expect(page.getByRole('heading', { name: 'januar 2026', exact: true })).toBeVisible();
    await page.getByRole('link', { name: 'Forrige måned' }).click();
    await expect(page).toHaveURL(/month=2025-12#month-title$/);
    await page.getByRole('link', { name: 'Hop til i dag' }).click();
    await expect(page).toHaveURL(/\/koebenhavn#week-today$/);
    const date = await page.locator('.today').getAttribute('data-today');
    const current = days.filter({ has: page.locator(`summary time[datetime="${date}"]`) });
    await expect(current.locator('[data-today-label]')).toBeVisible();
    await expect(current.locator('.prayer-list')).toBeVisible();
    await current.locator('summary').click();
    await expect(current.locator('.prayer-list')).toBeHidden();
    await page.getByRole('link', { name: 'Hop til i dag' }).click();
    await expect(current.locator('.prayer-list')).toBeVisible();
});

test('live timetable refresh keeps the next prayer above today’s date and preserves the active search', async ({ page }) => {
    await page.route('/', async route => {
        const response = await route.fetch();
        const body = (await response.text())
            .replace(/data-rendered-at="[^"]+"/, 'data-rendered-at="2026-11-20T20:00:01Z"')
            .replace(/data-refresh-at="[^"]+"/, 'data-refresh-at="2026-11-20T20:00:02Z"');
        await route.fulfill({ response, body });
    }, { times: 1 });
    await page.route('**/koebenhavn/i-dag?asr=standard&angles=*', async route => {
        const response = await route.fetch();
        const body = (await response.text()).replace('>Næste bøn</h2>', '>Næste bøn opdateret</h2>');
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

test('the next prayer counts down to its start and restarts for the following prayer', async ({ page }) => {
    await page.clock.install({ time: new Date('2026-11-29T11:59:00Z') });
    await page.clock.pauseAt(new Date('2026-11-29T12:00:00Z'));

    async function servePrayer(route, renderedAt, startsAt, name, localTime) {
        const response = await route.fetch();
        const body = (await response.text())
            .replace(/data-today="[^"]+"/, 'data-today="2026-11-29"')
            .replace(/data-rendered-at="[^"]+"/, `data-rendered-at="${renderedAt}"`)
            .replace(/data-refresh-at="[^"]+"/, `data-refresh-at="${startsAt}"`)
            .replace(/data-midnight="[^"]+"/, 'data-midnight="2026-11-29T23:00:00Z"')
            .replace(/<div class="next-prayer" data-next-prayer>[\s\S]+?(?=<p class="today-context">)/, `
                <div class="next-prayer" data-next-prayer>
                    <div><h2 tabindex="-1">Næste bøn</h2>
                        <p class="next-prayer-title" data-next-prayer-label>
                            <span>${name}</span> <time datetime="${startsAt}">${localTime}</time>
                        </p>
                        <p class="next-countdown" data-prayer-countdown role="timer" aria-label="Tid til næste bøn" hidden></p>
                    </div>
                </div>`);
        await route.fulfill({ response, body });
    }

    await page.route('**/koebenhavn', route =>
        servePrayer(route, '2026-11-29T12:00:00Z', '2026-11-29T13:01:02+01:00', 'Dhuhr', '13:01'));
    let refreshes = 0;
    await page.route('**/koebenhavn/i-dag?asr=standard&angles=*', route => {
        refreshes++;
        return servePrayer(route, '2026-11-29T12:01:03Z', '2026-11-29T14:05:03+01:00', 'Asr', '14:05');
    });

    await page.goto('/koebenhavn');
    const countdown = page.getByRole('timer', { name: 'Tid til næste bøn' });
    await expect(countdown).toHaveText('Om 1 min. 2 sek.');
    const name = page.locator('.next-prayer-title span');
    const time = page.locator('.next-prayer-title time');
    await expect(time).toHaveText('13:01');
    await expect(time).toHaveCSS('font-size', await name.evaluate(element => getComputedStyle(element).fontSize));
    await expect(time).toHaveCSS('font-weight', await name.evaluate(element => getComputedStyle(element).fontWeight));
    const [nameBox, timeBox, countdownBox] = await Promise.all([name.boundingBox(), time.boundingBox(), countdown.boundingBox()]);
    expect(timeBox.y).toBe(nameBox.y);
    expect(timeBox.x).toBeGreaterThan(nameBox.x + nameBox.width);
    expect(countdownBox.y).toBeGreaterThan(timeBox.y + timeBox.height);
    await page.clock.fastForward(1000);
    await expect(countdown).toHaveText('Om 1 min. 1 sek.');
    await page.clock.fastForward(61000);
    await expect(countdown).toHaveText('Tiden er inde');
    expect(refreshes).toBe(0);
    await page.clock.fastForward(1000);
    await expect(countdown).toHaveText('Om 1 t. 4 min. 0 sek.');
    await expect(page.locator('[data-prayer-announcement]')).toHaveText('Næste bøn Asr 14:05');
    expect(refreshes).toBe(1);
});

test('a 503 keeps today’s times and retry only accepts the selected Asr method', async ({ page }) => {
    await page.route('**/koebenhavn', async route => {
        const response = await route.fetch();
        const body = (await response.text()).replace(/data-refresh-at="[^"]+"/, 'data-refresh-at="2000-01-01T00:00:00Z"');
        await route.fulfill({ response, body });
    });
    let attempt = 0;
    await page.route('**/koebenhavn/i-dag?asr=standard&angles=*', async route => {
        const response = await route.fetch();
        const body = await response.text();
        attempt++;
        if (attempt === 1) {
            await route.fulfill({ response, status: 503, body: body.replace(/<dl class="prayer-list">[\s\S]*?<\/dl>/, '') });
        } else if (attempt === 2) {
            await route.fulfill({ response, body: body.replace('data-asr-method="standard"', 'data-asr-method="hanafi"') });
        } else {
            await route.fulfill({ response, body });
        }
    });

    await page.goto('/koebenhavn');
    const times = page.locator('.today .prayer-list time');
    const originalTimes = await times.allTextContents();
    expect(originalTimes.length).toBeGreaterThan(0);
    const retry = page.getByRole('link', { name: 'Prøv igen', exact: true });
    await expect(page.getByText(/Du ser de senest hentede tider for i dag/)).toBeVisible();
    await expect(times).toHaveText(originalTimes);
    const mismatch = page.waitForResponse(response => response.url().includes('/i-dag?'));
    await retry.click();
    await mismatch;
    await expect(retry).not.toHaveAttribute('aria-busy');
    await expect(times).toHaveText(originalTimes);
    await expect(retry).toBeVisible();
    await retry.click();
    await expect(retry).toBeHidden();
    await expect(times).toHaveText(originalTimes);
    await expect(page.getByRole('heading', { name: 'Næste bøn' })).toBeVisible();
});

test('at Danish midnight the previous times are historical during a failed refresh', async ({ page }) => {
    await page.clock.install({ time: new Date('2026-11-29T22:59:45Z') });
    await page.route('**/koebenhavn?month=2026-11', async route => {
        const response = await route.fetch();
        const body = (await response.text())
            .replace(/data-today="[^"]+"/, 'data-today="2026-11-29"')
            .replace(/data-rendered-at="[^"]+"/, 'data-rendered-at="2026-11-29T22:59:45Z"')
            .replace(/data-refresh-at="[^"]+"/, 'data-refresh-at="2026-11-29T22:59:46Z"')
            .replace(/data-midnight="[^"]+"/, 'data-midnight="2026-11-29T23:00:00Z"')
            .replace(/(<p class="today-context">[\s\S]*?<time datetime=")[^"]+"[^<]+/,
                (_, prefix) => `${prefix}2026-11-29">søndag 29. november 2026`);
        await route.fulfill({ response, body });
    });
    await page.route('**/koebenhavn/i-dag?asr=standard&angles=*', route => route.fulfill({ status: 503 }));

    await page.goto('/koebenhavn?month=2026-11');
    const dates = page.locator('.today-context time');
    await expect(dates).toHaveAttribute('datetime', '2026-11-29');
    await page.clock.fastForward(3000);
    await expect(page.getByText(/Du ser de senest hentede tider for i dag/)).toBeVisible();
    await page.clock.fastForward(15000);
    const historical = page.getByRole('region', { name: 'Historiske bønnetider' });
    await expect(historical.getByText('Historiske bønnetider', { exact: true })).toBeVisible();
    await expect(historical.locator('.prayer-list')).toBeVisible();
    await expect(historical.getByRole('heading', { name: 'Næste bøn' })).toBeHidden();
    await expect(dates).toHaveAttribute('datetime', '2026-11-29');
    await expect(page.locator('.current-day').first()).toHaveAttribute('data-calendar-date', '2026-11-30');
    await expect(page.getByText('Dagens bønnetider kunne ikke hentes.')).toBeVisible();
});

test('a saved seven-day plan can be reopened without a connection', async ({ page, context, baseURL }) => {
    // WebKit's Playwright offline switch also blocks its service worker. Disconnect the test origin instead.
    const proxy = createServer((incoming, outgoing) => {
        const target = new URL(incoming.url, baseURL);
        const send = target.protocol === 'https:' ? httpsRequest : httpRequest;
        const upstream = send(target, { method: incoming.method, headers: { ...incoming.headers, host: target.host } }, response => {
            outgoing.writeHead(response.statusCode, response.headers);
            response.pipe(outgoing);
        });
        upstream.on('error', () => outgoing.destroy());
        incoming.pipe(upstream);
    });
    proxy.listen(0, '127.0.0.1');
    await once(proxy, 'listening');
    const origin = `http://127.0.0.1:${proxy.address().port}`;

    try {
        await page.goto(`${origin}/aarhus`);
        await page.getByRole('button', { name: 'Vis Hanafi' }).click();
        const angles = page.locator('#fajr-isha');
        await angles.locator('summary').click();
        await angles.getByLabel('Fajr (°)').fill('5,5');
        await angles.getByLabel('Isha (°)').fill('6,5');
        await angles.getByRole('button', { name: 'Gem vinkler' }).click();
        const current = page.locator('.today');
        const now = await current.getAttribute('data-today');
        const asr = await current.locator('.prayer-list > div').filter({ hasText: 'Asr' }).locator('time').textContent();
        const background = await page.locator('body').evaluate(body => getComputedStyle(body).backgroundColor);
        await page.getByRole('button', { name: 'Gem 7 dages tider offline' }).click();
        const saved = page.getByRole('link', { name: 'Åbn gemt bønneplan' });
        await expect(saved).toBeVisible();
        const savedPath = await saved.getAttribute('href');
        proxy.close();
        proxy.closeAllConnections();

        const reopened = await context.newPage();
        await reopened.goto(new URL(savedPath, origin).href);
        await expect(reopened.getByRole('heading', { name: 'Offline bønnetider i Aarhus' })).toBeVisible();
        await expect(reopened.getByText('Asr-metode Hanafi')).toBeVisible();
        await expect(reopened.getByText('Fajr 5,5° · Isha 6,5°')).toBeVisible();
        const days = reopened.locator('.offline-plan article');
        await expect(days).toHaveCount(7);
        await expect(days.first().locator('h2 time')).toHaveAttribute('datetime', now);
        await expect(days.first().locator('.prayer-list > div').filter({ hasText: 'Asr' }).locator('time')).toHaveText(asr);
        const dates = await days.locator('h2 time').evaluateAll(times => times.map(time => time.dateTime));
        expect(dates).toEqual(Array.from({ length: 7 }, (_, index) =>
            new Date(Date.parse(now) + index * 86400000).toISOString().slice(0, 10)));
        await expect(reopened.locator('body')).toHaveCSS('background-color', background);
        await reopened.goto(`${origin}/aarhus`);
        await expect(reopened.getByRole('heading', { name: 'Offline bønnetider i Aarhus' })).toBeVisible();
    } finally {
        if (proxy.listening) {
            proxy.close();
            proxy.closeAllConnections();
        }
    }
});
