(() => {
    const picker = document.querySelector('[data-place-picker]');
    if (!picker) {
        return;
    }

    const form = picker.querySelector('form');
    const input = form.querySelector('input');
    const list = form.querySelector('[role="listbox"]');
    const spellingHeading = form.querySelector('[data-spelling-heading]');
    const searchStatus = form.querySelector('[role="status"]');
    const locationButton = picker.querySelector('[data-use-location]');
    const locationStatus = picker.querySelector('[data-location-status]');
    const toggle = document.querySelector('[data-place-toggle]');
    const panel = toggle ? document.getElementById(toggle.getAttribute('aria-controls')) : null;
    const currentPlace = document.querySelector('[data-current-place]')?.dataset;
    const recentKey = 'salahtimes-recent-places';
    const recentPlaces = rememberPlace();
    const locationKey = 'salahtimes-auto-location';
    const locationMaxAge = 5 * 60 * 1000;
    let locationPreference = readLocationPreference();
    let lastLocationCheck = locationPreference?.checkedAt ?? 0;
    let locationRequest;
    let searchRequest;
    let searchTimer;
    let suggestions = [];
    let activeIndex = -1;

    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-controls', list.id);
    input.setAttribute('aria-expanded', 'false');

    function closeSuggestions() {
        clearTimeout(searchTimer);
        searchTimer = undefined;
        searchRequest?.abort();
        searchRequest = undefined;
        suggestions = [];
        list.replaceChildren();
        list.hidden = true;
        spellingHeading.hidden = true;
        activeIndex = -1;
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
        searchStatus.textContent = '';
        searchStatus.classList.add('visually-hidden');
    }

    function showSearchMessage(message) {
        searchStatus.classList.remove('visually-hidden');
        searchStatus.textContent = message;
    }

    async function loadSuggestions(query) {
        searchTimer = undefined;
        const request = new AbortController();
        searchRequest = request;
        const waitingTimer = window.setTimeout(() => {
            if (searchRequest === request && !request.signal.aborted && document.activeElement === input) {
                showSearchMessage('Søger efter steder…');
            }
        }, 350);
        try {
            const url = new URL(form.dataset.suggestions, location.href);
            url.searchParams.set('q', query);
            const response = await fetch(url, { signal: request.signal });
            if (!response.ok) {
                throw new Error('Place suggestions unavailable');
            }

            const results = await response.json();
            if (request.signal.aborted || document.activeElement !== input) {
                return;
            }

            if (results.length === 0) {
                showSearchMessage('Ingen steder fundet. Prøv et andet bynavn eller postnummer.');
            } else {
                const spelling = results[0].isSpellingSuggestion;
                renderSuggestions(results, spelling ? 'Mente du?' : 'Stedforslag');
                spellingHeading.hidden = !spelling;
            }
        } catch {
            if (!request.signal.aborted && document.activeElement === input) {
                showSearchMessage('Forslag kunne ikke hentes. Tryk Søg for at prøve igen.');
            }
        } finally {
            clearTimeout(waitingTimer);
            if (searchRequest === request) {
                searchRequest = undefined;
            }
        }
    }

    function rememberPlace() {
        try {
            const saved = JSON.parse(localStorage.getItem(recentKey)) ?? [];
            const places = saved.filter(place => typeof place?.id === 'string' && /^[a-z0-9-]+$/.test(place.id) &&
                typeof place.label === 'string' && place.label.length <= 100);
            if (currentPlace) {
                const id = currentPlace.currentPlace;
                const previous = places.filter(place => place.id !== id);
                localStorage.setItem(recentKey, JSON.stringify([{ id, label: currentPlace.placeName }, ...previous].slice(0, 4)));
                return previous.slice(0, 3);
            }
            return places.slice(0, 3);
        } catch {
            return [];
        }
    }

    function renderSuggestions(results, label) {
        suggestions = results;
        searchStatus.classList.add('visually-hidden');
        list.setAttribute('aria-label', label);
        results.forEach((place, index) => {
            const option = document.createElement('li');
            option.id = `place-option-${index}`;
            option.setAttribute('role', 'option');
            option.setAttribute('aria-selected', 'false');
            option.textContent = place.label;
            if (place.area) {
                const area = document.createElement('small');
                area.textContent = place.area;
                option.append(area);
            }
            option.addEventListener('click', () => selectPlace(place));
            list.append(option);
        });
        list.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        searchStatus.textContent = label === 'Stedforslag'
            ? (results.length === 1 ? '1 forslag' : `${results.length} forslag`)
            : label;
    }

    function queueSuggestions() {
        closeSuggestions();
        const query = input.value.trim();
        if (query.length >= 2) {
            searchTimer = window.setTimeout(() => loadSuggestions(query), 200);
        } else if (query.length === 0 && recentPlaces.length > 0) {
            const base = new URL(locationButton.dataset.placeBase, location.href);
            renderSuggestions(recentPlaces.map(place => ({
                label: place.label, url: new URL(encodeURIComponent(place.id), base).href
            })), 'Senest brugte steder');
        }
    }

    function selectPlace(place) {
        stopLocation();
        window.location.assign(place.url);
    }

    input.addEventListener('input', () => {
        cancelLocationRequest();
        queueSuggestions();
    });
    input.addEventListener('focus', queueSuggestions);
    input.addEventListener('blur', closeSuggestions);
    list.addEventListener('mousedown', event => event.preventDefault());
    input.addEventListener('keydown', event => {
        if (event.isComposing) {
            return;
        }
        if (event.key === 'Escape') {
            if (searchTimer || searchRequest || !list.hidden || searchStatus.textContent) {
                event.preventDefault();
                closeSuggestions();
            }
            return;
        }
        if (list.hidden) {
            return;
        }
        if (event.key === 'Enter' && activeIndex >= 0) {
            event.preventDefault();
            selectPlace(suggestions[activeIndex]);
        } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            list.children[activeIndex]?.setAttribute('aria-selected', 'false');
            const step = event.key === 'ArrowDown' ? 1 : -1;
            activeIndex = activeIndex < 0 && step < 0 ? suggestions.length - 1
                : (activeIndex + step + suggestions.length) % suggestions.length;
            const option = list.children[activeIndex];
            option.setAttribute('aria-selected', 'true');
            input.setAttribute('aria-activedescendant', option.id);
            option.scrollIntoView({ block: 'nearest' });
        }
    });
    form.addEventListener('submit', stopLocation);
    document.querySelectorAll('[data-place-result]').forEach(link => link.addEventListener('click', stopLocation));

    function readLocationPreference() {
        try {
            const preference = JSON.parse(localStorage.getItem(locationKey));
            return typeof preference?.placeId === 'string' && Number.isFinite(preference.checkedAt)
                ? preference : null;
        } catch {
            return null;
        }
    }

    function saveLocationPreference(preference) {
        locationPreference = preference;
        try {
            if (preference) {
                localStorage.setItem(locationKey, JSON.stringify(preference));
            } else {
                localStorage.removeItem(locationKey);
            }
        } catch {
            // Location navigation still works if browser storage is unavailable.
        }
    }

    function updateLocationButton() {
        locationButton.textContent = currentPlace && locationPreference?.placeId === currentPlace.currentPlace
            ? 'Stop automatisk placering' : 'Brug min placering';
    }

    function cancelLocationRequest() {
        if (locationRequest) {
            locationRequest.abort();
            locationRequest = undefined;
            locationButton.disabled = false;
            locationButton.removeAttribute('aria-busy');
            locationStatus.textContent = '';
        }
    }

    function stopLocation() {
        cancelLocationRequest();
        saveLocationPreference(null);
        updateLocationButton();
    }

    function findNearestPlace(places, coords) {
        const radians = Math.PI / 180;
        const latitude = coords.latitude * radians;
        const longitude = coords.longitude * radians;
        let nearest;
        let minimum = Math.sin(30 / (2 * 6371)) ** 2;
        for (const place of places) {
            if (place.postalCode) {
                continue;
            }
            const lat = place.latitude * radians;
            const lon = place.longitude * radians;
            const distance = Math.sin((lat - latitude) / 2) ** 2 +
                Math.cos(latitude) * Math.cos(lat) * Math.sin((lon - longitude) / 2) ** 2;
            if (distance < minimum) {
                nearest = place;
                minimum = distance;
            }
        }
        return nearest;
    }

    async function useLocation(automatic = false) {
        const request = new AbortController();
        locationRequest = request;
        lastLocationCheck = Date.now();
        if (!automatic) {
            locationButton.disabled = true;
            locationButton.setAttribute('aria-busy', 'true');
            locationStatus.textContent = 'Finder et sted i nærheden…';
        }

        try {
            const { coords } = await new Promise((resolve, reject) => navigator.geolocation.getCurrentPosition(
                resolve, reject, { maximumAge: automatic ? locationMaxAge : 0, timeout: 10000 }));
            if (request.signal.aborted) {
                return;
            }
            const response = await fetch(locationButton.dataset.catalog, { signal: request.signal });
            if (!response.ok) {
                throw new Error('Place catalog unavailable');
            }
            const nearest = findNearestPlace(await response.json(), coords);
            if (request.signal.aborted ||
                (automatic && (document.hidden || document.activeElement === input))) {
                return;
            }
            if (!nearest) {
                locationStatus.textContent = 'Der blev ikke fundet et dansk sted i nærheden. Søg efter by eller postnummer.';
                return;
            }

            locationStatus.textContent = '';
            saveLocationPreference({ placeId: nearest.id, checkedAt: Date.now() });
            if (currentPlace?.currentPlace === nearest.id) {
                updateLocationButton();
                if (!automatic) {
                    locationStatus.textContent = `Du ser allerede bønnetiderne for ${nearest.name}.`;
                }
                return;
            }

            const target = new URL(encodeURIComponent(nearest.id), new URL(locationButton.dataset.placeBase, location.href));
            if (automatic) {
                window.location.replace(target);
            } else {
                window.location.assign(target);
            }
        } catch (error) {
            if (request.signal.aborted) {
                return;
            }
            if (error.code === 1 && automatic) {
                stopLocation();
            } else if (!automatic) {
                locationStatus.textContent = error.code === 1
                    ? 'Browseren giver ikke adgang til din placering. Søg efter by eller postnummer.'
                    : 'Placeringen kunne ikke bruges. Prøv igen, eller søg efter by eller postnummer.';
            }
        } finally {
            if (locationRequest === request) {
                locationRequest = undefined;
                locationButton.disabled = false;
                locationButton.removeAttribute('aria-busy');
            }
        }
    }

    function canRefreshLocation() {
        return currentPlace && locationPreference?.placeId === currentPlace.currentPlace && !locationRequest &&
            !document.hidden && (!panel || panel.hidden) && document.activeElement !== input;
    }

    async function refreshLocation() {
        if (!canRefreshLocation()) {
            return;
        }
        try {
            const permission = await navigator.permissions?.query({ name: 'geolocation' });
            if (!canRefreshLocation()) {
                return;
            }
            if (permission?.state === 'denied' || permission?.state === 'prompt') {
                stopLocation();
            } else if (permission?.state === 'granted' && Date.now() - lastLocationCheck >= locationMaxAge) {
                await useLocation(true);
            }
        } catch {
            // Some browsers do not support querying geolocation permission.
        }
    }

    if (panel) {
        // Keep location updates audible even when the search controls are collapsed.
        panel.after(locationStatus);
        toggle.addEventListener('click', event => {
            if (panel.hidden) {
                cancelLocationRequest();
                toggle.textContent = 'Luk stedvælger';
                toggle.setAttribute('aria-expanded', 'true');
                panel.hidden = false;
                if (event.detail === 0 || !window.matchMedia('(pointer: coarse)').matches) {
                    input.focus();
                }
            } else {
                closePicker();
            }
        });
        panel.parentElement.addEventListener('keydown', event => {
            if (!panel.hidden && event.key === 'Escape' && !event.defaultPrevented && !event.isComposing) {
                event.preventDefault();
                closePicker();
            }
        });

        function closePicker() {
            closeSuggestions();
            cancelLocationRequest();
            locationStatus.textContent = '';
            panel.hidden = true;
            toggle.textContent = 'Skift sted';
            toggle.setAttribute('aria-expanded', 'false');
            toggle.focus();
        }

        document.querySelector('[data-place-fallback]').hidden = true;
        toggle.hidden = false;
    }

    if (window.isSecureContext && navigator.geolocation) {
        locationButton.hidden = false;
        updateLocationButton();
        locationButton.addEventListener('click', () => {
            if (currentPlace && locationPreference?.placeId === currentPlace.currentPlace) {
                stopLocation();
                locationStatus.textContent = 'Automatisk placering er slået fra.';
            } else {
                useLocation();
            }
        });
        window.addEventListener('storage', event => {
            if (event.key === locationKey) {
                cancelLocationRequest();
                locationPreference = readLocationPreference();
                lastLocationCheck = locationPreference?.checkedAt ?? 0;
                updateLocationButton();
            }
        });
        window.addEventListener('pageshow', refreshLocation);
        document.addEventListener('visibilitychange', refreshLocation);
        refreshLocation();
    }
})();
