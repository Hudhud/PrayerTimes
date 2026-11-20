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

const locationButton = document.querySelector('[data-use-location]');
if (locationButton && navigator.geolocation) {
    const status = document.querySelector('[data-location-status]');
    locationButton.hidden = false;
    locationButton.addEventListener('click', () => {
        locationButton.disabled = true;
        status.hidden = false;
        status.textContent = 'Finder et sted i nærheden…';

        function finish(message) {
            status.textContent = message;
            locationButton.disabled = false;
        }

        navigator.geolocation.getCurrentPosition(async ({ coords }) => {
            try {
                const response = await fetch(locationButton.dataset.catalog);
                if (!response.ok) {
                    throw new Error('Place catalog unavailable');
                }
                const places = await response.json();
                const radians = Math.PI / 180;
                const latitude = coords.latitude * radians;
                const longitude = coords.longitude * radians;
                const distanceFactor = place => {
                    const lat = place.latitude * radians;
                    const lon = place.longitude * radians;
                    return Math.sin((lat - latitude) / 2) ** 2 +
                        Math.cos(latitude) * Math.cos(lat) * Math.sin((lon - longitude) / 2) ** 2;
                };
                let nearest;
                let minimum = Infinity;
                for (const place of places) {
                    if (place.postalCode) {
                        continue;
                    }
                    const distance = distanceFactor(place);
                    if (distance < minimum) {
                        nearest = place;
                        minimum = distance;
                    }
                }

                const thirtyKilometers = Math.sin(30 / (2 * 6371)) ** 2;
                if (!nearest || minimum > thirtyKilometers) {
                    finish('Der blev ikke fundet et dansk sted i nærheden. Søg efter by eller postnummer.');
                    return;
                }

                const link = document.createElement('a');
                link.href = new URL(encodeURIComponent(nearest.id), location.origin).href;
                link.textContent = `Se bønnetider for ${nearest.name}`;
                finish('');
                status.append(link);
            } catch {
                finish('Stederne kunne ikke hentes. Prøv at søge efter by eller postnummer.');
            }
        }, () => finish('Placeringen kunne ikke hentes. Søg efter by eller postnummer.'),
        { maximumAge: 300000, timeout: 10000 });
    });
}
