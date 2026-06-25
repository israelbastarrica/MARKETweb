// Gráfico del Informe de Ventas (Chart.js vendorizado en /lib/chartjs). Carga perezosa.
let _chart = null;

function cargar() {
    return new Promise((resolve, reject) => {
        if (window.Chart) return resolve();
        const s = document.createElement('script');
        s.src = '/lib/chartjs/chart.umd.min.js';
        s.onload = () => resolve();
        s.onerror = () => reject(new Error('no se pudo cargar chart.js'));
        document.head.appendChild(s);
    });
}

export async function render(canvasId, tipo, labels, datasets, titulo) {
    await cargar();
    const el = document.getElementById(canvasId);
    if (!el) return;
    if (_chart) { try { _chart.destroy(); } catch (e) { } _chart = null; }

    const apilado = tipo === 'bar';
    _chart = new window.Chart(el, {
        type: tipo,
        data: { labels: labels, datasets: datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                title: { display: !!titulo, text: titulo },
                legend: { position: 'bottom' },
                tooltip: { callbacks: {} }
            },
            scales: {
                x: { stacked: apilado },
                y: { stacked: apilado, beginAtZero: true }
            }
        }
    });
}

export function destroy() {
    if (_chart) { try { _chart.destroy(); } catch (e) { } _chart = null; }
}
