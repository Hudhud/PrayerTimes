const printButton = document.querySelector('[data-print]');
if (printButton) {
    printButton.hidden = false;
    printButton.addEventListener('click', () => window.print());
}

const timetable = document.querySelector('[data-refresh-at]');
if (timetable) {
    const refreshDelay = Date.parse(timetable.dataset.refreshAt) - Date.parse(timetable.dataset.renderedAt);
    const refreshAt = Date.now() + refreshDelay + 1000;
    function refreshIfCurrentView() {
        if (document.visibilityState === 'visible' && Date.now() >= refreshAt &&
            !document.activeElement?.matches('#place-query')) {
            window.location.reload();
        }
    }

    if (refreshDelay >= 0) {
        window.setTimeout(refreshIfCurrentView, refreshAt - Date.now());
        document.addEventListener('visibilitychange', refreshIfCurrentView);
        const searchInput = document.querySelector('#place-query');
        searchInput?.addEventListener('blur', () => {
            // Let a click on a search control finish before refreshing the timetable.
            window.setTimeout(refreshIfCurrentView, 1000);
        });
    }
}
