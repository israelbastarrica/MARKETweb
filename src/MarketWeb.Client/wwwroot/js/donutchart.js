// Dona del reporte de Motivos de Reposición (Chart.js vendorizado en /lib/chartjs). Carga perezosa.
let _d = null;

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

// Paleta estable (mismos colores aunque cambie el orden poco a poco).
const PALETA = ['#e8820c', '#3ca050', '#2f6fb0', '#b02a37', '#7a4fb5', '#0f9b8e',
                '#c9a227', '#d2602e', '#5a6b7b', '#9d4d8c', '#4b8b3b', '#806040'];

export async function render(canvasId, labels, values) {
    await cargar();
    const el = document.getElementById(canvasId);
    if (!el) return;
    if (_d) { try { _d.destroy(); } catch (e) { } _d = null; }

    const dark = document.documentElement.getAttribute('data-theme') === 'dark';
    const txt = dark ? '#d8d8d8' : '#333';
    const colors = labels.map((_, i) => PALETA[i % PALETA.length]);

    _d = new window.Chart(el, {
        type: 'doughnut',
        data: { labels: labels, datasets: [{ data: values, backgroundColor: colors, borderWidth: 1, borderColor: dark ? '#1e1e1e' : '#fff' }] },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '58%',
            plugins: {
                legend: { display: false },   // la leyenda la hace la tabla de motivos (color + nombre + %)
                tooltip: {
                    callbacks: {
                        label: (c) => {
                            const tot = c.dataset.data.reduce((a, b) => a + b, 0) || 1;
                            const p = (c.parsed / tot * 100);
                            return ` ${c.label}: ${c.parsed} (${p.toFixed(1)}%)`;
                        }
                    }
                }
            }
        }
    });
}

export function destroy() {
    if (_d) { try { _d.destroy(); } catch (e) { } _d = null; }
}
