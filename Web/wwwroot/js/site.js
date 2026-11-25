const english = document.documentElement.lang === 'en';
const text = (da, en) => english ? en : da;

const printButton = document.querySelector('[data-print]');
if (printButton) {
    printButton.hidden = false;
    printButton.addEventListener('click', () => window.print());
}

const calendarHeader = document.querySelector('.calendar-header');
if (calendarHeader) {
    const calendar = calendarHeader.parentElement;
    const selectionId = 'calendar-selection';
    const updateHeaderHeight = () => {
        calendar.style.setProperty('--calendar-header-height', `${calendarHeader.offsetHeight}px`);
    };

    calendar.querySelector('.month-days')?.addEventListener('click', event => {
        const day = event.target.closest('summary')?.parentElement;
        if (day) {
            requestAnimationFrame(() => {
                if (day.open) {
                    day.scrollIntoView({ block: 'start' });
                }
            });
        }
    });

    if (location.hash === `#${selectionId}`) {
        const date = new URLSearchParams(location.search).get('week');
        const day = Array.from(calendar.querySelectorAll('[data-calendar-date]')).find(row =>
            row.dataset.calendarDate === date && row.getClientRects().length);
        if (day) {
            const summary = day.querySelector('summary');
            const anchor = summary ?? day.querySelector('th');
            // Point native fragment scrolling at the same day in either layout.
            anchor.id = selectionId;
            window.addEventListener('pageshow', event => {
                if (!event.persisted) {
                    (summary ?? day).focus({ preventScroll: true });
                    anchor.scrollIntoView({ block: 'start' });
                }
            });
        }
    }

    const picker = calendar.querySelector('#calendar-date-picker');
    if (typeof picker.showModal === 'function') {
        const trigger = calendar.querySelector('.calendar-date-trigger');
        const form = picker.querySelector('form');
        const { month, year, day } = form.elements;
        const monthParameter = () => `${String(year.valueAsNumber).padStart(4, '0')}-${month.value.padStart(2, '0')}`;
        const updateDays = () => {
            const lastDay = new Date(0);
            lastDay.setUTCFullYear(year.valueAsNumber, Number(month.value), 0);
            const dayCount = lastDay.getUTCDate();
            for (const option of day.options) {
                option.hidden = option.disabled = Number(option.value) > dayCount;
            }
            if (Number(day.value) > dayCount) {
                day.value = dayCount;
            }
            month.setCustomValidity(monthParameter() >= picker.dataset.monthLimit ? text("Vælg en tidligere måned.", "Choose an earlier month.") : '');
        };

        trigger.hidden = false;
        calendar.querySelector('.calendar-month-text').hidden = true;
        trigger.addEventListener('click', () => {
            form.reset();
            updateDays();
            picker.showModal();
        });
        month.addEventListener('change', updateDays);
        year.addEventListener('input', updateDays);
        form.addEventListener('submit', event => {
            if (event.submitter?.formMethod === 'dialog') {
                return;
            }
            event.preventDefault();
            const url = new URL(form.action);
            const selectedMonth = monthParameter();
            url.searchParams.set('month', selectedMonth);
            url.searchParams.set('week', `${selectedMonth}-${day.value.padStart(2, '0')}`);
            url.hash = selectionId;
            location.assign(url.href);
        });
    }

    updateHeaderHeight();
    if ('ResizeObserver' in window) {
        new ResizeObserver(updateHeaderHeight).observe(calendarHeader);
    }
}

for (const link of document.querySelectorAll('[data-open-calendar-today]')) {
    link.addEventListener('click', () => {
        const day = document.getElementById(link.hash.slice(1));
        if (day) {
            day.open = true;
        } else if (link.getAttribute('href') === '#week-today') {
            link.href = calendarHeader.querySelector('[data-open-calendar-today]').href;
        }
    });
}

let timetable = document.querySelector('[data-refresh-url]');
if (timetable) {
    const announcement = document.querySelector('[data-prayer-announcement]');
    const refreshError = document.querySelector('[data-prayer-refresh-error]');
    const refreshMessage = refreshError.querySelector('[data-prayer-refresh-message]');
    const retry = refreshError.querySelector('[data-prayer-retry]');
    const correctionLink = document.querySelector('[data-correction-link]');
    let localDate;
    try {
        localDate = new Intl.DateTimeFormat('sv-SE', {
            timeZone: timetable.dataset.timeZone ?? 'Europe/Copenhagen', dateStyle: 'short'
        });
    } catch (error) {
        if (!(error instanceof RangeError)) {
            throw error;
        }
    }
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
            countdown.textContent = text("Tiden er inde", "It’s time");
            return;
        }

        const seconds = Math.ceil(remaining / 1000);
        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor(seconds / 60) % 60;
        const duration = [hours && `${hours} ${text("t.", "hr")}`,
            (hours || minutes) && `${minutes} ${text("min.", "min")}`, `${seconds % 60} ${text("sek.", "sec")}`];
        countdown.textContent = `${text('Om', 'In')} ${duration.filter(Boolean).join(' ')}`;
        countdownTimer = window.setTimeout(() => updateCountdown(card), remaining % 1000 || 1000);
    }

    function updateDisplayedDate() {
        const today = localDate?.format(new Date()) ?? timetable.dataset.today;
        const hasTimes = Boolean(timetable.querySelector('.prayer-list'));
        const historical = timetable.dataset.today < today;
        timetable.hidden = historical && !hasTimes;
        timetable.toggleAttribute('data-stale', historical || !refreshError.hidden);
        timetable.querySelector('[data-historical-times]').hidden = !historical;
        const card = timetable.querySelector('[data-next-prayer]');
        card.hidden = historical || (!refreshError.hidden && hasTimes);
        updateCountdown(card);
        timetable.setAttribute('aria-label', historical ? text("Historiske bønnetider", "Historical prayer times") : text("Dagens bønnetider", "Today’s prayer times"));
        if (calendarDate !== today) {
            updateCalendar(today);
            calendarDate = today;
        }

        if (!refreshError.hidden) {
            const message = historical || !hasTimes
                ? text("Dagens bønnetider kunne ikke hentes.", "Today’s prayer times could not be loaded.")
                : text("Tiderne kunne ikke opdateres. Du ser de senest hentede tider for i dag.", "The times could not be refreshed. Showing the last available times for today.");
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
                incoming.dataset.asrMethod !== timetable.dataset.asrMethod || incoming.dataset.angles !== timetable.dataset.angles ||
                incoming.dataset.timeZone !== timetable.dataset.timeZone ||
                (localDate && incoming.dataset.today !== localDate.format(new Date())) || !incoming.querySelector('.prayer-list')) {
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
                announcement.textContent = `${next.querySelector('h2').textContent} ${nextLabel}`.replace(/\s+/g, ' ');
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
        status.textContent = text("Gemmer bønneplanen…", "Saving the timetable…");
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
            status.textContent = text("Bønneplanen er gemt i denne browser.", "The timetable has been saved in this browser.");
        } catch {
            status.textContent = text("Bønneplanen kunne ikke gemmes. Prøv igen, når du er online.", "The timetable could not be saved. Try again when you’re online.");
        } finally {
            save.disabled = false;
        }
    });
}
