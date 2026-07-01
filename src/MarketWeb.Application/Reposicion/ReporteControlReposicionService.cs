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
            var d0 = desde.Date;
            var d1 = hasta.Date.AddDays(1);

            // Repo: lo que se PIDIÓ por local (independiente de remitos). PacksAReponer = packs recomendados.
            await using (var cmd = new SqlCommand(
                @"SELECT ISNULL(rd.LocalDestino,'') AS Local,
                         COUNT(DISTINCT rd.ARTCOD) AS Articulos,
                         ISNULL(SUM(rd.PacksAReponer),0) AS Packs,
                         ISNULL(SUM(rd.Pendiente),0) AS Prendas
                  FROM MARKET.dbo.ReposicionDetalle rd
                  JOIN MARKET.dbo.Reposicion r ON r.ID = rd.IDReposicion
                  WHERE ISNULL(r.Eliminado,0) = 0 AND r.FechaHoraCorrida >= @d0 AND r.FechaHoraCorrida < @d1
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
                        AND FechaEvento >= @d0 AND FechaEvento < @d1
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
                  WHERE Eliminado = 0 AND Fecha >= @d0 AND Fecha < @d1", cn) { CommandTimeout = 60 })
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

        var html = Render(desde, hasta, pedidos, remitos, refuerzos, reemp);
        var totalRecibidos = remitos.Sum(r => r.Recibidos);
        var totalGenerados = remitos.Sum(r => r.Generados);
        var resumen = $"repo pidió {pedidos.Sum(p => p.Packs)} packs · {totalRecibidos}/{totalGenerados} remitos recibidos · {refuerzos.Sum(r => r.Packs)} packs de refuerzo · {reemp.Enviados}/{reemp.Cargados} reemplazos enviados";
        return (html, resumen);
    }

    private static string Render(DateTime desde, DateTime hasta, List<PedidoLocal> pedidos, List<RemitoLocal> remitos, List<RefuerzoLocal> refuerzos, ReempTotales reemp)
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
}
