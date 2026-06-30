// Ficha técnica de artículo de viaje: costo unitario/total con tasas editables, combo y packing.
// Port de detalle_articulo.html (app ViajePedidos).

const tablaCombos = [
    { min: 0, max: 2100, combo: "2x6000" }, { min: 2101, max: 2800, combo: "2x8000" },
    { min: 2801, max: 3500, combo: "2x10000" }, { min: 3501, max: 5250, combo: "2x15000" },
    { min: 5251, max: 7000, combo: "2x20000" }, { min: 7001, max: 8750, combo: "2x25000" },
    { min: 8751, max: 10500, combo: "2x30000" }, { min: 10501, max: 14000, combo: "2x40000" },
    { min: 14001, max: 17500, combo: "2x50000" }, { min: 17501, max: 21000, combo: "2x60000" },
    { min: 21001, max: 28000, combo: "2x80000" }, { min: 28001, max: 35000, combo: "2x100000" }
];

export function init(d) {
    const fmt = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const pYuan = parseFloat(d.precioYuanes) || 0;
    const pDesc = parseFloat(d.pDesc) || 0;
    const pNac = parseFloat(d.pNac) || 0;
    const cbmUnit = parseFloat(d.cbmUnitario) || 0;
    const totalP = parseInt(d.totalPrendas) || 0;
    const cajasPedidas = parseFloat(d.cajasPedidas) || 0;
    const $ = id => document.getElementById(id);
    const set = (id, t) => { const e = $(id); if (e) e.innerText = t; };

    function actualizarSliderCombo(ars, tArs, tRmb) {
        const box = $('combo-box'), display = $('combo-display'), marker = $('combo-marker'),
              current = $('combo-current'), minLbl = $('combo-min'), maxLbl = $('combo-max');
        if (!box) return;
        let match = null;
        if (ars > 0) for (let c of tablaCombos) if (ars >= c.min && ars <= c.max) { match = c; break; }
        const denom = (tArs || 0) * (1 + pNac / 100) * (1 - pDesc / 100);
        const yuanFactor = (denom > 0 && tRmb > 0) ? (tRmb / denom) : 0;
        if (!match || yuanFactor <= 0) {
            display.innerText = "-"; marker.style.display = 'none'; current.style.display = 'none';
            minLbl.innerText = "¥ -"; maxLbl.innerText = "¥ -"; box.classList.add('combo-slider-empty'); return;
        }
        const yMin = match.min * yuanFactor, yMax = match.max * yuanFactor, yCur = ars * yuanFactor;
        const pos = Math.max(0, Math.min(100, ((ars - match.min) / (match.max - match.min)) * 100));
        display.innerText = match.combo;
        minLbl.innerText = "¥ " + fmt.format(yMin); maxLbl.innerText = "¥ " + fmt.format(yMax);
        current.innerText = "¥ " + fmt.format(yCur);
        marker.style.left = pos + '%'; current.style.left = pos + '%';
        marker.style.display = ''; current.style.display = ''; box.classList.remove('combo-slider-empty');
    }

    function recalcular() {
        let tRmb = parseFloat(($('tasa_rmb') || {}).value) || 7.24;
        let tArs = parseFloat(($('tasa_ars') || {}).value) || 1200;

        let yuanNetoUnit = pYuan * (1 - pDesc / 100);
        let usd = yuanNetoUnit / tRmb;
        let arsFobUnit = usd * tArs;
        let nacUsdUnit = usd * (pNac / 100);
        let nacArsUnit = arsFobUnit * (pNac / 100);
        let totalUsdUnit = usd + nacUsdUnit;
        let ars = arsFobUnit + nacArsUnit;

        set('unit_fob_yuan', "¥ " + fmt.format(yuanNetoUnit));
        set('unit_fob_usd', "U$S " + fmt.format(usd));
        set('unit_fob_ars', "$ " + fmt.format(arsFobUnit));
        set('unit_nac_usd', "+ U$S " + fmt.format(nacUsdUnit));
        set('unit_nac_ars', "+ $ " + fmt.format(nacArsUnit));
        set('unit_total_usd', "U$S " + fmt.format(totalUsdUnit));
        set('unit_total_ars', "$ " + fmt.format(ars));

        actualizarSliderCombo(ars, tArs, tRmb);

        const cbmFinal = cajasPedidas * cbmUnit;
        set('val_cbm_total', cbmFinal.toFixed(3));

        let fobUsdTotal = totalP * usd;
        let fobArsTotal = fobUsdTotal * tArs;
        let fobYuanTotal = totalP * pYuan * (1 - pDesc / 100);
        let nacUsdTotal = fobUsdTotal * (pNac / 100);
        let nacArsTotal = fobArsTotal * (pNac / 100);
        set('val_fob_yuan', "¥ " + fmt.format(fobYuanTotal));
        set('val_fob_usd', "U$S " + fmt.format(fobUsdTotal));
        set('val_fob_ars', "$ " + fmt.format(fobArsTotal));
        set('val_nac_usd', "+ U$S " + fmt.format(nacUsdTotal));
        set('val_nac_ars', "+ $ " + fmt.format(nacArsTotal));
        set('val_total_usd', "U$S " + fmt.format(fobUsdTotal + nacUsdTotal));
        set('val_total_ars', "$ " + fmt.format(fobArsTotal + nacArsTotal));
    }

    function renderPacks() {
        const area = $('area-colores-o-packs');
        if (!area) return;
        try {
            const strBD = d.packsArmados || '';
            const packsPorCajaArt = parseInt(d.packsPorCaja) || 1;
            const packsRaw = (strBD && strBD !== 'None' && strBD.trim() !== '') ? JSON.parse(strBD) : [];
            const packs = packsRaw.map(p => ({
                packs: p.packs ?? p.bultos ?? 0, cajas: p.cajas ?? p.bultos ?? 0,
                desc: p.desc || '', prendas: p.prendas || 0, talles: p.talles || null, colores: p.colores || null
            }));
            if (packs.length === 0) {
                const coloresBD = (d.colores || '').split(',');
                let h = '<h6 class="fw-bold text-muted text-uppercase small mb-3">🎨 Colores disponibles (pendientes de armar):</h6><div class="d-flex flex-wrap gap-2">';
                coloresBD.forEach(c => { const x = c.trim(); if (x) h += `<span class="badge bg-white text-dark border border-secondary px-3 py-2 fs-6 shadow-sm">${x}</span>`; });
                area.innerHTML = h + '</div>';
                return;
            }
            let html = `<h6 class="fw-bold text-primary text-uppercase small mb-3">📦 Desglose de packing list <small class="text-muted">(${packsPorCajaArt} pack/s por caja)</small></h6>`;
            packs.forEach(p => {
                const cajasTxt = Number.isInteger(p.cajas) ? p.cajas : (+p.cajas).toFixed(2);
                const packsTxt = Number.isInteger(p.packs) ? p.packs : (+p.packs).toFixed(2);
                const mostrarPacks = (packsPorCajaArt > 1 || p.packs !== p.cajas);
                let tablaPack = '';
                if (p.colores && p.colores.length && p.talles && p.talles.length) {
                    let totPorTalle = new Array(p.talles.length).fill(0), filas = '';
                    p.colores.forEach(c => {
                        const cant = (c.curva || []).map(v => v || 0);
                        const sub = cant.reduce((a, b) => a + b, 0);
                        cant.forEach((v, i) => totPorTalle[i] += v);
                        filas += `<tr><td class="text-start fw-bold">${c.nombre}</td><td class="text-center"><span class="badge bg-secondary">x${c.mult}</span></td>${cant.map(v => `<td class="text-center">${v || 0}</td>`).join('')}<td class="text-center fw-bold text-primary">${sub}</td></tr>`;
                    });
                    const totalPack = totPorTalle.reduce((a, b) => a + b, 0);
                    tablaPack = `<div class="mt-2"><small class="text-muted fw-bold d-block mb-1">Composición de 1 pack:</small>
                        <table class="table table-sm table-bordered text-center align-middle mb-0" style="font-size:.85rem;">
                        <thead class="table-light"><tr><th class="text-start">Color</th><th>Mult</th>${p.talles.map(t => `<th>${t}</th>`).join('')}<th class="bg-light">Subtotal</th></tr></thead>
                        <tbody>${filas}</tbody>
                        <tfoot class="table-light"><tr><td colspan="2" class="text-end fw-bold">Total pack:</td>${totPorTalle.map(v => `<td class="text-center fw-bold">${v}</td>`).join('')}<td class="text-center fw-bold text-danger">${totalPack}</td></tr></tfoot>
                        </table></div>`;
                }
                html += `<div class="card mb-2 border-primary-subtle shadow-sm"><div class="card-body py-2 px-3">
                    <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                        <div><span class="badge bg-primary fs-6 me-1">📦 ${cajasTxt} caja/s</span>${mostrarPacks ? `<span class="badge bg-info text-dark fs-6 me-1">🎁 ${packsTxt} pack/s</span>` : ''}<span class="fw-bold text-secondary ms-2">${p.desc}</span></div>
                        <span class="text-danger fw-bold fs-5">${p.prendas} <small class="text-muted fs-6">prendas</small></span>
                    </div>${tablaPack}</div></div>`;
            });
            area.innerHTML = html;
        } catch (e) {
            console.error(e);
            area.innerHTML = '<div class="alert alert-danger py-2 small mb-0">Error al leer los datos de empaque.</div>';
        }
    }

    const rmb = $('tasa_rmb'), ars = $('tasa_ars');
    if (rmb) rmb.addEventListener('input', recalcular);
    if (ars) ars.addEventListener('input', recalcular);
    renderPacks();
    recalcular();
}
