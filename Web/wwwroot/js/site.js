const printButton = document.querySelector('[data-print]');
if (printButton) {
    printButton.hidden = false;
    printButton.addEventListener('click', () => window.print());
}

let timetable = document.querySelector('[data-refresh-url]');
if (timetable) {
    const announcement = document.querySelector('[data-prayer-announcement]');
    const refreshError = document.querySelector('[data-prayer-refresh-error]');
    const refreshMessage = refreshError.querySelector('[data-prayer-refresh-message]');
    const retry = refreshError.querySelector('[data-prayer-retry]');
    const correctionLink = document.querySelector('[data-correction-link]');
    const danishDate = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Copenhagen', dateStyle: 'short' });
    let refreshAt;
    let timer;
    let midnightTimer;
    let countdownTimer;
    let calendarDate = timetable.dataset.today;
    let updating = false;

    function scheduleRefresh(delay) {
        clearTimeout(timer);
        refreshAt = Date.now() + delay;
        timer = window.setTimeout(() => refreshTimetable(), delay);
    }

    function scheduleFromTimetable() {
        const delay = Date.parse(timetable.dataset.refreshAt) - Date.parse(timetable.dataset.renderedAt);
        scheduleRefresh(Number.isFinite(delay) ? Math.max(1000, delay + 1000) : 60000);

        clearTimeout(midnightTimer);
        const untilMidnight = Date.parse(timetable.dataset.midnight) - Date.now();
        if (untilMidnight > 0) {
            midnightTimer = window.setTimeout(() => refreshTimetable(true), untilMidnight);
        }
    }

    function updateCalendar(date) {
        for (const day of document.querySelectorAll('[data-calendar-date]')) {
            const today = day.dataset.calendarDate === date;
            day.classList.toggle('current-day', today);
            day.querySelector('[data-today-label]').hidden = !today;
            if (today) {
                day.id = day.dataset.todayAnchor;
            } else {
                day.removeAttribute('id');
            }
        }
    }

    function updateCountdown(card) {
        clearTimeout(countdownTimer);
        const countdown = card.querySelector('[data-prayer-countdown]');
        if (!countdown || card.hidden || document.hidden) {
            return;
        }

        const remaining = Date.parse(card.querySelector('time')?.dateTime) - Date.now();
        if (!Number.isFinite(remaining)) {
            countdown.hidden = true;
            return;
        }

        countdown.hidden = false;
        if (remaining <= 0) {
            countdown.textContent = 'Tiden er inde';
            return;
        }

        const seconds = Math.ceil(remaining / 1000);
        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor(seconds / 60) % 60;
        const duration = [hours && `${hours} t.`, (hours || minutes) && `${minutes} min.`, `${seconds % 60} sek.`];
        countdown.textContent = `Om ${duration.filter(Boolean).join(' ')}`;
        countdownTimer = window.setTimeout(() => updateCountdown(card), remaining % 1000 || 1000);
    }

    function updateDisplayedDate() {
        const today = danishDate.format(new Date());
        const hasTimes = Boolean(timetable.querySelector('.prayer-list'));
        const historical = timetable.dataset.today < today;
        timetable.hidden = historical && !hasTimes;
        timetable.querySelector('[data-historical-times]').hidden = !historical;
        const card = timetable.querySelector('[data-next-prayer]');
        card.hidden = historical || (!refreshError.hidden && hasTimes);
        updateCountdown(card);
        timetable.setAttribute('aria-label', historical ? 'Historiske bønnetider' : 'Dagens bønnetider');
        if (calendarDate !== today) {
            updateCalendar(today);
            calendarDate = today;
        }

        if (!refreshError.hidden) {
            const message = historical || !hasTimes
                ? 'Dagens bønnetider kunne ikke hentes.'
                : 'Tiderne kunne ikke opdateres. Du ser de senest hentede tider for i dag.';
            if (refreshMessage.textContent !== message) {
                refreshMessage.textContent = message;
            }
        }
    }

    async function refreshTimetable(force = false) {
        updateDisplayedDate();
        if (updating || document.hidden || (!force && Date.now() < refreshAt)) {
            return;
        }
        // Keep a focused method link intact until the reader moves away.
        if (timetable.contains(document.activeElement) && document.activeElement.matches('a')) {
            scheduleRefresh(1000);
            return;
        }

        updating = true;
        retry.setAttribute('aria-busy', 'true');
        const request = new AbortController();
        const deadline = setTimeout(() => request.abort(), 10000);
        try {
            const response = await fetch(timetable.dataset.refreshUrl, { signal: request.signal });
            if (!response.ok) {
                throw new Error('Timetable unavailable');
            }
            const html = new DOMParser().parseFromString(await response.text(), 'text/html');
            const incoming = html.querySelector('[data-refresh-url]');
            if (incoming?.dataset.currentPlace !== timetable.dataset.currentPlace ||
                incoming.dataset.asrMethod !== timetable.dataset.asrMethod ||
                incoming.dataset.today !== danishDate.format(new Date()) || !incoming.querySelector('.prayer-list')) {
                throw new Error('Timetable unavailable');
            }
            if (timetable.contains(document.activeElement) && document.activeElement.matches('a')) {
                scheduleRefresh(1000);
                return;
            }
            const previousNext = timetable.querySelector('[data-next-prayer]');
            const next = incoming.querySelector('[data-next-prayer]');
            const nextLabel = next.querySelector('[data-next-prayer-label]').textContent.trim();
            const nextTime = next.querySelector('time');
            if (nextLabel !== previousNext.querySelector('[data-next-prayer-label]').textContent.trim() ||
                nextTime?.dateTime !== previousNext.querySelector('time')?.dateTime) {
                next.classList.add('prayer-updated');
                announcement.textContent = [next.querySelector('h2').textContent, nextLabel, nextTime?.textContent]
                    .filter(Boolean).join(' ').replace(/\s+/g, ' ');
            }
            if (correctionLink && incoming.dataset.correctionUrl) {
                correctionLink.href = incoming.dataset.correctionUrl;
            }

            timetable.replaceWith(incoming);
            timetable = incoming;
            if (document.activeElement === retry) {
                next.querySelector('h2').focus();
            }
            refreshError.hidden = true;
            updateDisplayedDate();
            scheduleFromTimetable();
        } catch {
            refreshError.hidden = false;
            updateDisplayedDate();
            scheduleRefresh(60000);
        } finally {
            clearTimeout(deadline);
            retry.removeAttribute('aria-busy');
            updating = false;
        }
    }

    retry.addEventListener('click', event => {
        event.preventDefault();
        refreshTimetable(true);
    });
    document.querySelector('[data-open-calendar-today]')?.addEventListener('click', event => {
        const day = document.getElementById(event.currentTarget.hash.slice(1));
        if (day) {
            day.open = true;
        }
    });
    document.addEventListener('visibilitychange', () => refreshTimetable());
    window.addEventListener('pageshow', () => refreshTimetable());
    window.addEventListener('online', () => {
        if (!refreshError.hidden) {
            refreshTimetable(true);
        }
    });
    updateDisplayedDate();
    scheduleFromTimetable();
}

const offline = document.querySelector('[data-offline-controls]');
if (offline && 'serviceWorker' in navigator && 'caches' in window) {
    const save = offline.querySelector('[data-save-offline]');
    const open = offline.querySelector('[data-open-offline]');
    const status = offline.querySelector('[data-offline-status]');
    const cacheName = 'salahtimes-offline-plan';
    const worker = new URL(offline.dataset.worker, location.href);
    offline.hidden = false;

    caches.match(open.href, { cacheName }).then(plan => { open.hidden = !plan; }).catch(() => {});
    save.addEventListener('click', async () => {
        save.disabled = true;
        status.textContent = 'Gemmer bønneplanen…';
        try {
            const response = await fetch(offline.dataset.offlinePlan);
            if (!response.ok || response.redirected) {
                throw new Error('Offline plan unavailable');
            }
            await navigator.serviceWorker.register(worker);
            await navigator.serviceWorker.ready;
            const cache = await caches.open(cacheName);
            await cache.addAll(['css/site.css', 'icons/crescent.svg', 'icons/favicon.svg'].map(path => new URL(path, worker).href));
            await cache.put(open.href, response);
            open.hidden = false;
            status.textContent = 'Bønneplanen er gemt i denne browser.';
        } catch {
            status.textContent = 'Bønneplanen kunne ikke gemmes. Prøv igen, når du er online.';
        } finally {
            save.disabled = false;
        }
    });
}
