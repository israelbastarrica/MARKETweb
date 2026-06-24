// Service worker AUTODESTRUCTIVO. Ya no usamos PWA: con deploys muy seguidos, el
// caché del SW dejaba "pegada" la versión vieja en las tablets. Este SW lo único que
// hace es limpiar todas las cachés, desregistrarse y recargar las pestañas abiertas.
// El navegador chequea este archivo y, al ver que cambió, corre esta versión que se
// suicida → la tablet vuelve a tomar siempre lo último con un simple refresco.
self.addEventListener('install', event => self.skipWaiting());

self.addEventListener('activate', event => event.waitUntil((async () => {
    try {
        const keys = await caches.keys();
        await Promise.all(keys.map(k => caches.delete(k)));
    } catch (e) { /* ignorar */ }

    await self.registration.unregister();

    // Recarga las pestañas abiertas para que tomen el contenido fresco (sin SW).
    const clients = await self.clients.matchAll({ type: 'window' });
    clients.forEach(c => c.navigate(c.url));
})()));
