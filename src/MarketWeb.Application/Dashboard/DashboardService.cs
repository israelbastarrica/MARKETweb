using System.Text.RegularExpressions;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Dashboard;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Dashboard;

/// <summary>
/// Datos del dashboard (port de NoblexTV). La VENTA se lee de COMPROBANTEV del local
/// EN VIVO por OPENQUERY (fallback a réplica). La REPOSICIÓN se lee de lo que CENTRAL
/// (DRAGONFISH_CENTRAL + DRAGONFISH_CCENTRAL) le ENVIÓ al local — remitos FLETRA='R',
/// FCLIENTE=local — agrupada por DÍA OPERATIVO 21→21 (corte 21:00 por FALTAFW+HALTAFW),
/// igual que el SP_RepoCalcularPacks. Devuelve la forma JSON que consume main.html.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly ISqlConnectionFactory _db;
    public DashboardService(ISqlConnectionFactory db) => _db = db;

    private static string Fecha8(string f)
    {
        var d = Regex.Replace(f ?? "", @"[^0-9]", "");
        return d.Length == 8 ? d : DateTime.Today.ToString("yyyyMMdd");
    }

    private static string DdMm(string yyyymmdd)
        => yyyymmdd.Length == 8 ? $"{yyyymmdd.Substring(6, 2)}/{yyyymmdd.Substring(4, 2)}" : yyyymmdd;

    private sealed record HoraRow(string HoraReloj, decimal TotalMonto, int CantidadTickets, decimal CantidadPrendas);
    private sealed record CajeroRow(string NombreCajero, int Tickets);
    private sealed record ArtRow(string Codigo, string Descripcion, decimal Cantidad, decimal Monto);
    private sealed record DiaVentaRow(string Dia, decimal Venta);
    private sealed record TipoVentaRow(string Codigo, string Descripcion, decimal Venta);
    private sealed record RepoRow(string OpDay, string TipoCod, string TipoDesc, decimal Cant);

    private sealed class VentaLocal
    {
        public Dictionary<string, object> PorHora = new();
        public Dictionary<string, object> Totales = new() { ["monto"] = 0m, ["tickets"] = 0, ["prendas"] = 0m };
        public Dictionary<string, int> PorCajero = new();
        public Dictionary<string, ArtRow> Articulos = new();
        public Dictionary<string, ArtRow> TiposVenta = new();
        public Dictionary<string, decimal> VentaPorDia = new();   // yyyymmdd -> unidades vendidas
        public Dictionary<string, TipoVentaRow> VentaPorTipo = new(); // COD -> venta por rubro
    }

    // Ejecuta una consulta contra el LOCAL: VIVO por OPENQUERY, fallback a réplica.
    private static async Task<List<T>> RunLocalAsync<T>(SqlConnection cn, string local, string innerSql, CancellationToken ct)
    {
        var linked = local == "LURO" ? "marketluro.ddns.net" : "marketperalta.ddns.net";
        try
        {
            var live = $"SELECT * FROM OPENQUERY([{linked}], '{innerSql.Replace("'", "''")}')";
            return (await cn.QueryAsync<T>(new CommandDefinition(live, cancellationToken: ct))).ToList();
        }
        catch
        {
            return (await cn.QueryAsync<T>(new CommandDefinition(innerSql, cancellationToken: ct))).ToList();
        }
    }

    // ---- VENTA del local (en vivo) ----
    private async Task<VentaLocal> VentaLocalAsync(SqlConnection cn, string local, string hoy, string inicio, CancellationToken ct)
    {
        var b = "DRAGONFISH_" + local;
        var d = new VentaLocal();

        var qHoras = $"SELECT HoraReloj = LEFT(COMP.HALTAFW, 2), TotalMonto = SUM(DET.MNTPTOT * COMP.SIGNOMOV), CantidadTickets = COUNT(DISTINCT COMP.CODIGO), CantidadPrendas = SUM(DET.FCANT * COMP.SIGNOMOV) FROM {b}.Zoologic.COMPROBANTEV COMP INNER JOIN {b}.Zoologic.COMPROBANTEVDET DET ON COMP.CODIGO = DET.CODIGO WHERE COMP.ANULADO=0 AND COMP.FFCH='{hoy}' AND LEFT(DET.FART, 1) <> 'Z' AND COMP.FLETRA <> 'R' GROUP BY LEFT(COMP.HALTAFW, 2)";
        foreach (var r in await RunLocalAsync<HoraRow>(cn, local, qHoras, ct))
        {
            d.PorHora[r.HoraReloj] = new Dictionary<string, object> { ["monto"] = r.TotalMonto, ["tickets"] = r.CantidadTickets, ["prendas"] = r.CantidadPrendas };
            d.Totales["monto"] = (decimal)d.Totales["monto"] + r.TotalMonto;
            d.Totales["tickets"] = (int)d.Totales["tickets"] + r.CantidadTickets;
            d.Totales["prendas"] = (decimal)d.Totales["prendas"] + r.CantidadPrendas;
        }

        var qCaj = $"SELECT NombreCajero = ISNULL(V.CLNOM, COMP.FVEN), Tickets = COUNT(DISTINCT COMP.CODIGO) FROM {b}.Zoologic.COMPROBANTEV COMP LEFT JOIN {b}.Zoologic.VEN V ON COMP.FVEN = V.CLCOD WHERE COMP.ANULADO=0 AND COMP.FFCH='{hoy}' AND COMP.FLETRA <> 'R' GROUP BY V.CLNOM, COMP.FVEN";
        foreach (var r in await RunLocalAsync<CajeroRow>(cn, local, qCaj, ct))
            d.PorCajero[(r.NombreCajero ?? "").Trim()] = r.Tickets;

        var qArt = $"SELECT Codigo = DET.FART, Descripcion = MAX(DET.FTXT), Cantidad = SUM(DET.FCANT * COMP.SIGNOMOV), Monto = SUM(DET.MNTPTOT * COMP.SIGNOMOV) FROM {b}.Zoologic.COMPROBANTEV COMP INNER JOIN {b}.Zoologic.COMPROBANTEVDET DET ON COMP.CODIGO = DET.CODIGO WHERE COMP.ANULADO=0 AND COMP.FFCH='{hoy}' AND LEFT(DET.FART, 1) <> 'Z' AND COMP.FLETRA <> 'R' GROUP BY DET.FART HAVING SUM(DET.MNTPTOT * COMP.SIGNOMOV) > 0";
        foreach (var r in await RunLocalAsync<ArtRow>(cn, local, qArt, ct))
            d.Articulos[(r.Codigo ?? "").Trim()] = r;

        var qTV = $"SELECT Codigo = TIPO.COD, Descripcion = MAX(TIPO.DESCRIP), Cantidad = SUM(DET.FCANT * COMP.SIGNOMOV), Monto = SUM(DET.MNTPTOT * COMP.SIGNOMOV) FROM {b}.Zoologic.COMPROBANTEV COMP INNER JOIN {b}.Zoologic.COMPROBANTEVDET DET ON COMP.CODIGO = DET.CODIGO INNER JOIN {b}.Zoologic.ART ART ON DET.FART = ART.ARTCOD INNER JOIN {b}.Zoologic.TIPOART TIPO ON ART.TIPOARTI = TIPO.COD WHERE COMP.ANULADO=0 AND COMP.FFCH='{hoy}' AND LEFT(DET.FART, 1) <> 'Z' AND COMP.FLETRA <> 'R' GROUP BY TIPO.COD HAVING SUM(DET.MNTPTOT * COMP.SIGNOMOV) > 0";
        foreach (var r in await RunLocalAsync<ArtRow>(cn, local, qTV, ct))
            d.TiposVenta[(r.Codigo ?? "").Trim()] = r;

        // Venta por día (calendario, día de negocio del local) — solo venta, sin remitos.
        var qDia = $"SELECT Dia = CONVERT(VARCHAR(8), COMP.FFCH, 112), Venta = SUM(DET.FCANT * COMP.SIGNOMOV) FROM {b}.Zoologic.COMPROBANTEV COMP INNER JOIN {b}.Zoologic.COMPROBANTEVDET DET ON COMP.CODIGO = DET.CODIGO WHERE COMP.ANULADO=0 AND COMP.FFCH >= '{inicio}' AND COMP.FFCH <= '{hoy}' AND LEFT(DET.FART, 1) <> 'Z' AND COMP.FLETRA <> 'R' GROUP BY CONVERT(VARCHAR(8), COMP.FFCH, 112)";
        foreach (var r in await RunLocalAsync<DiaVentaRow>(cn, local, qDia, ct))
            d.VentaPorDia[r.Dia] = r.Venta;

        // Venta por rubro (TIPOART) en la ventana de 7 días — solo venta.
        var qTipo = $"SELECT Codigo = TIPO.COD, Descripcion = MAX(TIPO.DESCRIP), Venta = SUM(DET.FCANT * COMP.SIGNOMOV) FROM {b}.Zoologic.COMPROBANTEV COMP INNER JOIN {b}.Zoologic.COMPROBANTEVDET DET ON COMP.CODIGO = DET.CODIGO INNER JOIN {b}.Zoologic.ART ART ON DET.FART = ART.ARTCOD INNER JOIN {b}.Zoologic.TIPOART TIPO ON ART.TIPOARTI = TIPO.COD WHERE COMP.ANULADO=0 AND COMP.FFCH >= '{inicio}' AND COMP.FFCH <= '{hoy}' AND LEFT(DET.FART, 1) <> 'Z' AND COMP.FLETRA <> 'R' GROUP BY TIPO.COD";
        foreach (var r in await RunLocalAsync<TipoVentaRow>(cn, local, qTipo, ct))
            d.VentaPorTipo[(r.Codigo ?? "").Trim()] = r;

        return d;
    }

    // ---- REPOSICIÓN enviada desde CENTRAL al local (directo, día operativo 21→21) ----
    // Une DRAGONFISH_CENTRAL + DRAGONFISH_CCENTRAL; mismo criterio que SP_RepoCalcularPacks
    // (remitos FLETRA='R', FCLIENTE=local) PERO excluye Z (bolsas, no se reponen).
    // Día operativo D = [21:00 de D-1, 21:00 de D): timestamp FALTAFW+HALTAFW + 3h, casteado a DATE.
    private async Task<List<RepoRow>> RepoCentralAsync(SqlConnection cn, string local, string hoy, string inicio, CancellationToken ct)
    {
        // FALTAFW se consulta desde inicio-1 (la noche previa, post 21:00, cae en el día operativo 'inicio').
        var faMin = DateTime.ParseExact(inicio, "yyyyMMdd", null).AddDays(-1).ToString("yyyyMMdd");
        var loc = local.ToUpperInvariant();

        string Rama(string baseDb) =>
            $@"SELECT
                  OpDay = CONVERT(VARCHAR(8), CAST(DATEADD(HOUR, 3, COMP.FALTAFW + CAST(ISNULL(NULLIF(COMP.HALTAFW, ''), '00:00:00') AS DATETIME)) AS DATE), 112),
                  TipoCod = ISNULL(RTRIM(TIPO.COD), '?'),
                  TipoDesc = ISNULL(MAX(TIPO.DESCRIP), 'Sin rubro'),
                  Cant = SUM(DET.FCANT * COMP.SIGNOMOV)
               FROM {baseDb}.Zoologic.COMPROBANTEV COMP
               INNER JOIN {baseDb}.Zoologic.COMPROBANTEVDET DET ON COMP.CODIGO = DET.CODIGO
               LEFT JOIN {baseDb}.Zoologic.ART ART ON DET.FART = ART.ARTCOD
               LEFT JOIN {baseDb}.Zoologic.TIPOART TIPO ON ART.TIPOARTI = TIPO.COD
               WHERE COMP.FLETRA = 'R' AND COMP.ANULADO = 0 AND UPPER(RTRIM(COMP.FCLIENTE)) = '{loc}'
                 AND LEFT(DET.FART, 1) <> 'Z'   -- Z = bolsas, no se reponen
                 AND COMP.FALTAFW >= '{faMin}' AND COMP.FALTAFW <= '{hoy}'
               GROUP BY CONVERT(VARCHAR(8), CAST(DATEADD(HOUR, 3, COMP.FALTAFW + CAST(ISNULL(NULLIF(COMP.HALTAFW, ''), '00:00:00') AS DATETIME)) AS DATE), 112), ISNULL(RTRIM(TIPO.COD), '?')";

        var sql = $@"
            ;WITH R AS (
                {Rama("DRAGONFISH_CENTRAL")}
                UNION ALL
                {Rama("DRAGONFISH_CCENTRAL")}
            )
            SELECT OpDay, TipoCod, TipoDesc = MAX(TipoDesc), Cant = SUM(Cant)
            FROM R
            WHERE OpDay >= '{inicio}' AND OpDay <= '{hoy}'
            GROUP BY OpDay, TipoCod";

        return (await cn.QueryAsync<RepoRow>(new CommandDefinition(sql, commandTimeout: 120, cancellationToken: ct))).ToList();
    }

    public async Task<DashboardVentasMobileDto> GetResumenMobileAsync(string fecha, string rol, string? local, CancellationToken ct = default)
    {
        var hoy = Fecha8(fecha);
        var dt = DateTime.ParseExact(hoy, "yyyyMMdd", null);
        var inicio = dt.AddDays(-6).ToString("yyyyMMdd");
        var fechaSqlDate = dt.ToString("yyyy-MM-dd");

        // Corte para "proyectado hasta ahora": si es hoy, la hora actual; si es un día pasado, el día completo.
        var esHoy = hoy == DateTime.Today.ToString("yyyyMMdd");
        var horaCorte = esHoy ? DateTime.Now.Hour : 23;

        using var cn = _db.Create();

        // Proyección de tickets por local y hora (tabla Ventas de MARKET central).
        var proy = new Dictionary<string, Dictionary<int, int>> { ["LURO"] = new(), ["PERALTA"] = new() };
        try
        {
            var rows = await cn.QueryAsync($"SELECT Local, Fecha_Hora, PrediccionTickets FROM Ventas WHERE CAST(Fecha_Hora AS DATE) = '{fechaSqlDate}'");
            foreach (var r in rows)
            {
                string l = ((string)(r.Local ?? "")).Trim().ToUpperInvariant();
                if (!proy.ContainsKey(l)) continue;
                var fh = (DateTime)r.Fecha_Hora;
                proy[l][fh.Hour] = r.PrediccionTickets is null ? 0 : Convert.ToInt32(r.PrediccionTickets);
            }
        }
        catch { /* sin proyecciones */ }

        // Solo ADMIN ve plata. Al cajero ni se le manda el monto (ni top de productos
        // con importes): la plata es exclusiva de administración.
        var conPlata = rol == "admin";

        var locales = new List<VentaLocalResumenDto>();
        if (rol == "cajero" && !string.IsNullOrEmpty(local))
            locales.Add(await ResumenLocalAsync(cn, local, hoy, inicio, proy.GetValueOrDefault(local.ToUpperInvariant()), horaCorte, conPlata, ct));
        else
        {
            locales.Add(await ResumenLocalAsync(cn, "LURO", hoy, inicio, proy["LURO"], horaCorte, conPlata, ct));
            locales.Add(await ResumenLocalAsync(cn, "PERALTA", hoy, inicio, proy["PERALTA"], horaCorte, conPlata, ct));
        }

        return new DashboardVentasMobileDto
        {
            Role = rol,
            Locales = locales,
            Actualizado = DateTime.Now.ToString("HH:mm")
        };
    }

    private async Task<VentaLocalResumenDto> ResumenLocalAsync(
        SqlConnection cn, string local, string hoy, string inicio,
        Dictionary<int, int>? proyHoras, int horaCorte, bool conPlata, CancellationToken ct)
    {
        var v = await VentaLocalAsync(cn, local, hoy, inicio, ct);

        int? proyDia = null, proyAhora = null;
        if (proyHoras is { Count: > 0 })
        {
            proyDia = proyHoras.Values.Sum();
            proyAhora = proyHoras.Where(kv => kv.Key <= horaCorte).Sum(kv => kv.Value);
        }

        return new VentaLocalResumenDto
        {
            Local = local,
            // Sin plata para no-admin: el monto no se transmite (queda en 0).
            Monto = conPlata ? (decimal)v.Totales["monto"] : 0,
            Tickets = (int)v.Totales["tickets"],
            Prendas = (decimal)v.Totales["prendas"],
            TopArticulos = conPlata
                ? v.Articulos.Values.OrderByDescending(a => a.Monto).Take(5)
                    .Select(a => new TopArticuloDto
                    {
                        Descripcion = string.IsNullOrWhiteSpace(a.Descripcion) ? a.Codigo : a.Descripcion.Trim(),
                        Cantidad = a.Cantidad,
                        Monto = a.Monto
                    }).ToList()
                : new List<TopArticuloDto>(),
            Cajeros = v.PorCajero.OrderByDescending(c => c.Value)
                .Select(c => new CajeroTicketsDto { Nombre = c.Key, Tickets = c.Value }).ToList(),
            ProyTicketsDia = proyDia,
            ProyTicketsAhora = proyAhora
        };
    }

    public async Task<object> GetVentasAsync(string fecha, string rol, string? local, CancellationToken ct = default)
    {
        var hoy = Fecha8(fecha);
        var dt = DateTime.ParseExact(hoy, "yyyyMMdd", null);
        var inicio = dt.AddDays(-6).ToString("yyyyMMdd");
        var dias = Enumerable.Range(0, 7).Select(i => dt.AddDays(-6 + i).ToString("yyyyMMdd")).ToList();
        var fechaSqlDate = dt.ToString("yyyy-MM-dd");

        using var cn = _db.Create();

        // Proyecciones (MARKET central, directo)
        var proy = new Dictionary<string, Dictionary<string, int>> { ["LURO"] = new(), ["PERALTA"] = new() };
        try
        {
            var rows = await cn.QueryAsync($"SELECT Local, Fecha_Hora, PrediccionTickets FROM Ventas WHERE CAST(Fecha_Hora AS DATE) = '{fechaSqlDate}'");
            foreach (var r in rows)
            {
                string l = ((string)(r.Local ?? "")).Trim().ToUpperInvariant();
                if (!proy.ContainsKey(l)) continue;
                var fh = (DateTime)r.Fecha_Hora;
                proy[l][fh.ToString("HH")] = r.PrediccionTickets is null ? 0 : Convert.ToInt32(r.PrediccionTickets);
            }
        }
        catch { /* sin proyecciones */ }

        if (rol == "cajero" && !string.IsNullOrEmpty(local))
        {
            var v = await VentaLocalAsync(cn, local, hoy, inicio, ct);
            AplicarProyeccion(v.PorHora, proy.GetValueOrDefault(local) ?? new());
            return new Dictionary<string, object?>
            {
                ["role"] = "cajero",
                ["local"] = local,
                ["por_hora"] = v.PorHora,
                ["totales_dia"] = v.Totales,
                ["por_cajero"] = v.PorCajero
            };
        }

        // admin → LURO + PERALTA
        var vL = await VentaLocalAsync(cn, "LURO", hoy, inicio, ct);
        var vP = await VentaLocalAsync(cn, "PERALTA", hoy, inicio, ct);
        var rL = await RepoCentralAsync(cn, "LURO", hoy, inicio, ct);
        var rP = await RepoCentralAsync(cn, "PERALTA", hoy, inicio, ct);
        AplicarProyeccion(vL.PorHora, proy["LURO"]);
        AplicarProyeccion(vP.PorHora, proy["PERALTA"]);

        var (dias7L, tot7L) = Construir7Dias(dias, vL.VentaPorDia, rL);
        var (dias7P, tot7P) = Construir7Dias(dias, vP.VentaPorDia, rP);

        return new Dictionary<string, object?>
        {
            ["role"] = "admin",
            ["por_hora"] = new Dictionary<string, object> { ["LURO"] = vL.PorHora, ["PERALTA"] = vP.PorHora },
            ["totales_dia"] = new Dictionary<string, object> { ["LURO"] = vL.Totales, ["PERALTA"] = vP.Totales },
            ["por_cajero"] = new Dictionary<string, object> { ["LURO"] = vL.PorCajero, ["PERALTA"] = vP.PorCajero },
            ["top_articulos"] = TopMerge(vL.Articulos, vP.Articulos),
            ["top_tipos_venta"] = TopMerge(vL.TiposVenta, vP.TiposVenta),
            ["ultimos_7_dias"] = new Dictionary<string, object> { ["LURO"] = dias7L, ["PERALTA"] = dias7P },
            ["totales_7_dias"] = new Dictionary<string, object> { ["LURO"] = tot7L, ["PERALTA"] = tot7P },
            ["top_tipos_balance"] = TopMergeBalance(vL.VentaPorTipo, vP.VentaPorTipo, rL, rP)
        };
    }

    // Arma el balance 7 días: venta del local (por FFCH) vs repo de central (por día operativo 21→21).
    private static (Dictionary<string, object> dias, Dictionary<string, object> totales) Construir7Dias(
        List<string> dias, Dictionary<string, decimal> ventaPorDia, List<RepoRow> repo)
    {
        var repoPorDia = repo.GroupBy(r => r.OpDay).ToDictionary(g => g.Key, g => g.Sum(x => x.Cant));
        var outDias = new Dictionary<string, object>();
        decimal totV = 0, totR = 0, acum = 0;
        foreach (var d in dias)
        {
            var venta = ventaPorDia.GetValueOrDefault(d);
            var rep = repoPorDia.GetValueOrDefault(d);
            acum += rep - venta;
            totV += venta; totR += rep;
            outDias[DdMm(d)] = new Dictionary<string, object> { ["venta"] = venta, ["reposicion"] = rep, ["saldo_acumulado"] = acum };
        }
        var tot = new Dictionary<string, object> { ["venta"] = totV, ["reposicion"] = totR, ["saldo_final"] = acum };
        return (outDias, tot);
    }

    private static void AplicarProyeccion(Dictionary<string, object> porHora, Dictionary<string, int> proy)
    {
        foreach (var (hora, val) in proy)
        {
            if (porHora.TryGetValue(hora, out var o) && o is Dictionary<string, object> dic)
                dic["proyeccion"] = val;
            else
                porHora[hora] = new Dictionary<string, object> { ["monto"] = 0m, ["tickets"] = 0, ["prendas"] = 0m, ["proyeccion"] = val };
        }
    }

    private static List<object> TopMerge(params Dictionary<string, ArtRow>[] fuentes)
    {
        var u = new Dictionary<string, (string desc, decimal cant, decimal monto)>();
        foreach (var f in fuentes)
            foreach (var (k, v) in f)
            {
                if (u.TryGetValue(k, out var e)) u[k] = (e.desc, e.cant + v.Cantidad, e.monto + v.Monto);
                else u[k] = (string.IsNullOrWhiteSpace(v.Descripcion) ? "Sin desc." : v.Descripcion.Trim(), v.Cantidad, v.Monto);
            }
        return u.Values.OrderByDescending(x => x.monto).Take(15)
            .Select(x => (object)new Dictionary<string, object> { ["descripcion"] = x.desc, ["cantidad"] = x.cant, ["monto"] = x.monto }).ToList();
    }

    // Balance por rubro: venta (de los locales) vs reposición (de central), unificado por COD de TIPOART.
    private static List<object> TopMergeBalance(
        Dictionary<string, TipoVentaRow> ventaL, Dictionary<string, TipoVentaRow> ventaP,
        List<RepoRow> repoL, List<RepoRow> repoP)
    {
        var u = new Dictionary<string, (string desc, decimal venta, decimal repo)>();

        void AddVenta(Dictionary<string, TipoVentaRow> f)
        {
            foreach (var (k, v) in f)
            {
                var desc = string.IsNullOrWhiteSpace(v.Descripcion) ? k : v.Descripcion.Trim();
                if (u.TryGetValue(k, out var e)) u[k] = (e.desc == "" ? desc : e.desc, e.venta + v.Venta, e.repo);
                else u[k] = (desc, v.Venta, 0m);
            }
        }
        void AddRepo(List<RepoRow> rows)
        {
            foreach (var g in rows.GroupBy(r => r.TipoCod))
            {
                var k = g.Key;
                var rep = g.Sum(x => x.Cant);
                var desc = g.Select(x => x.TipoDesc).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? k;
                if (u.TryGetValue(k, out var e)) u[k] = (string.IsNullOrWhiteSpace(e.desc) ? desc : e.desc, e.venta, e.repo + rep);
                else u[k] = (desc, 0m, rep);
            }
        }

        AddVenta(ventaL); AddVenta(ventaP);
        AddRepo(repoL); AddRepo(repoP);

        return u.Values.OrderByDescending(x => x.venta + x.repo).Take(15)
            .Select(x => (object)new Dictionary<string, object> { ["descripcion"] = x.desc, ["venta"] = x.venta, ["reposicion"] = x.repo }).ToList();
    }

    // ===================== FICHADAS (port de obtener_fichadas) =====================

    private sealed record FichadaRow(int Id, string? Nombre, string? Oficina, string? Puesto, string? Color,
        string Status, string? Label, string? Hora, string? Origen, DateTime Fecha);
    private sealed record Marca(string Status, string? Label, string Hora, string Origen);
    private sealed class Legajo
    {
        public string Nombre = "";
        public string Local = "";
        public string Puesto = "Sin puesto";
        public string Color = "#8b949e";
        public List<Marca> Dia = new();
        public List<Marca> Sig = new();
    }

    public async Task<object> GetFichadasAsync(string fecha, string rol, string? local, CancellationToken ct = default)
    {
        var hoy = Fecha8(fecha);
        var dt = DateTime.ParseExact(hoy, "yyyyMMdd", null);
        var fSql = dt.ToString("yyyy-MM-dd");
        var fSig = dt.AddDays(1).ToString("yyyy-MM-dd");  // turnos noche que cruzan medianoche

        using var cn = _db.Create();

        // DNI normalizado en el join (Legajos.DNI es varchar con puntos/guiones; Fichadas.DNI es int).
        var sql = $@"
            SELECT Id = L.ID, Nombre = L.Nombre, Oficina = L.Oficina, Puesto = L.PuestoBizneo, Color = L.AvatarColor,
                   Status = F.AttendanceStatus, Label = F.Label, Hora = CONVERT(VARCHAR(5), F.Hora, 108),
                   Origen = F.Origen, Fecha = F.Fecha
            FROM RRHHLegajos L
            INNER JOIN RRHHFichadas F
              ON REPLACE(REPLACE(L.DNI, '.', ''), '-', '') = REPLACE(REPLACE(F.DNI, '.', ''), '-', '')
             AND (F.Fecha = '{fSql}' OR F.Fecha = '{fSig}')
            WHERE L.Eliminado = 0 AND ISNULL(L.idNaaloo, 0) > 0
            ORDER BY L.Oficina, L.PuestoBizneo, L.Nombre, F.Fecha ASC, F.Hora ASC";

        var rows = (await cn.QueryAsync<FichadaRow>(new CommandDefinition(sql, cancellationToken: ct))).ToList();

        var legajos = new Dictionary<int, Legajo>();
        foreach (var r in rows)
        {
            if (!legajos.TryGetValue(r.Id, out var leg))
            {
                leg = new Legajo
                {
                    Nombre = (r.Nombre ?? "").Trim(),
                    Local = (r.Oficina ?? "").Trim().ToUpperInvariant(),
                    Puesto = string.IsNullOrWhiteSpace(r.Puesto) ? "Sin puesto" : r.Puesto.Trim(),
                    Color = string.IsNullOrWhiteSpace(r.Color) ? "#8b949e" : r.Color
                };
                legajos[r.Id] = leg;
            }
            var marca = new Marca(r.Status, r.Label, r.Hora ?? "", (r.Origen ?? "").Trim());
            if (r.Fecha.ToString("yyyy-MM-dd") == fSql) leg.Dia.Add(marca);
            else leg.Sig.Add(marca);
        }

        var resultado = new Dictionary<string, Dictionary<string, List<Dictionary<string, object?>>>>
        {
            ["LURO"] = new(),
            ["PERALTA"] = new()
        };

        foreach (var leg in legajos.Values)
        {
            // Último checkIn del día = inicio del turno vigente. Sin checkIn → solo cierre de turno previo, se omite.
            int idx = -1;
            for (int i = 0; i < leg.Dia.Count; i++)
                if (leg.Dia[i].Status == "checkIn") idx = i;
            if (idx == -1) continue;

            // Turno = desde el último checkIn + marcas del día siguiente hasta el próximo checkIn.
            var turno = new List<Marca>();
            for (int i = idx; i < leg.Dia.Count; i++) turno.Add(leg.Dia[i]);
            foreach (var m in leg.Sig)
            {
                if (m.Status == "checkIn") break;
                turno.Add(m);
            }

            var ingreso = turno[0].Hora;
            Marca? salida = null;
            for (int i = turno.Count - 1; i >= 0; i--)
                if (turno[i].Status == "checkOut") { salida = turno[i]; break; }

            bool salidaAuto = false;
            if (salida is not null)
            {
                var origen = (salida.Origen ?? "").ToLowerInvariant().Replace("-", " ");
                var label = (salida.Label ?? "").ToLowerInvariant();
                salidaAuto = origen.Contains("cierre automatico") || origen.Contains("cierre autom")
                          || label.Contains("cierre automatico") || label.Contains("cierre autom");
            }

            var ultima = turno[^1];
            string estado;
            string? inicioDescanso = null;
            if (ultima.Status == "checkOut") estado = "ido";
            else if (ultima.Status == "breakOut") { estado = "descanso"; inicioDescanso = ultima.Hora; }
            else estado = "trabajando";

            // Descansos cerrados (pares breakOut → breakIn) del turno.
            var descansos = new List<Dictionary<string, object?>>();
            string? ini = null;
            foreach (var m in turno)
            {
                if (m.Status == "breakOut") ini = m.Hora;
                else if (m.Status == "breakIn" && ini is not null)
                {
                    int? minutos = null;
                    try
                    {
                        var p1 = ini.Split(':'); var p2 = m.Hora.Split(':');
                        int min = (int.Parse(p2[0]) * 60 + int.Parse(p2[1])) - (int.Parse(p1[0]) * 60 + int.Parse(p1[1]));
                        if (min < 0) min += 24 * 60;
                        minutos = min;
                    }
                    catch { }
                    descansos.Add(new Dictionary<string, object?> { ["inicio"] = ini, ["fin"] = m.Hora, ["minutos"] = minutos });
                    ini = null;
                }
            }

            if (!resultado.TryGetValue(leg.Local, out var puestos)) continue; // solo LURO/PERALTA
            var legObj = new Dictionary<string, object?>
            {
                ["nombre"] = leg.Nombre,
                ["estado"] = estado,
                ["ingreso"] = ingreso,
                ["salida"] = salida?.Hora,
                ["salida_auto"] = salidaAuto,
                ["sin_salida"] = false,
                ["inicio_descanso"] = inicioDescanso,
                ["descansos"] = descansos
            };
            if (!puestos.TryGetValue(leg.Puesto, out var lista)) { lista = new(); puestos[leg.Puesto] = lista; }
            lista.Add(legObj);
        }

        // Ordenar cada puesto por nombre.
        foreach (var puestos in resultado.Values)
            foreach (var puesto in puestos.Keys.ToList())
                puestos[puesto] = puestos[puesto].OrderBy(o => (string)(o["nombre"] ?? "")).ToList();

        if (rol == "cajero" && !string.IsNullOrEmpty(local))
        {
            var locU = local.ToUpperInvariant();
            return new Dictionary<string, object?>
            {
                ["role"] = "cajero",
                ["local"] = locU,
                ["fichadas"] = new Dictionary<string, object?> { [locU] = resultado.GetValueOrDefault(locU) ?? new() }
            };
        }

        return new Dictionary<string, object?>
        {
            ["role"] = "admin",
            ["fichadas"] = new Dictionary<string, object?> { ["LURO"] = resultado["LURO"], ["PERALTA"] = resultado["PERALTA"] }
        };
    }
}
