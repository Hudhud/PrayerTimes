const printButton = document.querySelector('[data-print]');
if (printButton) {
    printButton.hidden = false;
    printButton.addEventListener('click', () => window.print());
}

let timetable = document.querySelector('[data-refresh-url]');
if (timetable) {
    const announcement = document.querySelector('[data-prayer-announcement]');
    const refreshError = document.querySelector('[data-prayer-refresh-error]');
    const correctionLink = document.querySelector('[data-correction-link]');
    let refreshAt;
    let timer;
    let updating = false;

    function scheduleRefresh(delay) {
        clearTimeout(timer);
        refreshAt = Date.now() + delay;
        timer = window.setTimeout(refreshTimetable, delay);
    }

    function scheduleFromTimetable() {
        const delay = Date.parse(timetable.dataset.refreshAt) - Date.parse(timetable.dataset.renderedAt);
        scheduleRefresh(Number.isFinite(delay) ? Math.max(1000, delay + 1000) : 60000);
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

    async function refreshTimetable() {
        if (updating || document.hidden || Date.now() < refreshAt) {
            return;
        }
        // Keep a focused method link intact until the reader moves away.
        if (timetable.contains(document.activeElement)) {
            scheduleRefresh(1000);
            return;
        }

        updating = true;
        try {
            const response = await fetch(timetable.dataset.refreshUrl);
            const html = new DOMParser().parseFromString(await response.text(), 'text/html');
            const incoming = html.querySelector('[data-refresh-url]');
            if ((!response.ok && response.status !== 503) || incoming?.dataset.currentPlace !== timetable.dataset.currentPlace) {
                throw new Error('Timetable unavailable');
            }
            if (timetable.contains(document.activeElement)) {
                scheduleRefresh(1000);
                return;
            }
            const previousNext = timetable.querySelector('[data-next-prayer]');
            const next = incoming.querySelector('[data-next-prayer]');
            if (next.textContent.trim() !== previousNext.textContent.trim() ||
                next.querySelector('time')?.dateTime !== previousNext.querySelector('time')?.dateTime) {
                next.classList.add('prayer-updated');
                announcement.textContent = next.textContent.trim().replace(/\s+/g, ' ');
            }
            if (incoming.dataset.today !== timetable.dataset.today) {
                updateCalendar(incoming.dataset.today);
            }
            if (correctionLink && incoming.dataset.correctionUrl) {
                correctionLink.href = incoming.dataset.correctionUrl;
            }

            timetable.replaceWith(incoming);
            timetable = incoming;
            refreshError.hidden = true;
            scheduleFromTimetable();
        } catch {
            refreshError.hidden = false;
            scheduleRefresh(60000);
        } finally {
            updating = false;
        }
    }

    document.addEventListener('visibilitychange', refreshTimetable);
    window.addEventListener('pageshow', refreshTimetable);
    scheduleFromTimetable();
}
