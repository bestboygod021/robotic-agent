const CACHE_NAME = 'robotic-agent-pwa-v2';
const ASSETS = [
  'index.html',
  'portal.html',
  'admin.html',
  'trust.html',
  'site-config.js',
  'manifest.json'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache => cache.addAll(ASSETS)).catch(() => {})
  );
  self.skipWaiting();
});

self.addEventListener('fetch', event => {
  event.respondWith(
    caches.match(event.request).then(cached => cached || fetch(event.request))
  );
});
