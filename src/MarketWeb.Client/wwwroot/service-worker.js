// Service worker de desarrollo: no cachea nada (deja todo pasar a la red).
// El comportamiento real de PWA está en service-worker.published.js.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.clients.claim());
self.addEventListener('fetch', () => { });
