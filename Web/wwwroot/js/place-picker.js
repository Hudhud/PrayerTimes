(() => {
    const english = document.documentElement.lang === 'en';
    const text = (da, en) => english ? en : da;
    const picker = document.querySelector('[data-place-picker]');
    if (!picker) {
        return;
    }

    const form = picker.querySelector('form');
    const input = form.querySelector('input[type=search]');
    const list = form.querySelector('[role="listbox"]');
    const spellingHeading = form.querySelector('[data-spelling-heading]');
    const searchStatus = form.querySelector('[role="status"]');
    const locationButton = picker.querySelector('[data-use-location]');
    const returnToSettings = locationButton.dataset.returnToSettings === 'true';
    const locationFeedback = picker.querySelector('[data-location-feedback]');
    const locationStatus = picker.querySelector('[data-location-status]');
    const locationSuggestion = picker.querySelector('[data-location-suggestion]');
    const automaticPlace = document.querySelector('[data-auto-place]');
    const defaultPlace = document.querySelector('[data-default-place]');
    const locationShortcut = document.querySelector('[data-location-shortcut]');
    const toggle = document.querySelector('[data-place-toggle]');
    const panel = toggle ? document.getElementById(toggle.getAttribute('aria-controls')) : null;
    const currentPlace = document.querySelector('[data-current-place]')?.dataset;
    const recentKey = 'salahtimes-recent-places';
    const recentPlaces = rememberPlace();
    const locationKey = 'salahtimes-auto-location';
    const locationMaxAge = 5 * 60 * 1000;
    const maxAutomaticAccuracy = 1000; // Geolocation reports accuracy in metres.
    let locationPreference = readLocationPreference();
    let lastLocationCheck = locationPreference?.checkedAt ?? 0;
    let locationRequest;
    let suggestedPlace;
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
                showSearchMessage(text("Søger efter steder…", "Searching for places…"));
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
                showSearchMessage(text("Ingen steder fundet. Prøv et andet bynavn eller postnummer.", "No places found. Try another city or a Danish postcode."));
            } else {
                const spelling = results[0].isSpellingSuggestion;
                renderSuggestions(results, spelling ? 'Mente du?' : text("Stedforslag", "Suggested places"));
                spellingHeading.hidden = !spelling;
            }
        } catch {
            if (!request.signal.aborted && document.activeElement === input) {
                showSearchMessage(text("Forslag kunne ikke hentes. Tryk Søg for at prøve igen.", "Suggestions could not be loaded. Select Search to try again."));
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
        searchStatus.textContent = label === text("Stedforslag", "Suggested places")
            ? (results.length === 1 ? text("1 forslag", "1 suggestion") : text(`${results.length} forslag`, `${results.length} suggestions`))
            : label;
    }

    function queueSuggestions() {
        closeSuggestions();
        const query = input.value.trim();
        if (query.length >= 2) {
            searchTimer = window.setTimeout(() => loadSuggestions(query), 200);
        } else if (query.length === 0 && recentPlaces.length > 0) {
            renderSuggestions(recentPlaces.map(place => ({
                label: place.label, url: placeUrl(place.id).href
            })), text("Senest brugte steder", "Recent places"));
        }
    }

    function placeUrl(id) {
        const base = new URL(locationButton.dataset.placeBase, location.href);
        const url = new URL(encodeURIComponent(id), base);
        if (returnToSettings) {
            url.searchParams.set('settings', 'true');
        }
        return url;
    }

    function selectPlace(place) {
        stopLocation();
        window.location.assign(place.url);
    }

    input.addEventListener('input', () => {
        cancelLocationRequest();
        clearLocationFeedback();
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
        const automatic = currentPlace && locationPreference?.placeId === currentPlace.currentPlace;
        locationButton.textContent = automatic ? text("Stop automatisk placering", "Stop automatic location") : text("Brug min placering", "Use my location");
        if (automaticPlace) {
            automaticPlace.hidden = !automatic;
        }
        if (defaultPlace) {
            defaultPlace.hidden = automatic;
            locationShortcut.hidden = automatic || !panel.hidden;
        }
    }

    function clearLocationFeedback() {
        suggestedPlace = undefined;
        locationSuggestion.hidden = true;
        locationStatus.textContent = '';
    }

    function cancelLocationRequest() {
        if (locationRequest) {
            locationRequest.abort();
            locationRequest = undefined;
            locationButton.disabled = false;
            locationButton.removeAttribute('aria-busy');
            clearLocationFeedback();
        }
    }

    function stopLocation() {
        cancelLocationRequest();
        clearLocationFeedback();
        saveLocationPreference(null);
        updateLocationButton();
    }

    function findNearestPlace(catalogs, coords) {
        const radians = Math.PI / 180;
        const latitude = coords.latitude * radians;
        const longitude = coords.longitude * radians;
        const scale = Math.cos(latitude);
        const danishLimit = Math.sin(30 / (2 * 6371)) ** 2;
        let nearest;
        let minimum = Math.sin(50 / (2 * 6371)) ** 2;
        for (const places of catalogs) {
            for (const place of places) {
                if (place.postalCode) {
                    continue;
                }
                const lat = place.latitude * radians;
                const lon = place.longitude * radians;
                const distance = Math.sin((lat - latitude) / 2) ** 2 +
                    scale * Math.cos(lat) * Math.sin((lon - longitude) / 2) ** 2;
                if (distance < minimum && (place.countryCode || distance < danishLimit)) {
                    nearest = place;
                    minimum = distance;
                }
            }
        }
        return nearest;
    }

    async function useLocation(automatic = false) {
        clearLocationFeedback();
        const request = new AbortController();
        locationRequest = request;
        lastLocationCheck = Date.now();
        if (!automatic) {
            locationButton.disabled = true;
            locationButton.setAttribute('aria-busy', 'true');
            locationStatus.textContent = text("Finder et sted i nærheden…", "Finding a nearby place…");
        }

        try {
            const { coords } = await new Promise((resolve, reject) => navigator.geolocation.getCurrentPosition(
                resolve, reject, { enableHighAccuracy: !automatic, maximumAge: automatic ? locationMaxAge : 0, timeout: 10000 }));
            if (request.signal.aborted) {
                return;
            }
            const responses = await Promise.all([locationButton.dataset.catalog, locationButton.dataset.worldCatalog]
                .map(url => fetch(url, { signal: request.signal })));
            if (!responses[0].ok) {
                throw new Error('Place catalog unavailable');
            }
            const catalogs = await Promise.all(responses.filter(response => response.ok).map(response => response.json()));
            const nearest = findNearestPlace(catalogs, coords);
            if (request.signal.aborted ||
                (automatic && (document.hidden || (panel && !panel.hidden) || document.activeElement === input))) {
                return;
            }
            if (!nearest) {
                locationStatus.textContent = text("Der blev ikke fundet et sted i nærheden. Søg efter by eller postnummer.", "No nearby city was found. Search by city or Danish postcode.");
                return;
            }

            const target = placeUrl(nearest.id);
            if (!Number.isFinite(coords.accuracy) || coords.accuracy <= 0 || coords.accuracy > maxAutomaticAccuracy) {
                if (automatic) {
                    saveLocationPreference({ placeId: currentPlace.currentPlace, checkedAt: Date.now() });
                }
                if (currentPlace?.currentPlace === nearest.id) {
                    locationStatus.textContent = text(`Placeringen er omtrentlig. Du ser allerede bønnetiderne for ${nearest.name}.`,
                        `Your location is approximate. You are already viewing prayer times for ${nearest.name}.`);
                } else {
                    suggestedPlace = { url: target };
                    locationStatus.textContent = text("Placeringen er omtrentlig. Bekræft det foreslåede sted.", "Your location is approximate. Confirm the suggested place.");
                    locationSuggestion.textContent = text(`Vælg ${nearest.name}`, `Choose ${nearest.name}`);
                    locationSuggestion.hidden = false;
                }
                return;
            }

            locationStatus.textContent = '';
            saveLocationPreference({ placeId: nearest.id, checkedAt: Date.now() });
            if (currentPlace?.currentPlace === nearest.id && (!defaultPlace || automatic)) {
                updateLocationButton();
                if (!automatic) {
                    locationStatus.textContent = text(`Du ser allerede bønnetiderne for ${nearest.name}.`, `You are already viewing prayer times for ${nearest.name}.`);
                }
                return;
            }

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
                    ? text("Browseren giver ikke adgang til din placering. Søg efter by eller postnummer.", "Your browser does not allow location access. Search by city or Danish postcode.")
                    : text("Placeringen kunne ikke bruges. Prøv igen, eller søg efter by eller postnummer.", "Your location could not be used. Try again or search by city or Danish postcode.");
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
        return !returnToSettings && currentPlace && locationPreference?.placeId === currentPlace.currentPlace && !locationRequest &&
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
        panel.after(locationFeedback);
        toggle.addEventListener('click', event => {
            if (panel.hidden) {
                openPicker();
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

        function openPicker() {
            cancelLocationRequest();
            panel.hidden = false;
            toggle.textContent = text("Luk stedvælger", "Close location picker");
            toggle.setAttribute('aria-expanded', 'true');
            if (locationShortcut) {
                locationShortcut.hidden = true;
            }
        }

        function closePicker() {
            closeSuggestions();
            cancelLocationRequest();
            panel.hidden = true;
            toggle.textContent = text("Skift sted", "Change location");
            toggle.setAttribute('aria-expanded', 'false');
            if (!locationButton.hidden) {
                updateLocationButton();
            }
            toggle.focus();
        }

        locationShortcut?.addEventListener('click', () => {
            openPicker();
            toggle.focus();
            useLocation();
        });
        document.querySelector('[data-place-fallback]').hidden = true;
        toggle.hidden = false;
    }

    if (window.isSecureContext && navigator.geolocation) {
        locationButton.hidden = false;
        updateLocationButton();
        locationButton.addEventListener('click', () => {
            if (currentPlace && locationPreference?.placeId === currentPlace.currentPlace) {
                stopLocation();
                locationStatus.textContent = text("Automatisk placering er slået fra.", "Automatic location is off.");
            } else {
                useLocation();
            }
        });
        locationSuggestion.addEventListener('click', () => {
            if (suggestedPlace) {
                selectPlace(suggestedPlace);
            }
        });
        window.addEventListener('storage', event => {
            if (event.key === locationKey) {
                cancelLocationRequest();
                clearLocationFeedback();
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
