// Lector de código de barras / QR para el remito (escanear bolsas del depósito).
// Usa ZXing DIRECTO (la misma librería que la app "Barcode Scanner") con TRY_HARDER,
// escaneando el frame COMPLETO en alta resolución. Lee Code128 (bolsas) y QR.
// Vendorizado en /lib/zxing (no depende de internet). Carga perezosa al primer uso.
// Llama de vuelta a .NET: OnScanResult(codigo) al leer, OnScanError(motivo) si falla.

let _reader = null;
let _entregado = false;

function cargarLib() {
    return new Promise((resolve, reject) => {
        if (window.ZXing) return resolve();
        const s = document.createElement('script');
        s.src = '/lib/zxing/zxing.min.js';
        s.onload = () => resolve();
        s.onerror = () => reject(new Error('no se pudo cargar el lector'));
        document.head.appendChild(s);
    });
}

export async function start(videoId, dotNetRef) {
    try {
        if (!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia)) {
            await dotNetRef.invokeMethodAsync('OnScanError', 'no-soportado');
            return false;
        }
        await cargarLib();

        const Z = window.ZXing;
        const hints = new Map();
        hints.set(Z.DecodeHintType.POSSIBLE_FORMATS, [Z.BarcodeFormat.CODE_128, Z.BarcodeFormat.QR_CODE]);
        hints.set(Z.DecodeHintType.TRY_HARDER, true);   // modo insistente, como los lectores dedicados

        _reader = new Z.BrowserMultiFormatReader(hints, 200);
        _entregado = false;

        // Cámara trasera en alta resolución. ZXing decodifica el FRAME COMPLETO (sin recorte).
        const constraints = {
            audio: false,
            video: {
                facingMode: { ideal: 'environment' },
                width: { ideal: 1920 },
                height: { ideal: 1080 }
            }
        };

        await _reader.decodeFromConstraints(constraints, videoId, async (result, err) => {
            if (!result || _entregado) return;
            const val = (result.getText() || '').trim();
            if (!val) return;
            _entregado = true;
            await detener();
            await dotNetRef.invokeMethodAsync('OnScanResult', val);
        });

        // Foco continuo: best-effort sobre el track ya iniciado.
        try {
            const v = document.getElementById(videoId);
            const track = (v && v.srcObject && v.srcObject.getVideoTracks) ? v.srcObject.getVideoTracks()[0] : null;
            if (track && track.applyConstraints) {
                await track.applyConstraints({ advanced: [{ focusMode: 'continuous' }] });
            }
        } catch (e) { /* la cámara no permite control de foco */ }

        return true;
    } catch (e) {
        console.error('rnscan zxing error:', e);
        await detener();
        const motivo = (e && (e.name === 'NotAllowedError' ? 'permiso' : (e.name || e.message || 'error')));
        await dotNetRef.invokeMethodAsync('OnScanError', motivo);
        return false;
    }
}

async function detener() {
    if (_reader) {
        try { _reader.reset(); } catch (e) { }
        _reader = null;
    }
}

export function stop() { return detener(); }
