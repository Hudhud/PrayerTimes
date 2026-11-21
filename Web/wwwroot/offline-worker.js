const cacheName = 'salahtimes-offline-plan';
const scope = new URL(self.registration.scope);
const planUrl = new URL('offline', scope);
const assets = ['css/site.css', 'icons/crescent.svg', 'icons/favicon.svg'].map(path => new URL(path, scope).href);

self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('fetch', event => {
    const { request } = event;
    if (request.method !== 'GET') {
        return;
    }

    const url = new URL(request.url);
    if (url.origin !== scope.origin) {
        return;
    }

    if (request.mode === 'navigate') {
        event.respondWith(openPage(request, url));
    } else if (assets.includes(url.href)) {
        event.respondWith(caches.match(request, { cacheName }).then(asset => asset || fetch(request), () => fetch(request)));
    }
});

async function openPage(request, url) {
    const saved = () => caches.match(planUrl.href, { cacheName }).catch(() => undefined);
    if (url.pathname === planUrl.pathname) {
        return await saved() ?? fetch(request);
    }

    try {
        return await fetch(request);
    } catch (error) {
        const plan = await saved();
        if (plan && (url.pathname === scope.pathname ||
            `${url.pathname}/offline` === new URL(plan.url).pathname)) {
            return plan;
        }
        throw error;
    }
}
