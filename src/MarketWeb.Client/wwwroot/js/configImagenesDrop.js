// Drag & drop a nivel ventana para Config Imágenes (Diseño).
// Permite soltar una imagen en cualquier parte de la página: lee el archivo y se lo pasa
// al componente Blazor (ConfigImagenes) que abre el alta con la foto ya cargada.
// Espejo del DragDrop de frmRepoCatalogosConfigImagenes.

const MAX_BYTES = 10 * 1024 * 1024; // 10 MB
let dotnet = null;
let dragDepth = 0;

function tieneArchivos(e) {
    return e.dataTransfer && Array.from(e.dataTransfer.types || []).includes("Files");
}

function onDragEnter(e) {
    if (!tieneArchivos(e)) return;
    e.preventDefault();
    dragDepth++;
    dotnet?.invokeMethodAsync("SetDragActivo", true);
}

function onDragOver(e) {
    if (!tieneArchivos(e)) return;
    e.preventDefault(); // necesario para habilitar el drop
    e.dataTransfer.dropEffect = "copy";
}

function onDragLeave(e) {
    if (!tieneArchivos(e)) return;
    dragDepth--;
    if (dragDepth <= 0) {
        dragDepth = 0;
        dotnet?.invokeMethodAsync("SetDragActivo", false);
    }
}

async function onDrop(e) {
    if (!tieneArchivos(e)) return;
    e.preventDefault(); // evita que el navegador abra la imagen
    dragDepth = 0;
    if (!dotnet) return;

    const file = e.dataTransfer.files && e.dataTransfer.files[0];
    if (!file) {
        dotnet.invokeMethodAsync("SetDragActivo", false);
        return;
    }
    if (file.size > MAX_BYTES) {
        dotnet.invokeMethodAsync("RecibirErrorArrastre", "La imagen supera el tamaño máximo permitido (10 MB).");
        return;
    }
    try {
        const buffer = await file.arrayBuffer();
        const b64 = base64DesdeBuffer(buffer);
        await dotnet.invokeMethodAsync("RecibirImagenArrastrada", file.name || "", file.type || "", b64);
    } catch {
        dotnet.invokeMethodAsync("RecibirErrorArrastre", "No se pudo leer la imagen arrastrada.");
    }
}

function base64DesdeBuffer(buffer) {
    let binary = "";
    const bytes = new Uint8Array(buffer);
    const chunk = 0x8000; // de a 32k para no reventar el stack en btoa
    for (let i = 0; i < bytes.length; i += chunk) {
        binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
    }
    return btoa(binary);
}

export function init(dotnetRef) {
    dotnet = dotnetRef;
    dragDepth = 0;
    window.addEventListener("dragenter", onDragEnter);
    window.addEventListener("dragover", onDragOver);
    window.addEventListener("dragleave", onDragLeave);
    window.addEventListener("drop", onDrop);
}

export function dispose() {
    window.removeEventListener("dragenter", onDragEnter);
    window.removeEventListener("dragover", onDragOver);
    window.removeEventListener("dragleave", onDragLeave);
    window.removeEventListener("drop", onDrop);
    dotnet = null;
    dragDepth = 0;
}
