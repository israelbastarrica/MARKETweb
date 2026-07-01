using System.Data;
using System.Net;
using System.Text;
using MarketWeb.Application.Data;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Reporte diario de control de Reposición (para enviar por mail a la mañana):
///  1) Por local: conteo de remitos generados/despachados/recibidos/aceptados (SP_RemitosControlEstado).
///  2) Enviado "por afuera": refuerzos cargados como evento (EventosReposicion, Accion='ENVIAR REFUERZO').
///  3) Reemplazos: cargados (propuestos) vs enviados (Procesado=1) — RepoReemplazos.
/// Como corre ~9 AM, se asume que el ciclo de anoche ya está todo despachado/recibido.
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

    private sealed class RemitoLocal
    {
        public string Local = "";
        public int Generados, Despachados, Recibidos, NoRecibidos, Aceptados, Rechazados;
    }
    private sealed class RefuerzoLocal { public string Local = ""; public int Eventos; public int Packs; }
    private sealed class ReempTotales { public int Cargados; public int Enviados; }
    private sealed class PedidoLocal { public string Local = ""; public int Articulos; public int Packs; public int Prendas; }
    private sealed class DiffRow
    {
        public string Local = ""; public string Art = ""; public string Des = "";
        public int PacksPed; public int CantPack; public int Pedido; public int Enviado;
        public double PacksEnv => CantPack > 0 ? (double)Enviado / CantPack : 0;
        public int Dif => Pedido - Enviado;
    }

    private static string DragonDb(string origen) => (origen ?? "").ToUpperInvariant() switch
    {
        "LURO" => "DRAGONFISH_LURO",
        "PERALTA" => "DRAGONFISH_PERALTA",
        _ => "DRAGONFISH_CENTRAL",
    };

    // Detalle de diferencias repo vs enviado, por local+artículo (solo locales con corrida de repo en el período).
    // Pedido = SUM(PacksAReponer*CantPack) prendas del snapshot. Enviado = SUM(FCANT) de los remitos al local (Dragon).
    private async Task<List<DiffRow>> DiferenciasAsync(DateTime desde, DateTime hasta, CancellationToken ct)
    {
        // 1) Pedido por (local, artículo) de la/s corrida/s dentro de la ventana horaria [desde, hasta].
        var pedido = new Dictionary<string, (int Packs, int CantPack, int Prendas, string Des)>(StringComparer.OrdinalIgnoreCase);
        var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cn = _db.Create())
        {
            await cn.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                @"SELECT UPPER(LTRIM(RTRIM(rd.LocalDestino))) AS Local, RTRIM(rd.ARTCOD) AS Art,
                         MAX(rd.ARTDES) AS Des, ISNULL(SUM(rd.PacksAReponer),0) AS Packs,
                         MAX(ISNULL(rd.CantPack,1)) AS CantPack,
                         ISNULL(SUM(rd.PacksAReponer * ISNULL(rd.CantPack,1)),0) AS Prendas
                  FROM MARKET.dbo.ReposicionDetalle rd
                  JOIN MARKET.dbo.Reposicion r ON r.ID = rd.IDReposicion
                  WHERE ISNULL(r.Eliminado,0)=0 AND r.FechaHoraCorrida >= @desde AND r.FechaHoraCorrida <= @hasta
                  GROUP BY UPPER(LTRIM(RTRIM(rd.LocalDestino))), RTRIM(rd.ARTCOD)", cn) { CommandTimeout = 90 };
            cmd.Parameters.AddWithValue("@desde", desde);
            cmd.Parameters.AddWithValue("@hasta", hasta);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var loc = rdr["Local"]?.ToString() ?? "";
                var art = (rdr["Art"]?.ToString() ?? "").Trim();
                locales.Add(loc);
                pedido[loc + "|" + art] = (Convert.ToInt32(rdr["Packs"]), Convert.ToInt32(rdr["CantPack"]),
                    Convert.ToInt32(rdr["Prendas"]), (rdr["Des"]?.ToString() ?? "").Trim());
            }
        }
        if (locales.Count == 0) return new();

        // 2) Remitos dentro de la ventana horaria [desde, hasta]; solo los que van a un local de la repo.
        var remitos = await _control.ListadoAsync(desde, hasta, 0, "TODOS", ct);
        string? Match(string destino)
        {
            var d = (destino ?? "").ToUpperInvariant();
            return locales.FirstOrDefault(l => d == l || d.Contains(l) || l.Contains(d));
        }
        var codeLocal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // remitoId -> local
        var porOrigen = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in remitos)
        {
            if (string.IsNullOrWhiteSpace(r.RemitoId)) continue;
            // La ventana real es HORARIA (ciclo 21h→9h): recortamos por la fecha/hora del remito.
            if (r.FechaRemito is DateTime f && (f < desde || f > hasta)) continue;
            var loc = Match(r.Destino);
            if (loc is null) continue;
            codeLocal[r.RemitoId.Trim()] = loc;
            var db = DragonDb(r.Origen);
            if (!porOrigen.TryGetValue(db, out var l)) porOrigen[db] = l = new List<string>();
            l.Add(r.RemitoId.Trim());
        }

        // 3) Enviado (unidades) por (local, artículo) leyendo COMPROBANTEVDET de la base Dragon del origen.
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

        // 4) Diferencias (solo donde pedido != enviado).
        var rows = new List<DiffRow>();
        foreach (var key in pedido.Keys.Union(enviado.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var sep = key.IndexOf('|');
            var loc = sep >= 0 ? key[..sep] : key;
            var art = sep >= 0 ? key[(sep + 1)..] : "";
            var hayPed = pedido.TryGetValue(key, out var p);
            int ped = hayPed ? p.Prendas : 0;
            int env = enviado.TryGetValue(key, out var e) ? e : 0;
            if (ped == env) continue;
            rows.Add(new DiffRow
            {
                Local = loc, Art = art, Des = hayPed ? p.Des : "",
                PacksPed = hayPed ? p.Packs : 0, CantPack = hayPed ? p.CantPack : 0,
                Pedido = ped, Enviado = env
            });
        }
        return rows.OrderBy(r => r.Local).ThenByDescending(r => Math.Abs(r.Dif)).ToList();
    }

    public async Task<(string Html, string Resumen)> ConstruirAsync(DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        // 1) Remitos por local (agrega las filas del período por local).
        var estado = await _control.EstadoAsync(desde, hasta, ct);
        var porLocal = new Dictionary<string, RemitoLocal>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in estado)
        {
            if (!porLocal.TryGetValue(e.LocalDestino, out var r))
                porLocal[e.LocalDestino] = r = new RemitoLocal { Local = e.LocalDestino };
            r.Generados += e.Generados; r.Despachados += e.Despachados; r.Recibidos += e.Recibidos;
            r.NoRecibidos += e.NoRecibidos; r.Aceptados += e.Aceptados; r.Rechazados += e.Rechazados;
        }
        var remitos = porLocal.Values.OrderBy(x => x.Local).ToList();

        // Packs que pidió la repo por local (snapshot de la/las corrida/s del período), refuerzos y reemplazos.
        var pedidos = new List<PedidoLocal>();
        var refuerzos = new List<RefuerzoLocal>();
        var reemp = new ReempTotales();
        await using (var cn = _db.Create())
        {
            await cn.OpenAsync(ct);
            // Ventana HORARIA (ciclo 21h→9h), no por día completo.
            var d0 = desde;
            var d1 = hasta;

            // Repo: lo que se PIDIÓ por local (independiente de remitos). PacksAReponer = packs recomendados.
            await using (var cmd = new SqlCommand(
                @"SELECT ISNULL(rd.LocalDestino,'') AS Local,
                         COUNT(DISTINCT rd.ARTCOD) AS Articulos,
                         ISNULL(SUM(rd.PacksAReponer),0) AS Packs,
                         ISNULL(SUM(rd.Pendiente),0) AS Prendas
                  FROM MARKET.dbo.ReposicionDetalle rd
                  JOIN MARKET.dbo.Reposicion r ON r.ID = rd.IDReposicion
                  WHERE ISNULL(r.Eliminado,0) = 0 AND r.FechaHoraCorrida >= @d0 AND r.FechaHoraCorrida <= @d1
                  GROUP BY rd.LocalDestino ORDER BY rd.LocalDestino", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.AddWithValue("@d0", d0);
                cmd.Parameters.AddWithValue("@d1", d1);
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

            await using (var cmd = new SqlCommand(
                @"SELECT ISNULL(Local,'') AS Local, COUNT(*) AS Eventos, ISNULL(SUM(ISNULL(CantidadPacks,0)),0) AS Packs
                  FROM MARKET.dbo.EventosReposicion
                  WHERE Eliminado = 0 AND Accion LIKE 'ENVIAR REFUERZO%'
                        AND FechaEvento >= @d0 AND FechaEvento <= @d1
                  GROUP BY Local ORDER BY Local", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.AddWithValue("@d0", d0);
                cmd.Parameters.AddWithValue("@d1", d1);
                await using var rdr = await cmd.ExecuteReaderAsync(ct);
                while (await rdr.ReadAsync(ct))
                    refuerzos.Add(new RefuerzoLocal
                    {
                        Local = rdr["Local"]?.ToString() ?? "",
                        Eventos = Convert.ToInt32(rdr["Eventos"]),
                        Packs = Convert.ToInt32(rdr["Packs"])
                    });
            }

            await using (var cmd = new SqlCommand(
                @"SELECT
                    SUM(CASE WHEN ISNULL(ARTCODReemplazo,'') <> '' THEN 1 ELSE 0 END) AS Cargados,
                    SUM(CASE WHEN ISNULL(ARTCODReemplazo,'') <> '' AND Procesado = 1 THEN 1 ELSE 0 END) AS Enviados
                  FROM MARKET.dbo.RepoReemplazos
                  WHERE Eliminado = 0 AND Fecha >= @d0 AND Fecha <= @d1", cn) { CommandTimeout = 60 })
            {
                cmd.Parameters.AddWithValue("@d0", d0);
                cmd.Parameters.AddWithValue("@d1", d1);
                await using var rdr = await cmd.ExecuteReaderAsync(ct);
                if (await rdr.ReadAsync(ct))
                {
                    reemp.Cargados = rdr["Cargados"] is DBNull ? 0 : Convert.ToInt32(rdr["Cargados"]);
                    reemp.Enviados = rdr["Enviados"] is DBNull ? 0 : Convert.ToInt32(rdr["Enviados"]);
                }
            }
        }

        var diffs = await DiferenciasAsync(desde, hasta, ct);

        var html = Render(desde, hasta, pedidos, remitos, refuerzos, reemp, diffs);
        var totalRecibidos = remitos.Sum(r => r.Recibidos);
        var totalGenerados = remitos.Sum(r => r.Generados);
        var resumen = $"repo pidió {pedidos.Sum(p => p.Packs)} packs · {totalRecibidos}/{totalGenerados} remitos recibidos · {refuerzos.Sum(r => r.Packs)} packs de refuerzo · {reemp.Enviados}/{reemp.Cargados} reemplazos enviados";
        return (html, resumen);
    }

    private static string Render(DateTime desde, DateTime hasta, List<PedidoLocal> pedidos, List<RemitoLocal> remitos, List<RefuerzoLocal> refuerzos, ReempTotales reemp, List<DiffRow> diffs)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        var per = desde.Date == hasta.Date ? desde.ToString("dd/MM/yyyy") : $"{desde:dd/MM/yyyy} – {hasta:dd/MM/yyyy}";

        sb.Append("<div style='font-family:Arial,Helvetica,sans-serif;color:#222;max-width:820px'>");
        sb.Append($"<h2 style='margin:0 0 2px'>Control de Reposición</h2>");
        sb.Append($"<div style='color:#777;font-size:13px;margin-bottom:16px'>Período: {per}</div>");

        // 1) Repo: packs pedidos por local (del snapshot, independiente de remitos)
        sb.Append("<h3 style='margin:18px 0 6px'>1 · Repo — pedido por local</h3>");
        if (pedidos.Count == 0)
            sb.Append("<p style='color:#777'>No hay corridas de reposición en el período.</p>");
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

        // 2) Remitos por local
        sb.Append("<h3 style='margin:22px 0 6px'>2 · Remitos por local (enviado vs llegado)</h3>");
        if (remitos.Count == 0)
            sb.Append("<p style='color:#777'>Sin remitos en el período.</p>");
        else
        {
            sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
            sb.Append("<tr style='background:#f2f2f2'>" +
                Th("Local") + Th("Generados", true) + Th("Despachados", true) + Th("Recibidos", true) +
                Th("No recib.", true) + Th("Aceptados", true) + Th("Rechazados", true) + "</tr>");
            foreach (var r in remitos)
                sb.Append("<tr>" + Td(H(r.Local)) + TdN(r.Generados) + TdN(r.Despachados) + TdN(r.Recibidos) +
                    TdN(r.NoRecibidos) + TdN(r.Aceptados) + TdN(r.Rechazados) + "</tr>");
            sb.Append("<tr style='font-weight:bold;background:#fafafa'>" + Td("TOTAL") +
                TdN(remitos.Sum(x => x.Generados)) + TdN(remitos.Sum(x => x.Despachados)) + TdN(remitos.Sum(x => x.Recibidos)) +
                TdN(remitos.Sum(x => x.NoRecibidos)) + TdN(remitos.Sum(x => x.Aceptados)) + TdN(remitos.Sum(x => x.Rechazados)) + "</tr>");
            sb.Append("</table>");
        }

        // 2) Enviado por afuera (refuerzos)
        sb.Append("<h3 style='margin:22px 0 6px'>3 · Enviado por afuera — refuerzos</h3>");
        if (refuerzos.Count == 0)
            sb.Append("<p style='color:#777'>No se cargaron refuerzos en el período.</p>");
        else
        {
            sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
            sb.Append("<tr style='background:#f2f2f2'>" + Th("Local") + Th("Refuerzos", true) + Th("Packs", true) + "</tr>");
            foreach (var r in refuerzos)
                sb.Append("<tr>" + Td(H(r.Local)) + TdN(r.Eventos) + TdN(r.Packs) + "</tr>");
            sb.Append("<tr style='font-weight:bold;background:#fafafa'>" + Td("TOTAL") +
                TdN(refuerzos.Sum(x => x.Eventos)) + TdN(refuerzos.Sum(x => x.Packs)) + "</tr>");
            sb.Append("</table>");
        }

        // 3) Reemplazos
        sb.Append("<h3 style='margin:22px 0 6px'>4 · Reemplazos</h3>");
        sb.Append("<table style='border-collapse:collapse;font-size:13px'>");
        sb.Append("<tr>" + Td("Cargados (propuestos)") + TdN(reemp.Cargados) + "</tr>");
        sb.Append("<tr>" + Td("Enviados (visados)") + TdN(reemp.Enviados) + "</tr>");
        sb.Append("</table>");

        // 5) Detalle de diferencias repo vs enviado (por local/artículo)
        sb.Append("<h3 style='margin:22px 0 6px'>5 · Diferencias repo vs enviado <span style='font-weight:normal;color:#777;font-size:12px'>(prendas)</span></h3>");
        if (diffs.Count == 0)
            sb.Append("<p style='color:#777'>Sin diferencias: se envió exactamente lo que pidió la repo.</p>");
        else
        {
            sb.Append("<div style='color:#777;font-size:12px;margin-bottom:6px'>Pedido y Enviado en prendas (packs × cant/pack). Dif = pedido − enviado: <b>positivo</b> = faltó enviar · <b>negativo</b> = se envió de más / por afuera.</div>");
            sb.Append("<table style='border-collapse:collapse;width:100%;font-size:13px'>");
            sb.Append("<tr style='background:#f2f2f2'>" + Th("Local") + Th("Código") + Th("Descripción") +
                Th("Packs", true) + Th("Cant/pack", true) + Th("Pedido", true) +
                Th("Packs env.", true) + Th("Enviado", true) + Th("Dif.", true) + "</tr>");
            foreach (var r in diffs)
            {
                var color = r.Dif > 0 ? "#b00" : "#0a7";
                var packsEnv = r.CantPack > 0 ? r.PacksEnv.ToString("0.#") : "—";
                sb.Append("<tr>" + Td(H(r.Local)) + Td(H(r.Art)) + Td(H(r.Des)) +
                    TdN(r.PacksPed) + TdN(r.CantPack) + TdN(r.Pedido) +
                    TdT(packsEnv) + TdN(r.Enviado) +
                    $"<td style='border:1px solid #ddd;padding:6px 8px;text-align:right;color:{color};font-weight:bold'>{r.Dif:N0}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("<p style='color:#999;font-size:12px;margin-top:20px'>Generado automáticamente por MARKET Web (programador de tareas).</p>");
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
