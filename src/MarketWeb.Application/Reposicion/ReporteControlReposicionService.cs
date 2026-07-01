using System.Net;
using System.Text;
using MarketWeb.Application.Data;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Reporte diario de control de Reposición (para enviar por mail a la mañana), ventana horaria del ciclo
/// (corrida de anoche ~21:00 → ahora ~9:00):
///  1) Repo — packs pedidos por local (snapshot Reposicion/ReposicionDetalle).
///  2) Reemplazos: cargados (propuestos) vs enviados (Procesado=1).
///  3) Control repo vs enviado: (a) por local, packs/prendas OK (coinciden con lo enviado);
///     (b) diferencias del lado de la repo (lo que se pidió y no se envió igual);
///     (c) lo que se mandó sin pedir, marcando si corresponde a un reemplazo.
/// El "enviado" sale del contenido real de los remitos (Dragon COMPROBANTEVDET).
/// </summary>
public interface IReporteControlReposicionService
{
    /// <summary>Arma el HTML del reporte para la ventana [desde, hasta]. Devuelve (html, resumen corto).</summary>
    Task<(string Html, string Resumen)> ConstruirAsync(DateTime desde, DateTime hasta, CancellationToken ct = default);
}

public sealed class ReporteControlReposicionService : IReporteControlReposicionService
{
    private readonly ISqlConnectionFactory _db;
    private readonly IControlRemitosService _control;
    public ReporteControlReposicionService(ISqlConnectionFactory db, IControlRemitosService control)
    {
        _db = db;
        _control = control;
    }

    private sealed class ReempTotales { public int Cargados; public int Enviados; }
    private sealed class PedidoLocal { public string Local = ""; public int Articulos; public int Packs; public int Prendas; }
    private sealed class LocalOk { public string Local = ""; public int ArticulosOk; public int PacksOk; public int PrendasOk; public int ArticulosDif; }
    private sealed class DiffRow
    {
        public string Local = ""; public string Art = ""; public string Des = ""; public bool SinMapeo;
        public int PacksPed; public int CantPack; public int Pedido; public int Enviado;
        public double PacksEnv => CantPack > 0 ? (double)Enviado / CantPack : 0;
        public int Dif => Pedido - Enviado;
    }
    private sealed class AfueraRow { public string Local = ""; public string Art = ""; public string Des = ""; public int Enviado; public bool EsReemplazo; }
    private sealed class DiffResult
    {
        public List<LocalOk> Ok = new();
        public List<DiffRow> Repo = new();
        public List<AfueraRow> Afuera = new();
    }

    private static string DragonDb(string origen) => (origen ?? "").ToUpperInvariant() switch
    {
        "LURO" => "DRAGONFISH_LURO",
        "PERALTA" => "DRAGONFISH_PERALTA",
        _ => "DRAGONFISH_CENTRAL",
    };

    public async Task<(string Html, string Resumen)> ConstruirAsync(DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var pedidos = new List<PedidoLocal>();
        var reemp = new ReempTotales();
        await using (var cn = _db.Create())
        {
            await cn.OpenAsync(ct);

            // 1) Repo: lo que se PIDIÓ por local (snapshot de la corrida del ciclo).
            await using (var cmd = new SqlCommand(
                @"SELECT ISNULL(rd.LocalDestino,'') AS Local,
                         COUNT(DISTINCT rd.ARTCOD) AS Articulos,
                         ISNULL(SUM(rd.PacksAReponer),0) AS Packs,
                         ISNULL(SUM(rd.PacksAReponer * ISNULL(rd.CantPack,1)),0) AS Prendas
                  FROM MARKET.dbo.ReposicionDetalle rd
                  JOIN MARKET.dbo.Reposicion r ON r.ID = rd.IDReposicion
                  WHERE ISNULL(r.Eliminado,0) = 0 AND r.FechaHoraCorrida >= @desde AND r.FechaHoraCorrida <= @hasta
                  GROUP BY rd.LocalDestino ORDER BY rd.LocalDestino", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);
                await using var rdr = await cmd.ExecuteReaderAsync(ct);
                while (await rdr.ReadAsync(ct))
                    pedidos.Add(new PedidoLocal
                    {
                        Local = rdr["Local"]?.ToString() ?? "",
                        Articulos = Convert.ToInt32(rdr["Articulos"]),
                        Packs = Convert.ToInt32(rdr["Packs"]),
                        Prendas = Convert.ToInt32(rdr["Prendas"])
                    });
            }

            // 2) Reemplazos: cargados (propuestos) vs enviados (visados).
            await using (var cmd = new SqlCommand(
                @"SELECT
                    SUM(CASE WHEN ISNULL(ARTCODReemplazo,'') <> '' THEN 1 ELSE 0 END) AS Cargados,
                    SUM(CASE WHEN ISNULL(ARTCODReemplazo,'') <> '' AND Procesado = 1 THEN 1 ELSE 0 END) AS Enviados
                  FROM MARKET.dbo.RepoReemplazos
                  WHERE Eliminado = 0 AND Fecha >= @desde AND Fecha <= @hasta", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);
                await using var rdr = await cmd.ExecuteReaderAsync(ct);
                if (await rdr.ReadAsync(ct))
                {
                    reemp.Cargados = rdr["Cargados"] is DBNull ? 0 : Convert.ToInt32(rdr["Cargados"]);
                    reemp.Enviados = rdr["Enviados"] is DBNull ? 0 : Convert.ToInt32(rdr["Enviados"]);
                }
            }
        }

        var diff = await DiferenciasAsync(desde, hasta, ct);

        var html = Render(desde, hasta, pedidos, reemp, diff);
        var resumen = $"repo pidió {pedidos.Sum(p => p.Packs)} packs · {diff.Ok.Sum(o => o.PacksOk)} packs OK · " +
                      $"{diff.Repo.Count} art. con dif · {diff.Afuera.Count} sin pedir · {reemp.Enviados}/{reemp.Cargados} reemplazos";
        return (html, resumen);
    }

    // Reconciliación repo vs enviado por local+artículo. Pedido = PacksAReponer*CantPack (prendas) del snapshot.
    // Enviado = SUM(FCANT) de los remitos al local dentro del ciclo (Dragon COMPROBANTEVDET).
    private async Task<DiffResult> DiferenciasAsync(DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var res = new DiffResult();

        // Pedido por (local, artículo) de la corrida del ciclo.
        var pedido = new Dictionary<string, (int Packs, int CantPack, int Prendas, string Des, bool SinMapeo)>(StringComparer.OrdinalIgnoreCase);
        var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reempSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // ARTCODReemplazo del ciclo
        await using (var cn = _db.Create())
        {
            await cn.OpenAsync(ct);
            await using (var cmd = new SqlCommand(
                @"SELECT UPPER(LTRIM(RTRIM(rd.LocalDestino))) AS Local, RTRIM(rd.ARTCOD) AS Art,
                         MAX(rd.ARTDES) AS Des, ISNULL(SUM(rd.PacksAReponer),0) AS Packs,
                         MAX(ISNULL(rd.CantPack,1)) AS CantPack,
                         ISNULL(SUM(rd.PacksAReponer * ISNULL(rd.CantPack,1)),0) AS Prendas,
                         MAX(CASE WHEN mp.Hay = 1 THEN 1 ELSE 0 END) AS TieneDepo
                  FROM MARKET.dbo.ReposicionDetalle rd
                  JOIN MARKET.dbo.Reposicion r ON r.ID = rd.IDReposicion
                  OUTER APPLY (SELECT TOP 1 1 AS Hay
                               FROM MARKET.dbo.MapeoRegistro R WITH(NOLOCK)
                               INNER JOIN MARKET.dbo.Mapeo m WITH(NOLOCK) ON m.ID = R.IDMapeo
                               WHERE RTRIM(R.ARTCOD) = RTRIM(rd.ARTCOD)
                                 AND m.IDUbicacion = 1 AND m.Eliminado = 0 AND R.Eliminado = 0) mp
                  WHERE ISNULL(r.Eliminado,0)=0 AND r.FechaHoraCorrida >= @desde AND r.FechaHoraCorrida <= @hasta
                  GROUP BY UPPER(LTRIM(RTRIM(rd.LocalDestino))), RTRIM(rd.ARTCOD)", cn) { CommandTimeout = 90 })
            {
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);
                await using var rdr = await cmd.ExecuteReaderAsync(ct);
                while (await rdr.ReadAsync(ct))
                {
                    var loc = rdr["Local"]?.ToString() ?? "";
                    var art = (rdr["Art"]?.ToString() ?? "").Trim();
                    locales.Add(loc);
                    pedido[loc + "|" + art] = (Convert.ToInt32(rdr["Packs"]), Convert.ToInt32(rdr["CantPack"]),
                        Convert.ToInt32(rdr["Prendas"]), (rdr["Des"]?.ToString() ?? "").Trim(), Convert.ToInt32(rdr["TieneDepo"]) == 0);
                }
            }

            // Artículos de reemplazo del ciclo (para marcar los envíos "sin pedir" que son reemplazos).
            await using (var cmd = new SqlCommand(
                @"SELECT DISTINCT UPPER(LTRIM(RTRIM(ARTCODReemplazo))) AS Art
                  FROM MARKET.dbo.RepoReemplazos
                  WHERE Eliminado = 0 AND ISNULL(ARTCODReemplazo,'') <> '' AND Fecha >= @desde AND Fecha <= @hasta", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);
                await using var rdr = await cmd.ExecuteReaderAsync(ct);
                while (await rdr.ReadAsync(ct)) reempSet.Add(rdr["Art"]?.ToString() ?? "");
            }
        }
        if (locales.Count == 0) return res;

        // Remitos del ciclo hacia locales de la repo (recorte por FechaRemito real).
        var remitos = await _control.ListadoAsync(desde, hasta, 0, "TODOS", ct);
        string? Match(string destino)
        {
            var d = (destino ?? "").ToUpperInvariant();
            return locales.FirstOrDefault(l => d == l || d.Contains(l) || l.Contains(d));
        }
        var codeLocal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var porOrigen = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in remitos)
        {
            if (string.IsNullOrWhiteSpace(r.RemitoId)) continue;
            if (r.FechaRemito is DateTime f && (f < desde || f > hasta)) continue;
            // Solo remitos con su RemitoEscaneado (recibidos): sino se cuentan duplicados/no recibidos y el enviado se infla.
            if (r.FechaEscaneo is null) continue;
            var loc = Match(r.Destino);
            if (loc is null) continue;
            codeLocal[r.RemitoId.Trim()] = loc;
            var db = DragonDb(r.Origen);
            if (!porOrigen.TryGetValue(db, out var l)) porOrigen[db] = l = new List<string>();
            l.Add(r.RemitoId.Trim());
        }

        // Enviado (unidades) por (local, artículo).
        var enviado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (codeLocal.Count > 0)
        {
            await using var cn = _db.Create();
            await cn.OpenAsync(ct);
            foreach (var (db, codes) in porOrigen)
            {
                for (int off = 0; off < codes.Count; off += 200)
                {
                    var lote = codes.Skip(off).Take(200).ToList();
                    var ps = lote.Select((_, i) => "@c" + i).ToList();
                    var sql = $@"SELECT RTRIM(DET.CODIGO) AS Cod, RTRIM(DET.FART) AS Art, SUM(DET.FCANT) AS Cant
                                 FROM {db}.ZooLogic.COMPROBANTEVDET DET WITH(NOLOCK)
                                 WHERE DET.CODIGO IN ({string.Join(",", ps)})
                                 GROUP BY RTRIM(DET.CODIGO), RTRIM(DET.FART)";
                    try
                    {
                        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 90 };
                        for (int i = 0; i < lote.Count; i++) cmd.Parameters.AddWithValue(ps[i], lote[i]);
                        await using var rdr = await cmd.ExecuteReaderAsync(ct);
                        while (await rdr.ReadAsync(ct))
                        {
                            var cod = (rdr["Cod"]?.ToString() ?? "").Trim();
                            if (!codeLocal.TryGetValue(cod, out var loc)) continue;
                            var art = (rdr["Art"]?.ToString() ?? "").Trim();
                            var cant = rdr["Cant"] is DBNull ? 0 : Convert.ToInt32(rdr["Cant"]);
                            var key = loc + "|" + art;
                            enviado[key] = (enviado.TryGetValue(key, out var v) ? v : 0) + cant;
                        }
                    }
                    catch { /* base no disponible para ese lote */ }
                }
            }
        }

        // OK por local + diferencias del lado de la repo.
        var okPorLocal = new Dictionary<string, LocalOk>(StringComparer.OrdinalIgnoreCase);
        LocalOk Ok(string loc) => okPorLocal.TryGetValue(loc, out var o) ? o : (okPorLocal[loc] = new LocalOk { Local = loc });
        foreach (var (key, p) in pedido)
        {
            var sep = key.IndexOf('|');
            var loc = sep >= 0 ? key[..sep] : key;
            var art = sep >= 0 ? key[(sep + 1)..] : "";
            int env = enviado.TryGetValue(key, out var e) ? e : 0;
            var o = Ok(loc);
            if (p.Prendas == env)
            {
                o.ArticulosOk++; o.PacksOk += p.Packs; o.PrendasOk += p.Prendas;
            }
            else
            {
                o.ArticulosDif++;
                res.Repo.Add(new DiffRow { Local = loc, Art = art, Des = p.Des, SinMapeo = p.SinMapeo, PacksPed = p.Packs, CantPack = p.CantPack, Pedido = p.Prendas, Enviado = env });
            }
        }
        res.Ok = okPorLocal.Values.OrderBy(x => x.Local).ToList();
        res.Repo = res.Repo.OrderBy(r => r.Local).ThenByDescending(r => Math.Abs(r.Dif)).ToList();

        // Enviado SIN pedir (por afuera): claves de enviado que no están en pedido.
        var afueraCods = new List<AfueraRow>();
        foreach (var (key, env) in enviado)
        {
            if (env <= 0 || pedido.ContainsKey(key)) continue;
            var sep = key.IndexOf('|');
            var loc = sep >= 0 ? key[..sep] : key;
            var art = sep >= 0 ? key[(sep + 1)..] : "";
            afueraCods.Add(new AfueraRow { Local = loc, Art = art, Enviado = env, EsReemplazo = reempSet.Contains(art.ToUpperInvariant()) });
        }
        // Descripción de los "sin pedir" desde Dragon (lote único).
        if (afueraCods.Count > 0)
        {
            var cods = afueraCods.Select(a => a.Art).Where(a => a.Length > 0).Distinct().ToList();
            var des = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using var cn = _db.Create();
            await cn.OpenAsync(ct);
            for (int off = 0; off < cods.Count; off += 200)
            {
                var lote = cods.Skip(off).Take(200).ToList();
                var ps = lote.Select((_, i) => "@c" + i).ToList();
                try
                {
                    await using var cmd = new SqlCommand(
                        $@"SELECT RTRIM(ARTCOD) AS Art, LTRIM(RTRIM(ISNULL(ARTDES,''))) AS Des
                           FROM DRAGONFISH_CENTRAL.ZooLogic.ART WITH(NOLOCK)
                           WHERE RTRIM(ARTCOD) IN ({string.Join(",", ps)})", cn) { CommandTimeout = 60 };
                    for (int i = 0; i < lote.Count; i++) cmd.Parameters.AddWithValue(ps[i], lote[i]);
                    await using var rdr = await cmd.ExecuteReaderAsync(ct);
                    while (await rdr.ReadAsync(ct)) des[(rdr["Art"]?.ToString() ?? "").Trim()] = rdr["Des"]?.ToString() ?? "";
                }
                catch { }
            }
            foreach (var a in afueraCods) if (des.TryGetValue(a.Art, out var d)) a.Des = d;
        }
        res.Afuera = afueraCods.OrderBy(a => a.Local).ThenByDescending(a => a.Enviado).ToList();

        return res;
    }

    private static string Render(DateTime desde, DateTime hasta, List<PedidoLocal> pedidos, ReempTotales reemp, DiffResult diff)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        var mismaFecha = desde.Date == hasta.Date;
        var per = mismaFecha ? $"{desde:dd/MM/yyyy} {desde:HH:mm}–{hasta:HH:mm}" : $"{desde:dd/MM HH:mm} – {hasta:dd/MM HH:mm}";

        sb.Append("<div style='font-family:Arial,Helvetica,sans-serif;color:#222;max-width:820px'>");
        sb.Append("<h2 style='margin:0 0 2px'>Control de Reposición</h2>");
        sb.Append($"<div style='color:#777;font-size:13px;margin-bottom:16px'>Ciclo: {per}</div>");

        // 1) Repo — pedido por local
        sb.Append("<h3 style='margin:18px 0 6px'>1 · Repo — pedido por local</h3>");
        if (pedidos.Count == 0)
            sb.Append("<p style='color:#777'>No hay corridas de reposición en el ciclo.</p>");
        else
        {
            sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
            sb.Append("<tr style='background:#f2f2f2'>" + Th("Local") + Th("Artículos", true) + Th("Packs", true) + Th("Prendas", true) + "</tr>");
            foreach (var p in pedidos)
                sb.Append("<tr>" + Td(H(p.Local)) + TdN(p.Articulos) + TdN(p.Packs) + TdN(p.Prendas) + "</tr>");
            sb.Append("<tr style='font-weight:bold;background:#fafafa'>" + Td("TOTAL") +
                TdN(pedidos.Sum(x => x.Articulos)) + TdN(pedidos.Sum(x => x.Packs)) + TdN(pedidos.Sum(x => x.Prendas)) + "</tr>");
            sb.Append("</table>");
        }

        // 2) Reemplazos
        sb.Append("<h3 style='margin:22px 0 6px'>2 · Reemplazos</h3>");
        sb.Append("<table style='border-collapse:collapse;font-size:13px'>");
        sb.Append("<tr>" + Td("Cargados (propuestos)") + TdN(reemp.Cargados) + "</tr>");
        sb.Append("<tr>" + Td("Enviados (visados)") + TdN(reemp.Enviados) + "</tr>");
        sb.Append("</table>");

        // 3) Control repo vs enviado
        sb.Append("<h3 style='margin:22px 0 6px'>3 · Control repo vs enviado</h3>");
        sb.Append("<div style='color:#777;font-size:12px;margin-bottom:6px'>«Enviado» = unidades de los remitos que tienen escaneo de recepción (RemitoEscaneado).</div>");

        // 3a) OK por local
        sb.Append("<div style='font-weight:bold;margin:10px 0 4px'>OK por local <span style='font-weight:normal;color:#777;font-size:12px'>(se envió lo que pidió)</span></div>");
        if (diff.Ok.Count == 0)
            sb.Append("<p style='color:#777'>Sin datos.</p>");
        else
        {
            sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
            sb.Append("<tr style='background:#f2f2f2'>" + Th("Local") + Th("Art. OK", true) + Th("Packs OK", true) + Th("Prendas OK", true) + Th("Art. c/dif", true) + "</tr>");
            foreach (var o in diff.Ok)
                sb.Append("<tr>" + Td(H(o.Local)) + TdN(o.ArticulosOk) + TdN(o.PacksOk) + TdN(o.PrendasOk) + TdN(o.ArticulosDif) + "</tr>");
            sb.Append("</table>");
        }

        // 3b) Diferencias del lado de la repo
        sb.Append("<div style='font-weight:bold;margin:16px 0 4px'>Diferencias (lado repo) <span style='font-weight:normal;color:#777;font-size:12px'>(pidió y no se envió igual · prendas)</span></div>");
        if (diff.Repo.Count == 0)
            sb.Append("<p style='color:#777'>Sin diferencias: se envió todo lo pedido.</p>");
        else
        {
            sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
            sb.Append("<tr style='background:#f2f2f2'>" + Th("Local") + Th("Código") + Th("Descripción") +
                Th("Packs", true) + Th("Cant/pack", true) + Th("Pedido", true) + Th("Packs env.", true) + Th("Enviado", true) + Th("Dif.", true) + "</tr>");
            foreach (var grupo in diff.Repo.GroupBy(r => r.Local))
            {
                foreach (var r in grupo)
                {
                    var color = r.Dif > 0 ? "#b00" : "#0a7";
                    var packsEnv = r.CantPack > 0 ? r.PacksEnv.ToString("0.#") : "—";
                    var desCell = H(r.Des) + (r.SinMapeo
                        ? " <span style='color:#b00;font-weight:bold'>· SIN MAPEO DEPO</span>" : "");
                    sb.Append("<tr>" + Td(H(r.Local)) + Td(H(r.Art)) + Td(desCell) +
                        TdN(r.PacksPed) + TdN(r.CantPack) + TdN(r.Pedido) + TdT(packsEnv) + TdN(r.Enviado) +
                        $"<td style='border:1px solid #ddd;padding:6px 8px;text-align:right;color:{color};font-weight:bold'>{r.Dif:N0}</td></tr>");
                }
                // Subtotal del local: cuánto se pidió y cuánto se envió (de las diferencias).
                int sPacks = grupo.Sum(x => x.PacksPed), sPed = grupo.Sum(x => x.Pedido), sEnv = grupo.Sum(x => x.Enviado), sDif = sPed - sEnv;
                var cDif = sDif > 0 ? "#b00" : "#0a7";
                sb.Append("<tr style='font-weight:bold;background:#fafafa'>" +
                    $"<td colspan='3' style='border:1px solid #ddd;padding:6px 8px'>Subtotal {H(grupo.Key)}</td>" +
                    TdN(sPacks) + Td("") + TdN(sPed) + Td("") + TdN(sEnv) +
                    $"<td style='border:1px solid #ddd;padding:6px 8px;text-align:right;color:{cDif}'>{sDif:N0}</td></tr>");
            }
            sb.Append("</table>");
        }

        // 3c) Se mandó sin pedir (por afuera) + ¿reemplazo?
        sb.Append("<div style='font-weight:bold;margin:16px 0 4px'>Se mandó sin pedir <span style='font-weight:normal;color:#777;font-size:12px'>(no estaba en la repo · prendas)</span></div>");
        if (diff.Afuera.Count == 0)
            sb.Append("<p style='color:#777'>Nada: no se envió nada fuera de lo pedido.</p>");
        else
        {
            sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
            sb.Append("<tr style='background:#f2f2f2'>" + Th("Local") + Th("Código") + Th("Descripción") + Th("Enviado", true) + Th("¿Reemplazo?") + "</tr>");
            foreach (var a in diff.Afuera)
            {
                var tag = a.EsReemplazo
                    ? "<span style='color:#0a7;font-weight:bold'>Reemplazo</span>"
                    : "<span style='color:#b00;font-weight:bold'>Por afuera</span>";
                sb.Append("<tr>" + Td(H(a.Local)) + Td(H(a.Art)) + Td(H(a.Des)) + TdN(a.Enviado) +
                    $"<td style='border:1px solid #ddd;padding:6px 8px'>{tag}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("<p style='color:#999;font-size:12px;margin-top:20px'>Generado automáticamente por MARKET Web.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Th(string t, bool num = false)
        => $"<th style='border:1px solid #ddd;padding:6px 8px;text-align:{(num ? "right" : "left")}'>{WebUtility.HtmlEncode(t)}</th>";
    private static string Td(string t)
        => $"<td style='border:1px solid #ddd;padding:6px 8px'>{t}</td>";
    private static string TdN(int n)
        => $"<td style='border:1px solid #ddd;padding:6px 8px;text-align:right'>{n:N0}</td>";
    private static string TdT(string s)
        => $"<td style='border:1px solid #ddd;padding:6px 8px;text-align:right'>{WebUtility.HtmlEncode(s)}</td>";
}
