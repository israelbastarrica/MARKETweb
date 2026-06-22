// Puente página Blazor → iframe del visor 3D: le manda la lista de módulos a resaltar.
export function resaltar(iframe, modulos) {
    try {
        if (iframe && iframe.contentWindow) {
            iframe.contentWindow.postMessage({ tipo: 'resaltar', modulos: modulos || [] }, '*');
        }
    } catch (e) { console.error(e); }
}
