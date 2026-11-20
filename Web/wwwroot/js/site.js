const printButton = document.querySelector('[data-print]');
if (printButton) {
    printButton.hidden = false;
    printButton.addEventListener('click', () => window.print());
}

const timetable = document.querySelector('[data-refresh-at]');
if (timetable) {
    const refreshAt = Date.parse(timetable.dataset.refreshAt);
    function refreshIfCurrentView() {
        if (document.visibilityState === 'visible' && Date.now() >= refreshAt) {
            window.location.reload();
        }
    }

    if (refreshAt > Date.now()) {
        window.setTimeout(refreshIfCurrentView, refreshAt - Date.now() + 1000);
        document.addEventListener('visibilitychange', refreshIfCurrentView);
    }
}
