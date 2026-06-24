// Pantalla ancha para tablets: entra en fullscreen y bloquea la orientación a horizontal.
// Una web sólo puede bloquear la orientación estando en fullscreen (Chrome Android lo soporta).
// Como MarketWeb es SPA, queda activo en toda la app hasta salir.

export async function entrar() {
    try {
        const el = document.documentElement;
        if (el.requestFullscreen) await el.requestFullscreen();
        if (screen.orientation && screen.orientation.lock) {
            await screen.orientation.lock('landscape');
        }
        return true;
    } catch (e) {
        return !!document.fullscreenElement;
    }
}

export async function salir() {
    try { if (screen.orientation && screen.orientation.unlock) screen.orientation.unlock(); } catch (e) { }
    try { if (document.fullscreenElement && document.exitFullscreen) await document.exitFullscreen(); } catch (e) { }
    return false;
}

export function activo() {
    return !!document.fullscreenElement;
}
