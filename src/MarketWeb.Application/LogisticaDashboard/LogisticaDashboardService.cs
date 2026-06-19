using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.LogisticaDashboard;

namespace MarketWeb.Application.LogisticaDashboard;

public sealed class LogisticaDashboardService : ILogisticaDashboardService
{
    private readonly ISqlConnectionFactory _db;
    private readonly EstancadosCache _estancados;
    private readonly BackgroundCache<List<ArticuloUbicacionesDto>> _masUbic;
    private readonly BackgroundCache<ReposFast> _reposFast;
    private readonly BackgroundCache<Dictionary<string, RepoAbastDto>> _abast;
    public LogisticaDashboardService(
        ISqlConnectionFactory db,
        EstancadosCache estancados,
        BackgroundCache<List<ArticuloUbicacionesDto>> masUbic,
        BackgroundCache<ReposFast> reposFast,
        BackgroundCache<Dictionary<string, RepoAbastDto>> abast)
    {
        _db = db;
        _estancados = estancados;
        _masUbic = masUbic;
        _reposFast = reposFast;
        _abast = abast;
    }

    private const int JornadaHoraInicio = 21;

    private static (DateTime inicio, DateTime fin) JornadaActual()
    {
        var now = DateTime.Now;
        var baseDia = now.Hour >= JornadaHoraInicio ? now : now.AddDays(-1);
        var inicio = new DateTime(baseDia.Year, baseDia.Month, baseDia.Day, JornadaHoraInicio, 0, 0);
        return (inicio, now);
    }

    private static string LocalDeId(int id) => id == 2 ? "LURO" : id == 3 ? "PERALTA" : "?";

    public async Task<PanelDespachoRecepcionDto> GetPanelDespachoRecepcionAsync(CancellationToken ct = default)
    {
        var (inicio, fin) = JornadaActual();
        using var cn = _db.Create();

        return new PanelDespachoRecepcionDto
        {
            Despacho = await DespachoAsync(cn, inicio, fin, ct),
            Recepcion = await RecepcionQrAsync(cn, inicio, fin, ct),
            Ventana = $"{inicio:dd/MM HH:mm} → {fin:HH:mm}",
            Actualizado = fin.ToString("HH:mm:ss")
        };
    }

    // ---- DESPACHO: CENTRAL → local ----
    private sealed record CruceRow(int IdLocal, int Despachados, int Recibidos, int CircuitoOk);
    private sealed record IdNum(int IdLocal, int N);
    private sealed record AnulRow(string Local, int Anulados);

    private async Task<List<DespachoLocalDto>> DespachoAsync(Microsoft.Data.SqlClient.SqlConnection cn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        const string sqlCruce = """
            WITH D AS (
                SELECT DISTINCT IDLocalDestino AS id_local, LTRIM(RTRIM(CODIGO)) AS CODIGO
                FROM dbo.RemitosDespachados rd
                WHERE ISNULL(rd.Eliminado, 0) = 0 AND rd.FechaEscaneo >= @inicio AND rd.FechaEscaneo < @fin
                  AND rd.IDLocalDestino IN (2, 3)
                  AND NOT EXISTS (SELECT 1 FROM dbo.RemitosEscaneados p
                      WHERE LTRIM(RTRIM(p.CODIGO)) = LTRIM(RTRIM(rd.CODIGO)) AND p.IDLocal = rd.IDLocalDestino
                        AND p.EsDesconocido = 0 AND p.FechaEscaneo < @inicio)),
            E AS (
                SELECT DISTINCT R.IDLocal AS id_local, LTRIM(RTRIM(R.CODIGO)) AS CODIGO
                FROM dbo.RemitosEscaneados R
                WHERE R.EsDesconocido = 0 AND R.FechaEscaneo >= @inicio AND R.FechaEscaneo < @fin AND R.IDLocal IN (2, 3)),
            locales AS (SELECT 2 AS id_local UNION ALL SELECT 3)
            SELECT l.id_local AS IdLocal,
                   ISNULL((SELECT COUNT(*) FROM D WHERE D.id_local = l.id_local), 0) AS Despachados,
                   ISNULL((SELECT COUNT(*) FROM E WHERE E.id_local = l.id_local), 0) AS Recibidos,
                   ISNULL((SELECT COUNT(*) FROM D INNER JOIN E ON D.CODIGO = E.CODIGO AND D.id_local = E.id_local
                           WHERE D.id_local = l.id_local), 0) AS CircuitoOk
            FROM locales l;
            """;
        var cruce = (await cn.QueryAsync<CruceRow>(new CommandDefinition(sqlCruce, new { inicio, fin }, cancellationToken: ct))).ToList();

        const string sqlDem = """
            SELECT IDLocal AS IdLocal, DATEDIFF(MINUTE, MIN(FechaEscaneo), MAX(FechaEscaneo)) AS N
            FROM dbo.RemitosEscaneados WHERE EsDesconocido = 0 AND FechaEscaneo >= @inicio AND FechaEscaneo < @fin AND IDLocal IN (2, 3)
            GROUP BY IDLocal;
            """;
        var demora = (await cn.QueryAsync<IdNum>(new CommandDefinition(sqlDem, new { inicio, fin }, cancellationToken: ct))).ToDictionary(r => r.IdLocal, r => r.N);

        const string sqlAnul = """
            SELECT UPPER(RTRIM(IR.LocalDestino)) AS Local, SUM(CASE WHEN CV.ANULADO = 1 THEN 1 ELSE 0 END) AS Anulados
            FROM dbo.ImpresorRemito_Cola IR
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV ON CV.CODIGO = IR.RemitoCODIGO
            WHERE IR.FechaEmision >= @inicio AND IR.FechaEmision < @fin AND UPPER(RTRIM(IR.LocalOrigen)) = 'CENTRAL'
            GROUP BY UPPER(RTRIM(IR.LocalDestino));
            """;
        var anul = (await cn.QueryAsync<AnulRow>(new CommandDefinition(sqlAnul, new { inicio, fin }, cancellationToken: ct))).ToDictionary(r => r.Local, r => r.Anulados, StringComparer.OrdinalIgnoreCase);

        const string sqlQr = """
            SELECT E.IDLocal AS IdLocal, COUNT(DISTINCT LTRIM(RTRIM(E.CODIGO))) AS N
            FROM dbo.RemitosEscaneados E
            INNER JOIN (SELECT DISTINCT LTRIM(RTRIM(RemitoCODIGO)) AS CODIGO FROM dbo.RemitoQRGenerado_Log) lg ON lg.CODIGO = LTRIM(RTRIM(E.CODIGO))
            WHERE E.FechaEscaneo >= @inicio AND E.FechaEscaneo < @fin AND E.IDLocal IN (2, 3) AND E.EsDesconocido = 0
            GROUP BY E.IDLocal;
            """;
        var qr = (await cn.QueryAsync<IdNum>(new CommandDefinition(sqlQr, new { inicio, fin }, cancellationToken: ct))).ToDictionary(r => r.IdLocal, r => r.N);

        // Doble despacho: CENTRAL volvió a despachar algo que el local ya tenía (recibido antes del inicio).
        const string sqlDoble = """
            WITH d AS (SELECT DISTINCT LTRIM(RTRIM(CODIGO)) AS CODIGO, IDLocalDestino FROM dbo.RemitosDespachados
                       WHERE ISNULL(Eliminado, 0) = 0 AND FechaEscaneo >= @inicio AND FechaEscaneo < @fin AND IDLocalDestino IN (2, 3)),
                 rp AS (SELECT DISTINCT LTRIM(RTRIM(CODIGO)) AS CODIGO, IDLocal FROM dbo.RemitosEscaneados
                        WHERE EsDesconocido = 0 AND IDLocal IN (2, 3) AND FechaEscaneo < @inicio)
            SELECT d.IDLocalDestino AS IdLocal, COUNT(*) AS N FROM d INNER JOIN rp ON rp.CODIGO = d.CODIGO AND rp.IDLocal = d.IDLocalDestino
            GROUP BY d.IDLocalDestino;
            """;
        var doble = (await cn.QueryAsync<IdNum>(new CommandDefinition(sqlDoble, new { inicio, fin }, cancellationToken: ct))).ToDictionary(r => r.IdLocal, r => r.N);

        var lista = new List<DespachoLocalDto>();
        foreach (var c in cruce.OrderBy(x => x.IdLocal))
        {
            var loc = LocalDeId(c.IdLocal);
            var enTransito = c.Despachados - c.CircuitoOk;
            var sinSalida = c.Recibidos - c.CircuitoOk;
            var universo = c.CircuitoOk + enTransito + sinSalida;
            lista.Add(new DespachoLocalDto
            {
                Local = loc,
                Despachados = c.Despachados, Recibidos = c.Recibidos, CircuitoOk = c.CircuitoOk,
                EnTransito = enTransito, SinSalida = sinSalida,
                Fantasmas = Math.Max(0, c.Recibidos - c.Despachados),
                DobleDespacho = doble.TryGetValue(c.IdLocal, out var db) ? db : 0,
                PctCircuito = universo > 0 ? Math.Round((double)c.CircuitoOk / universo * 100, 1) : 0,
                DemoraMin = demora.TryGetValue(c.IdLocal, out var dm) ? dm : null,
                Anulados = anul.TryGetValue(loc, out var an) ? an : 0,
                QrPc = qr.TryGetValue(c.IdLocal, out var q) ? q : 0
            });
        }
        if (lista.Count > 0)
        {
            var circ = lista.Sum(x => x.CircuitoOk); var tra = lista.Sum(x => x.EnTransito); var sin = lista.Sum(x => x.SinSalida);
            var uni = circ + tra + sin;
            var desp = lista.Sum(x => x.Despachados); var rec = lista.Sum(x => x.Recibidos);
            lista.Add(new DespachoLocalDto
            {
                Local = "TOTAL",
                Despachados = desp, Recibidos = rec, CircuitoOk = circ, EnTransito = tra, SinSalida = sin,
                Fantasmas = Math.Max(0, rec - desp), DobleDespacho = lista.Sum(x => x.DobleDespacho),
                PctCircuito = uni > 0 ? Math.Round((double)circ / uni * 100, 1) : 0,
                DemoraMin = null, Anulados = lista.Sum(x => x.Anulados), QrPc = lista.Sum(x => x.QrPc)
            });
        }
        return lista;
    }

    // ---- RECEPCIÓN: local → CENTRAL vía QR (MARKET directo) ----
    private sealed record QrRow(string Local, int Despachados, int Recibidos, int Pendientes, double? DemoraMinProm);

    private async Task<List<RecepcionLocalDto>> RecepcionQrAsync(Microsoft.Data.SqlClient.SqlConnection cn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        const string sql = """
            WITH cola_qr AS (
                SELECT UPPER(RTRIM(LocalOrigen)) AS local, LTRIM(RTRIM(RemitoCODIGO)) AS CODIGO, FechaEmision
                FROM dbo.ImpresorRemito_Cola
                WHERE FechaEmision >= @inicio AND FechaEmision < @fin
                  AND UPPER(RTRIM(LocalOrigen)) IN ('LURO', 'PERALTA') AND UPPER(RTRIM(LocalDestino)) = 'CENTRAL'),
            recibidos AS (
                SELECT LTRIM(RTRIM(CODIGO)) AS CODIGO, MIN(FechaEscaneo) AS recep
                FROM dbo.RemitosEscaneados WHERE IDLocal = 1 AND EsDesconocido = 0 GROUP BY LTRIM(RTRIM(CODIGO))),
            cruce AS (
                SELECT c.local, c.FechaEmision, r.recep, CASE WHEN r.CODIGO IS NOT NULL THEN 1 ELSE 0 END AS recibido
                FROM cola_qr c LEFT JOIN recibidos r ON r.CODIGO = c.CODIGO)
            SELECT local AS Local, COUNT(*) AS Despachados, SUM(recibido) AS Recibidos, SUM(1 - recibido) AS Pendientes,
                   AVG(CAST(DATEDIFF(MINUTE, FechaEmision, COALESCE(recep, GETDATE())) AS float)) AS DemoraMinProm
            FROM cruce GROUP BY local;
            """;
        var rows = (await cn.QueryAsync<QrRow>(new CommandDefinition(sql, new { inicio, fin }, cancellationToken: ct))).ToList();

        var lista = new List<RecepcionLocalDto>();
        foreach (var loc in new[] { "LURO", "PERALTA" })
        {
            var r = rows.FirstOrDefault(x => string.Equals(x.Local, loc, StringComparison.OrdinalIgnoreCase));
            var desp = r?.Despachados ?? 0; var rec = r?.Recibidos ?? 0;
            lista.Add(new RecepcionLocalDto
            {
                Local = loc, Despachados = desp, Recibidos = rec, Pendientes = r?.Pendientes ?? 0,
                PctLlegada = desp > 0 ? Math.Round((double)rec / desp * 100, 1) : 0,
                DemoraMinProm = r?.DemoraMinProm
            });
        }
        var dT = lista.Sum(x => x.Despachados); var rT = lista.Sum(x => x.Recibidos);
        lista.Add(new RecepcionLocalDto
        {
            Local = "TOTAL", Despachados = dT, Recibidos = rT, Pendientes = lista.Sum(x => x.Pendientes),
            PctLlegada = dT > 0 ? Math.Round((double)rT / dT * 100, 1) : 0
        });
        return lista;
    }

    // ===================== PANEL 2 · Pendientes en tránsito =====================

    private const int TopLocal = 200;   // techo para que los conteos por local sean exactos

    private const string ContenidoCte = """
        contenido AS (
            SELECT CODIGO, COUNT(DISTINCT RTRIM(FART)) AS n_arts, MIN(RTRIM(FART)) AS un_artcod
            FROM DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET
            WHERE LEFT(RTRIM(FART), 1) NOT IN ('Z', '9') AND (FTXT NOT LIKE '%BOLSA%' OR FTXT IS NULL)
            GROUP BY CODIGO)
        """;

    private sealed class PendRowRaw
    {
        public int FPTOVEN { get; set; }
        public int FNUMCOMP { get; set; }
        public int Minutos { get; set; }
        public string? Destino { get; set; }
        public string? DestinoReal { get; set; }
        public string? RecibidoEn { get; set; }
        public string? Origen { get; set; }
        public bool Despachado { get; set; }
        public int? NArts { get; set; }
        public string? UnArtcod { get; set; }
        public string? UnArtdes { get; set; }
        public DateTime? RecepFecha { get; set; }
    }

    private static string Nro(int pto, int num) => $"R {pto:D4}-{num:D8}";
    private static string? Contenido(int? n, string? cod, string? des) =>
        n == 1 ? $"{(string.IsNullOrWhiteSpace(cod) ? "—" : cod.Trim())} · {(string.IsNullOrWhiteSpace(des) ? "(sin desc.)" : des.Trim())}"
        : (n > 1 ? "REPOSICIÓN" : null);

    public async Task<PanelPendientesDto> GetPanelPendientesAsync(CancellationToken ct = default)
    {
        var (inicio, fin) = JornadaActual();
        using var cn = _db.Create();
        return new PanelPendientesDto
        {
            Cruzados = await CruzadosAsync(cn, inicio, fin, ct),
            DobleDespacho = await DobleAsync(cn, inicio, fin, ct),
            SinEscanear = await SinEscanearAsync(cn, inicio, fin, ct),
            SinSalida = await SinSalidaAsync(cn, inicio, fin, ct),
            Recepcion = await RecepcionPendAsync(cn, inicio, fin, ct),
            Ventana = $"{inicio:dd/MM HH:mm} → {fin:HH:mm}",
            Actualizado = fin.ToString("HH:mm:ss")
        };
    }

    private async Task<PendListaDto> CruzadosAsync(Microsoft.Data.SqlClient.SqlConnection cn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        var sql = $"""
            WITH D AS (SELECT LTRIM(RTRIM(CODIGO)) AS CODIGO, IDLocalDestino AS desp_local FROM dbo.RemitosDespachados
                       WHERE ISNULL(Eliminado,0)=0 AND FechaEscaneo>=@inicio AND FechaEscaneo<@fin AND IDLocalDestino IN (2,3)
                       GROUP BY LTRIM(RTRIM(CODIGO)), IDLocalDestino),
                 E AS (SELECT LTRIM(RTRIM(CODIGO)) AS CODIGO, IDLocal AS recep_local, MIN(FechaEscaneo) AS recep FROM dbo.RemitosEscaneados
                       WHERE FechaEscaneo>=@inicio AND FechaEscaneo<@fin AND IDLocal IN (2,3) GROUP BY LTRIM(RTRIM(CODIGO)), IDLocal),
                 cr AS (SELECT D.CODIGO, CASE D.desp_local WHEN 2 THEN 'LURO' WHEN 3 THEN 'PERALTA' END AS destino_real,
                               CASE E.recep_local WHEN 2 THEN 'LURO' WHEN 3 THEN 'PERALTA' END AS recibido_en,
                               DATEDIFF(MINUTE, E.recep, GETDATE()) AS minutos
                        FROM D INNER JOIN E ON D.CODIGO=E.CODIGO WHERE D.desp_local<>E.recep_local),
                 {ContenidoCte}
            SELECT CV.FPTOVEN AS FPTOVEN, CV.FNUMCOMP AS FNUMCOMP, cr.minutos AS Minutos,
                   cr.destino_real AS DestinoReal, cr.recibido_en AS RecibidoEn,
                   ct.n_arts AS NArts, ct.un_artcod AS UnArtcod, RTRIM(ART.ARTDES) AS UnArtdes
            FROM cr
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV ON CV.CODIGO=cr.CODIGO
            LEFT JOIN contenido ct ON ct.CODIGO=cr.CODIGO
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD)=ct.un_artcod AND ct.n_arts=1
            ORDER BY cr.minutos DESC;
            """;
        var rows = (await cn.QueryAsync<PendRowRaw>(new CommandDefinition(sql, new { inicio, fin }, commandTimeout: 60, cancellationToken: ct))).ToList();
        return new PendListaDto
        {
            Total = rows.Count,
            Items = rows.Select(r => new PendItemDto
            {
                NroRemito = Nro(r.FPTOVEN, r.FNUMCOMP), Minutos = r.Minutos,
                Contenido = Contenido(r.NArts, r.UnArtcod, r.UnArtdes),
                Local = r.RecibidoEn,
                Traza = $"era para {r.DestinoReal} · recibido en {r.RecibidoEn}"
            }).ToList()
        };
    }

    private async Task<PendListaDto> DobleAsync(Microsoft.Data.SqlClient.SqlConnection cn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        var sql = $"""
            WITH d AS (SELECT DISTINCT LTRIM(RTRIM(CODIGO)) AS CODIGO, IDLocalDestino,
                              CASE IDLocalDestino WHEN 2 THEN 'LURO' WHEN 3 THEN 'PERALTA' END AS destino, MIN(FechaEscaneo) AS desp
                       FROM dbo.RemitosDespachados WHERE ISNULL(Eliminado,0)=0 AND FechaEscaneo>=@inicio AND FechaEscaneo<@fin AND IDLocalDestino IN (2,3)
                       GROUP BY LTRIM(RTRIM(CODIGO)), IDLocalDestino),
                 rp AS (SELECT LTRIM(RTRIM(CODIGO)) AS CODIGO, IDLocal, MIN(FechaEscaneo) AS recep FROM dbo.RemitosEscaneados
                        WHERE EsDesconocido=0 AND IDLocal IN (2,3) AND FechaEscaneo<@inicio GROUP BY LTRIM(RTRIM(CODIGO)), IDLocal),
                 b AS (SELECT d.CODIGO, d.destino, d.desp, rp.recep, DATEDIFF(MINUTE, d.desp, GETDATE()) AS minutos
                       FROM d INNER JOIN rp ON rp.CODIGO=d.CODIGO AND rp.IDLocal=d.IDLocalDestino),
                 {ContenidoCte}
            SELECT CV.FPTOVEN AS FPTOVEN, CV.FNUMCOMP AS FNUMCOMP, b.minutos AS Minutos, b.destino AS Destino, b.recep AS RecepFecha,
                   ct.n_arts AS NArts, ct.un_artcod AS UnArtcod, RTRIM(ART.ARTDES) AS UnArtdes
            FROM b
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV ON CV.CODIGO=b.CODIGO
            LEFT JOIN contenido ct ON ct.CODIGO=b.CODIGO
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD)=ct.un_artcod AND ct.n_arts=1
            ORDER BY b.minutos DESC;
            """;
        var rows = (await cn.QueryAsync<PendRowRaw>(new CommandDefinition(sql, new { inicio, fin }, commandTimeout: 60, cancellationToken: ct))).ToList();
        return new PendListaDto
        {
            Total = rows.Count,
            Items = rows.Select(r => new PendItemDto
            {
                NroRemito = Nro(r.FPTOVEN, r.FNUMCOMP), Minutos = r.Minutos,
                Contenido = Contenido(r.NArts, r.UnArtcod, r.UnArtdes), Local = r.Destino,
                Traza = $"{r.Destino} ya lo recibió {(r.RecepFecha is { } rf ? rf.ToString("dd/MM HH:mm") : "—")}"
            }).ToList()
        };
    }

    private async Task<PendBloqueDto> SinEscanearAsync(Microsoft.Data.SqlClient.SqlConnection cn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        var sql = $"""
            WITH base AS (
                SELECT c.RemitoCODIGO, c.FNUMCOMP, c.FPTOVEN, UPPER(RTRIM(c.LocalDestino)) AS destino,
                       DATEDIFF(MINUTE, c.FechaEmision, GETDATE()) AS minutos,
                       CASE WHEN rd.CODIGO IS NOT NULL THEN 1 ELSE 0 END AS despachado,
                       ROW_NUMBER() OVER (PARTITION BY UPPER(RTRIM(c.LocalDestino)) ORDER BY c.FechaEmision ASC) AS rn
                FROM dbo.ImpresorRemito_Cola c
                LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV ON CV.CODIGO=c.RemitoCODIGO
                LEFT JOIN (SELECT DISTINCT CODIGO FROM dbo.RemitosDespachados WHERE Eliminado=0) rd ON rd.CODIGO=c.RemitoCODIGO
                LEFT JOIN (SELECT DISTINCT IDLocal, LTRIM(RTRIM(CODIGO)) AS CODIGO FROM dbo.RemitosEscaneados WHERE EsDesconocido=0) re
                    ON re.CODIGO=LTRIM(RTRIM(c.RemitoCODIGO)) AND re.IDLocal=CASE UPPER(RTRIM(c.LocalDestino)) WHEN 'LURO' THEN 2 WHEN 'PERALTA' THEN 3 END
                WHERE c.FechaEmision>=@inicio AND c.FechaEmision<@fin AND (CV.CODIGO IS NULL OR CV.ANULADO=0)
                  AND UPPER(RTRIM(c.LocalOrigen))='CENTRAL' AND UPPER(RTRIM(c.LocalDestino)) IN ('LURO','PERALTA') AND re.CODIGO IS NULL),
                 {ContenidoCte}
            SELECT b.FPTOVEN AS FPTOVEN, b.FNUMCOMP AS FNUMCOMP, b.destino AS Destino, b.minutos AS Minutos,
                   CAST(b.despachado AS BIT) AS Despachado, ct.n_arts AS NArts, ct.un_artcod AS UnArtcod, RTRIM(ART.ARTDES) AS UnArtdes
            FROM base b
            LEFT JOIN contenido ct ON ct.CODIGO=b.RemitoCODIGO
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD)=ct.un_artcod AND ct.n_arts=1
            WHERE b.rn<=@top ORDER BY b.destino, b.minutos DESC;
            """;
        return MapBloque((await cn.QueryAsync<PendRowRaw>(new CommandDefinition(sql, new { inicio, fin, top = TopLocal }, commandTimeout: 60, cancellationToken: ct))).ToList(), true);
    }

    private async Task<PendBloqueDto> SinSalidaAsync(Microsoft.Data.SqlClient.SqlConnection cn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        var sql = $"""
            WITH base AS (
                SELECT LTRIM(RTRIM(E.CODIGO)) AS CODIGO, CASE E.IDLocal WHEN 2 THEN 'LURO' WHEN 3 THEN 'PERALTA' END AS destino,
                       MIN(E.FechaEscaneo) AS recep, DATEDIFF(MINUTE, MIN(E.FechaEscaneo), GETDATE()) AS minutos
                FROM dbo.RemitosEscaneados E
                LEFT JOIN (SELECT DISTINCT LTRIM(RTRIM(CODIGO)) AS CODIGO FROM dbo.RemitosDespachados WHERE ISNULL(Eliminado,0)=0) D ON D.CODIGO=LTRIM(RTRIM(E.CODIGO))
                WHERE E.EsDesconocido=0 AND E.FechaEscaneo>=@inicio AND E.FechaEscaneo<@fin AND E.IDLocal IN (2,3) AND D.CODIGO IS NULL
                GROUP BY LTRIM(RTRIM(E.CODIGO)), E.IDLocal),
                 ci AS (SELECT b.*, CV.FNUMCOMP, CV.FPTOVEN, ROW_NUMBER() OVER (PARTITION BY b.destino ORDER BY b.recep ASC) AS rn
                        FROM base b LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV ON CV.CODIGO=b.CODIGO),
                 {ContenidoCte}
            SELECT ci.FPTOVEN AS FPTOVEN, ci.FNUMCOMP AS FNUMCOMP, ci.destino AS Destino, ci.minutos AS Minutos,
                   ct.n_arts AS NArts, ct.un_artcod AS UnArtcod, RTRIM(ART.ARTDES) AS UnArtdes
            FROM ci
            LEFT JOIN contenido ct ON ct.CODIGO=ci.CODIGO
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD)=ct.un_artcod AND ct.n_arts=1
            WHERE ci.rn<=@top ORDER BY ci.destino, ci.minutos DESC;
            """;
        return MapBloque((await cn.QueryAsync<PendRowRaw>(new CommandDefinition(sql, new { inicio, fin, top = TopLocal }, commandTimeout: 60, cancellationToken: ct))).ToList(), false);
    }

    private static PendBloqueDto MapBloque(List<PendRowRaw> rows, bool conDespacho)
    {
        var items = rows.Select(r => new PendItemDto
        {
            NroRemito = Nro(r.FPTOVEN, r.FNUMCOMP), Minutos = r.Minutos,
            Contenido = Contenido(r.NArts, r.UnArtcod, r.UnArtdes),
            Local = (r.Destino ?? "").Trim().ToUpperInvariant(),
            Despachado = conDespacho && r.Despachado
        }).ToList();
        return new PendBloqueDto
        {
            Items = items, Total = items.Count,
            Luro = items.Count(x => x.Local == "LURO"), Peralta = items.Count(x => x.Local == "PERALTA")
        };
    }

    private async Task<PendBloqueDto> RecepcionPendAsync(Microsoft.Data.SqlClient.SqlConnection cn, DateTime inicio, DateTime fin, CancellationToken ct)
    {
        var desde = inicio.ToString("yyyy-MM-dd HH:mm:ss");
        var hasta = fin.ToString("yyyy-MM-dd HH:mm:ss");
        var items = new List<PendItemDto>();
        foreach (var (loc, host, db) in new[] { ("LURO", "marketluro.ddns.net", "DRAGONFISH_LURO"), ("PERALTA", "marketperalta.ddns.net", "DRAGONFISH_PERALTA") })
        {
            var inner = $"SELECT FNUMCOMP, FPTOVEN, FFCH, FCLIENTE FROM {db}.ZooLogic.COMPROBANTEV "
                      + "WHERE FLETRA=''R'' AND ANULADO=0 AND UPPER(RTRIM(FCLIENTE)) IN (''CENTRAL'',''CCENTRAL'') "
                      + $"AND FFCH >= ''{desde}'' AND FFCH < ''{hasta}''";
            var sql = $@"
                WITH rl AS (SELECT '{loc}' AS origen, FNUMCOMP, FPTOVEN, FFCH FROM OPENQUERY([{host}], '{inner}'))
                SELECT rl.FPTOVEN AS FPTOVEN, rl.FNUMCOMP AS FNUMCOMP, '{loc}' AS Origen,
                       DATEDIFF(MINUTE, rl.FFCH, GETDATE()) AS Minutos
                FROM rl
                LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.MTRANS mt ON mt.ORIGNRO=rl.FNUMCOMP
                    AND UPPER(RTRIM(LTRIM(mt.ORIGDEST)))='{loc}' AND mt.ORIGLETRA='R' AND mt.ANULADO=0
                WHERE mt.CODIGO IS NULL
                ORDER BY rl.FFCH ASC;";
            try
            {
                var rows = await cn.QueryAsync<PendRowRaw>(new CommandDefinition(sql, commandTimeout: 30, cancellationToken: ct));
                foreach (var r in rows)
                    items.Add(new PendItemDto { NroRemito = Nro(r.FPTOVEN, r.FNUMCOMP), Minutos = r.Minutos, Local = loc, Traza = $"{loc} → CENTRAL" });
            }
            catch { /* local caído */ }
        }
        return new PendBloqueDto
        {
            Items = items, Total = items.Count,
            Luro = items.Count(x => x.Local == "LURO"), Peralta = items.Count(x => x.Local == "PERALTA")
        };
    }

    // ---- PANEL 3: últimos mapeos en logística (CENTRAL) ----
    private const int MapeosTop = 16;
    private sealed record MapeoRow(string? ArtCod, string? ArtDes, int? IdPalet, int? NroPalet, string? Modulo, int Minutos);

    public async Task<PanelMapeosDto> GetPanelMapeosAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@top)
                RTRIM(R.ARTCOD)  AS ArtCod,
                RTRIM(ART.ARTDES) AS ArtDes,
                R.IDPalet         AS IdPalet,
                P.NroPalet        AS NroPalet,
                RTRIM(M.Modulo)   AS Modulo,
                DATEDIFF(MINUTE, R.FechaHora, GETDATE()) AS Minutos
            FROM dbo.MapeoRegistro R
            INNER JOIN dbo.Mapeo M ON R.IDMapeo = M.ID
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(R.ARTCOD)
            LEFT JOIN dbo.Palets P ON P.ID = R.IDPalet
            WHERE M.IDUbicacion = 1 AND R.Eliminado = 0 AND M.Eliminado = 0
            ORDER BY R.ID DESC;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<MapeoRow>(new CommandDefinition(sql, new { top = MapeosTop }, commandTimeout: 60, cancellationToken: ct));

        var items = rows.Select(r =>
        {
            var esPalet = r.IdPalet is > 0;
            return new MapeoRecienteDto
            {
                EsPalet = esPalet,
                Codigo = esPalet ? "PALET" : (string.IsNullOrWhiteSpace(r.ArtCod) ? "—" : r.ArtCod!),
                Descripcion = esPalet
                    ? (r.NroPalet is not null ? $"N° {r.NroPalet}" : "(palet sin número)")
                    : (string.IsNullOrWhiteSpace(r.ArtDes) ? "(sin descripción)" : r.ArtDes!),
                Modulo = string.IsNullOrWhiteSpace(r.Modulo) ? "—" : r.Modulo!,
                Minutos = r.Minutos
            };
        }).ToList();

        var now = DateTime.Now;
        return new PanelMapeosDto { Items = items, Total = items.Count, Ventana = "", Actualizado = now.ToString("HH:mm:ss") };
    }

    // ---- PANEL 4: ubicaciones libres en logística (CENTRAL sin MapeoRegistro vigente) ----
    private const int VaciasTop = 150;

    // Orden de pasillos: primero racks (letras), luego numéricos por valor.
    private const string PasilloOrder = """
        CASE WHEN TRY_CAST(RTRIM(M.Pasillo) AS INT) IS NULL THEN 0 ELSE 1 END,
        CASE WHEN TRY_CAST(RTRIM(M.Pasillo) AS INT) IS NULL THEN RTRIM(M.Pasillo) END,
        TRY_CAST(RTRIM(M.Pasillo) AS INT)
        """;

    public async Task<PanelVaciasDto> GetPanelVaciasAsync(CancellationToken ct = default)
    {
        var itemsSql = $"""
            SELECT TOP (@top)
                RTRIM(M.Pasillo) AS Pasillo,
                RTRIM(M.Modulo)  AS Modulo,
                M.Posicion       AS Posicion
            FROM dbo.Mapeo M
            LEFT JOIN dbo.MapeoRegistro R ON R.IDMapeo = M.ID AND R.Eliminado = 0
            WHERE M.IDUbicacion = 1 AND M.Eliminado = 0 AND R.ID IS NULL
            ORDER BY {PasilloOrder}, M.Modulo, M.Posicion;
            """;

        var aggSql = $"""
            SELECT RTRIM(M.Pasillo) AS Pasillo, COUNT(*) AS Cantidad
            FROM dbo.Mapeo M
            LEFT JOIN dbo.MapeoRegistro R ON R.IDMapeo = M.ID AND R.Eliminado = 0
            WHERE M.IDUbicacion = 1 AND M.Eliminado = 0 AND R.ID IS NULL
            GROUP BY RTRIM(M.Pasillo)
            ORDER BY {PasilloOrder};
            """;

        using var cn = _db.Create();
        var items = (await cn.QueryAsync<UbicacionLibreDto>(new CommandDefinition(itemsSql, new { top = VaciasTop }, commandTimeout: 60, cancellationToken: ct))).ToList();
        var porPasillo = (await cn.QueryAsync<PasilloLibreDto>(new CommandDefinition(aggSql, commandTimeout: 60, cancellationToken: ct))).ToList();

        return new PanelVaciasDto
        {
            Items = items,
            PorPasillo = porPasillo,
            Total = porPasillo.Sum(p => p.Cantidad),
            Actualizado = DateTime.Now.ToString("HH:mm:ss")
        };
    }

    // ---- PANEL 5: artículos estancados en logística (cache 5 min + refresh en background) ----
    private const int EstancadosTop = 16;
    private const int EstancadoDiasEnTemp = 30;
    private const int EstancadoDiasFueraTemp = 365;
    private static readonly (string Loc, string Host)[] LocalesHosts =
        { ("LURO", "marketluro.ddns.net"), ("PERALTA", "marketperalta.ddns.net") };

    // Argentina retail: marzo-agosto → INV (Oto-Inv), septiembre-febrero → VER (Prim-Ver).
    private static string TemporadaActual() => DateTime.Now.Month is >= 3 and <= 8 ? "INV" : "VER";

    private static bool HostVivo(string host)
    {
        try
        {
            using var c = new System.Net.Sockets.TcpClient();
            return c.ConnectAsync(host, 1433).Wait(TimeSpan.FromMilliseconds(600)) && c.Connected;
        }
        catch { return false; }
    }

    // Bloque SQL que trae los remitos del local por OPENQUERY y expande #remitos_palets → #arts_palet.
    // Requiere que #remitos_palets y #arts_palet ya existan. Sólo se incluye si el host está vivo.
    private static string PaletLocalInsert(string loc, string fechaCorte)
    {
        var db = "DRAGONFISH_" + loc;
        var host = LocalesHosts.First(x => x.Loc == loc).Host;
        // OJO: dentro del string de OPENQUERY las comillas simples van duplicadas.
        var inner = $"SELECT CV.FLETRA, CV.FPTOVEN, CV.FNUMCOMP, RTRIM(CVD.FART) AS FART " +
                    $"FROM {db}.ZooLogic.COMPROBANTEV CV " +
                    $"JOIN {db}.ZooLogic.COMPROBANTEVDET CVD ON CVD.CODIGO=CV.CODIGO " +
                    $"WHERE CV.FLETRA=''R'' AND CV.ANULADO=0 AND CV.FFCH >= ''{fechaCorte}''";
        return $"""
            SELECT FLETRA, FPTOVEN, FNUMCOMP, FART, '{loc}' AS Origen
            INTO #cv_{loc.ToLowerInvariant()}
            FROM OPENQUERY([{host}], '{inner}');
            INSERT INTO #arts_palet
            SELECT DISTINCT FART, rp.IDPalet
            FROM #remitos_palets rp
            INNER JOIN #cv_{loc.ToLowerInvariant()} cv
                ON cv.FLETRA = rp.FLETRA AND cv.FPTOVEN = rp.FPTOVEN AND cv.FNUMCOMP = rp.FNUMCOMP AND cv.Origen = rp.Origen
            WHERE rp.Origen = '{loc}'
              AND NOT EXISTS (SELECT 1 FROM #arts_palet a WHERE a.ARTCOD = cv.FART AND a.IDPalet = rp.IDPalet);
            """;
    }

    public Task<PanelEstancadosDto> GetPanelEstancadosAsync(CancellationToken ct = default)
    {
        // Si el cache está vencido y nadie lo está refrescando, disparamos el refresh
        // en background y devolvemos el último valor conocido (el HTTP nunca se cuelga).
        if (_estancados.Stale() && _estancados.TryBeginRefresh())
        {
            _ = Task.Run(async () =>
            {
                try { _estancados.Complete(await EstancadosQueryAsync(CancellationToken.None)); }
                catch { _estancados.Fail(); }
            });
        }

        var (rows, ts, refreshing) = _estancados.Snapshot();
        return Task.FromResult(new PanelEstancadosDto
        {
            Items = rows,
            Total = rows.Count,
            Loading = refreshing && rows.Count == 0,
            Temporada = TemporadaActual(),
            Actualizado = ts == DateTime.MinValue ? "" : ts.ToString("HH:mm:ss")
        });
    }

    private async Task<List<ArticuloEstancadoDto>> EstancadosQueryAsync(CancellationToken ct)
    {
        var temp = TemporadaActual();
        var fechaCorte = DateTime.Now.AddDays(-365).ToString("yyyy-MM-dd HH:mm:ss");

        // Bloque OPENQUERY por local (sólo si el host responde, igual que el ping-check del Dash).
        var insertLuro = HostVivo(LocalesHosts[0].Host) ? PaletLocalInsert("LURO", fechaCorte) : "";
        var insertPeralta = HostVivo(LocalesHosts[1].Host) ? PaletLocalInsert("PERALTA", fechaCorte) : "";

        var sql = $"""
            SET NOCOUNT ON;

            SELECT DISTINCT RTRIM(R.ARTCOD) AS ARTCOD
            INTO #directos
            FROM MARKET.dbo.MapeoRegistro R
            INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo = M.ID
            WHERE M.IDUbicacion = 1 AND M.Eliminado = 0 AND R.Eliminado = 0
              AND (R.IDPalet IS NULL OR R.IDPalet = 0);

            SELECT DISTINCT R.IDPalet
            INTO #palets_no_ab
            FROM MARKET.dbo.MapeoRegistro R
            INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo = M.ID
            WHERE M.IDUbicacion = 1 AND M.Eliminado = 0 AND R.Eliminado = 0
              AND R.IDPalet > 0 AND RTRIM(M.Pasillo) NOT IN ('A','B');

            SELECT
                pna.IDPalet,
                UPPER(RTRIM(PD.Origen)) AS Origen,
                LEFT(RTRIM(PD.NroRemito), 1) AS FLETRA,
                TRY_CAST(SUBSTRING(RTRIM(PD.NroRemito), 3, 4) AS int) AS FPTOVEN,
                TRY_CAST(SUBSTRING(RTRIM(PD.NroRemito), 8, 8) AS int) AS FNUMCOMP
            INTO #remitos_palets
            FROM #palets_no_ab pna
            INNER JOIN MARKET.dbo.PaletsDetalle PD ON PD.IDPalet = pna.IDPalet AND PD.Eliminado = 0
            WHERE RTRIM(PD.NroRemito) LIKE 'R [0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]';

            SELECT DISTINCT RTRIM(CVD.FART) AS ARTCOD, rp.IDPalet
            INTO #arts_palet
            FROM #remitos_palets rp
            INNER JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV
                ON CV.FLETRA = rp.FLETRA AND CV.FPTOVEN = rp.FPTOVEN AND CV.FNUMCOMP = rp.FNUMCOMP
            INNER JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET CVD ON CVD.CODIGO = CV.CODIGO
            WHERE rp.Origen = 'CENTRAL';

            {insertLuro}
            {insertPeralta}

            CREATE INDEX ix_arts_palet_artcod ON #arts_palet(ARTCOD);

            WITH articulos_no_pallet AS (
                SELECT ARTCOD FROM #directos
                UNION
                SELECT DISTINCT ARTCOD FROM #arts_palet
            ),
            palet_por_articulo AS (
                SELECT ap.ARTCOD, P.NroPalet,
                       RTRIM(M.Pasillo) AS PaletPasillo, RTRIM(M.Modulo) AS PaletModulo, M.Posicion AS PaletPosicion,
                       ROW_NUMBER() OVER (PARTITION BY ap.ARTCOD ORDER BY M.Pasillo, M.Modulo, M.Posicion) AS rn
                FROM #arts_palet ap
                INNER JOIN MARKET.dbo.Palets P ON P.ID = ap.IDPalet
                INNER JOIN MARKET.dbo.MapeoRegistro R ON R.IDPalet = ap.IDPalet AND R.Eliminado = 0
                INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo = M.ID AND M.Eliminado = 0 AND M.IDUbicacion = 1
            ),
            ultimo_envio AS (
                SELECT COALESCE(RTRIM(E.CARTICUL), RTRIM(CVD.FART)) AS ARTCOD, MAX(CV.FFCH) AS ultima_fecha
                FROM DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV
                INNER JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET CVD ON CV.CODIGO = CVD.CODIGO
                LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.EQUI E ON RTRIM(E.CCODIGO) = RTRIM(CVD.FART)
                WHERE CV.FLETRA = 'R' AND CV.ANULADO = 0 AND UPPER(RTRIM(CV.FCLIENTE)) IN ('LURO','PERALTA')
                GROUP BY COALESCE(RTRIM(E.CARTICUL), RTRIM(CVD.FART))
            )
            SELECT TOP (@top)
                A.ARTCOD AS ArtCod,
                RTRIM(ART.ARTDES) AS ArtDes,
                DATEDIFF(DAY, COALESCE(UE.ultima_fecha, ART.FALTAFW), GETDATE()) AS DiasEstancado,
                CASE WHEN UE.ultima_fecha IS NULL THEN 1 ELSE 0 END AS NuncaEnviado,
                COALESCE(UE.ultima_fecha, ART.FALTAFW) AS FechaRef,
                RTRIM(ART.ATEMPORADA) AS Atemporada,
                CASE WHEN RTRIM(ART.ATEMPORADA) = @temp OR RTRIM(ART.ATEMPORADA) IN ('TA','000')
                          OR ART.ATEMPORADA IS NULL OR LTRIM(RTRIM(ART.ATEMPORADA)) = ''
                     THEN 1 ELSE 0 END AS EnTemporada,
                ubic_first.Modulo,
                ubic_count.n_ubic AS NUbic,
                CASE WHEN palet_info.NroPalet IS NOT NULL THEN 1 ELSE 0 END AS ViaPalet,
                palet_info.NroPalet,
                palet_info.PaletModulo,
                ISNULL(stock_comb.stock_total, 0) AS StockTotal
            FROM articulos_no_pallet A
            LEFT JOIN ultimo_envio UE ON UE.ARTCOD = A.ARTCOD
            INNER JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = A.ARTCOD
            OUTER APPLY (
                SELECT TOP 1 RTRIM(M.Modulo) AS Modulo
                FROM MARKET.dbo.MapeoRegistro R
                INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo = M.ID
                WHERE M.IDUbicacion = 1 AND M.Eliminado = 0 AND R.Eliminado = 0
                  AND (R.IDPalet IS NULL OR R.IDPalet = 0) AND RTRIM(R.ARTCOD) = A.ARTCOD
                ORDER BY M.Pasillo, M.Modulo, M.Posicion
            ) ubic_first
            OUTER APPLY (
                SELECT COUNT(DISTINCT R.IDMapeo) AS n_ubic
                FROM MARKET.dbo.MapeoRegistro R
                INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo = M.ID
                WHERE M.IDUbicacion = 1 AND M.Eliminado = 0 AND R.Eliminado = 0
                  AND (R.IDPalet IS NULL OR R.IDPalet = 0) AND RTRIM(R.ARTCOD) = A.ARTCOD
            ) ubic_count
            LEFT JOIN (
                SELECT RTRIM(COART) AS ARTCOD, SUM(CORIG) AS stock_total
                FROM DRAGONFISH_CENTRAL.Zoologic.COMB GROUP BY RTRIM(COART)
            ) stock_comb ON stock_comb.ARTCOD = A.ARTCOD
            LEFT JOIN palet_por_articulo palet_info ON palet_info.ARTCOD = A.ARTCOD AND palet_info.rn = 1
            WHERE ART.FALTAFW IS NOT NULL
              AND DATEDIFF(DAY, COALESCE(UE.ultima_fecha, ART.FALTAFW), GETDATE()) >
                  CASE WHEN RTRIM(ART.ATEMPORADA) = @temp OR RTRIM(ART.ATEMPORADA) IN ('TA','000')
                            OR ART.ATEMPORADA IS NULL OR LTRIM(RTRIM(ART.ATEMPORADA)) = ''
                       THEN @diasEnTemp ELSE @diasFueraTemp END
            ORDER BY
                CASE WHEN RTRIM(ART.ATEMPORADA) = @temp THEN 1 WHEN RTRIM(ART.ATEMPORADA) = 'TA' THEN 2 ELSE 3 END ASC,
                COALESCE(UE.ultima_fecha, ART.FALTAFW) ASC;
            """;

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<ArticuloEstancadoDto>(new CommandDefinition(
            sql, new { top = EstancadosTop, temp, diasEnTemp = EstancadoDiasEnTemp, diasFueraTemp = EstancadoDiasFueraTemp },
            commandTimeout: 120, cancellationToken: ct));
        return rows.ToList();
    }

    // ---- PANEL 6: picking nocturno (corrida pedido vs armado) ----
    private sealed record CorridaRow(int Id, DateTime FechaHoraCorrida);
    private sealed record PedidoRow(string Local, string ArtCod, string? ArtDes, int PacksPedidos, int CantPack, string? Pasillo, string? Modulo);
    private sealed record ArmadoRow(string Local, string ArtCod, decimal Unidades);
    private sealed record PackRow(string ArtCod, int CantPack);

    private static bool EnModoReposicion() => DateTime.Now.Hour is >= 21 or < 5;

    public async Task<PanelPickingDto> GetPanelPickingAsync(CancellationToken ct = default)
    {
        var res = new PanelPickingDto { ModoRepo = EnModoReposicion(), Actualizado = DateTime.Now.ToString("HH:mm:ss") };
        using var cn = _db.Create();

        // 1) Última corrida productiva.
        var corrida = await cn.QuerySingleOrDefaultAsync<CorridaRow>(new CommandDefinition("""
            SELECT TOP 1 ID AS Id, FechaHoraCorrida
            FROM MARKET.dbo.Reposicion
            WHERE MachineName = 'DESKTOP-PGIO2QP' AND ISNULL(Eliminado, 0) = 0
            ORDER BY ID DESC;
            """, cancellationToken: ct));
        if (corrida is null) return res;
        res.Corrida = corrida.FechaHoraCorrida;

        // 2) Pedido por (Local, ARTCOD): packs + ubicación CENTRAL + descripción.
        const string sqlPedido = """
            WITH ubic_central AS (
                SELECT R.ARTCOD,
                       Pasillo = (SELECT TOP 1 RTRIM(M2.Pasillo) FROM MARKET.dbo.MapeoRegistro R2
                                  INNER JOIN MARKET.dbo.Mapeo M2 ON R2.IDMapeo=M2.ID
                                  WHERE M2.IDUbicacion=1 AND M2.Eliminado=0 AND R2.Eliminado=0 AND RTRIM(R2.ARTCOD)=RTRIM(R.ARTCOD)
                                  ORDER BY M2.Pasillo, M2.Modulo, M2.Posicion),
                       Modulo  = (SELECT TOP 1 RTRIM(M2.Modulo) FROM MARKET.dbo.MapeoRegistro R2
                                  INNER JOIN MARKET.dbo.Mapeo M2 ON R2.IDMapeo=M2.ID
                                  WHERE M2.IDUbicacion=1 AND M2.Eliminado=0 AND R2.Eliminado=0 AND RTRIM(R2.ARTCOD)=RTRIM(R.ARTCOD)
                                  ORDER BY M2.Pasillo, M2.Modulo, M2.Posicion)
                FROM (SELECT DISTINCT ARTCOD FROM MARKET.dbo.MapeoRegistro WHERE Eliminado=0) R
            )
            SELECT
                UPPER(RTRIM(RD.LocalDestino)) AS Local,
                RTRIM(RD.ARTCOD) AS ArtCod,
                RTRIM(ART.ARTDES) AS ArtDes,
                RD.PacksAReponer AS PacksPedidos,
                ISNULL((SELECT TOP 1 ADX.CantPack FROM MARKET.dbo.ArticulosDatosAdiciones ADX
                        WHERE RTRIM(ADX.ARTCOD)=RTRIM(RD.ARTCOD) AND ADX.Eliminado=0 ORDER BY ADX.ID DESC), 1) AS CantPack,
                uc.Pasillo, uc.Modulo
            FROM MARKET.dbo.ReposicionDetalle RD
            LEFT JOIN ubic_central uc ON uc.ARTCOD = RTRIM(RD.ARTCOD)
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD)=RTRIM(RD.ARTCOD)
            WHERE RD.IDReposicion = @idCorrida AND ISNULL(RD.Eliminado, 0) = 0 AND RD.PacksAReponer > 0;
            """;
        var pedidos = (await cn.QueryAsync<PedidoRow>(new CommandDefinition(sqlPedido, new { idCorrida = corrida.Id }, commandTimeout: 60, cancellationToken: ct))).ToList();

        // 3) Armado: ImpresorRemito_Cola post-corrida ⨯ COMPROBANTEVDET, unidades por ARTCOD.
        const string sqlArmado = """
            SELECT
                UPPER(RTRIM(ir.LocalDestino)) AS Local,
                COALESCE(RTRIM(E.CARTICUL), RTRIM(D.FART)) AS ArtCod,
                SUM(D.FCANT) AS Unidades
            FROM dbo.ImpresorRemito_Cola ir
            INNER JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV C ON C.CODIGO = ir.RemitoCODIGO
            INNER JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET D ON D.CODIGO = C.CODIGO
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.EQUI E ON RTRIM(E.CCODIGO) = RTRIM(D.FART)
            WHERE ir.FechaEmision > @corridaTs
              AND UPPER(RTRIM(ir.LocalOrigen)) = 'CENTRAL'
              AND UPPER(RTRIM(ir.LocalDestino)) IN ('LURO','PERALTA')
              AND C.ANULADO = 0 AND LEFT(RTRIM(D.FART),1) <> 'Z'
            GROUP BY UPPER(RTRIM(ir.LocalDestino)), COALESCE(RTRIM(E.CARTICUL), RTRIM(D.FART));
            """;
        var armados = (await cn.QueryAsync<ArmadoRow>(new CommandDefinition(sqlArmado, new { corridaTs = corrida.FechaHoraCorrida }, commandTimeout: 60, cancellationToken: ct))).ToList();
        var armadoIdx = armados.ToDictionary(a => (a.Local, a.ArtCod), a => a.Unidades);

        // 4) Cruce — dos métricas honestas (packs cubiertos con cap + arts con envío). Reponibles = con ubicación.
        var locales = new[] { "LURO", "PERALTA" };
        var pedidosPorLocal = new Dictionary<string, HashSet<string>> { ["LURO"] = new(), ["PERALTA"] = new() };
        foreach (var loc in locales)
        {
            var dl = loc == "LURO" ? res.Luro : res.Peralta;
            decimal totalPedido = 0, totalCubierto = 0;
            foreach (var p in pedidos.Where(x => x.Local == loc))
            {
                var pack = p.CantPack <= 0 ? 1m : p.CantPack;
                decimal packsPed = p.PacksPedidos;
                var unidadesArm = armadoIdx.TryGetValue((loc, p.ArtCod), out var u) ? u : 0m;
                var packsArm = unidadesArm / pack;
                var cubierto = Math.Min(packsArm, packsPed);
                var tieneUbic = !string.IsNullOrWhiteSpace(p.Modulo);
                if (tieneUbic)
                {
                    totalPedido += packsPed;
                    totalCubierto += cubierto;
                    dl.ArtsPedidos++;
                    if (unidadesArm > 0) dl.ArtsArmados++;
                }
                pedidosPorLocal[loc].Add(p.ArtCod);
                var falta = packsPed - packsArm;
                if (falta > 0.5m)
                    dl.Items.Add(new PickingItemDto
                    {
                        ArtCod = p.ArtCod,
                        ArtDes = (p.ArtDes ?? "").Trim(),
                        Pasillo = (p.Pasillo ?? "").Trim(),
                        Modulo = (p.Modulo ?? "").Trim(),
                        Pedidos = (int)packsPed,
                        Armados = Math.Round(packsArm, 1),
                        Falta = Math.Max(1, (int)Math.Round(falta, MidpointRounding.AwayFromZero))
                    });
            }
            dl.TotalPedido = (int)Math.Round(totalPedido, MidpointRounding.AwayFromZero);
            dl.TotalArmado = Math.Round(totalCubierto, 1);
            // Orden por ubicación; sin ubicación al final.
            dl.Items = dl.Items
                .OrderBy(x => x.Pasillo.Length == 0)
                .ThenBy(x => x.Pasillo).ThenBy(x => x.Modulo)
                .ToList();
        }

        // 5) Refuerzos puros: ARTCODs enviados que NO estaban en la corrida.
        var huerfanos = armadoIdx.Keys.Where(k => !pedidosPorLocal[k.Item1].Contains(k.Item2))
            .Select(k => k.Item2).Distinct().ToList();
        var packHuerfanos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (huerfanos.Count > 0)
        {
            const string sqlPack = """
                SELECT RTRIM(ARTCOD) AS ArtCod, CantPack FROM (
                    SELECT ARTCOD, CantPack, ROW_NUMBER() OVER (PARTITION BY RTRIM(ARTCOD) ORDER BY ID DESC) AS rn
                    FROM MARKET.dbo.ArticulosDatosAdiciones WHERE Eliminado = 0 AND RTRIM(ARTCOD) IN @arts
                ) z WHERE rn = 1;
                """;
            foreach (var r in await cn.QueryAsync<PackRow>(new CommandDefinition(sqlPack, new { arts = huerfanos }, commandTimeout: 60, cancellationToken: ct)))
                packHuerfanos[r.ArtCod] = r.CantPack <= 0 ? 1 : r.CantPack;
        }
        foreach (var ((loc, artcod), unidades) in armadoIdx)
        {
            if (loc != "LURO" && loc != "PERALTA") continue;
            if (pedidosPorLocal[loc].Contains(artcod)) continue;
            var dl = loc == "LURO" ? res.Luro : res.Peralta;
            var pack = packHuerfanos.TryGetValue(artcod, out var cp) ? cp : 1;
            dl.ExtrasArts++;
            dl.ExtrasPacks += unidades / pack;
        }
        res.Luro.ExtrasPacks = Math.Round(res.Luro.ExtrasPacks, 1);
        res.Peralta.ExtrasPacks = Math.Round(res.Peralta.ExtrasPacks, 1);

        return res;
    }

    // ---- PANEL 7: artículos con más ubicaciones en CENTRAL ----
    private const int MasUbicTop = 16;
    private const int MasUbicMin = 2;
    private sealed record UbicRow(string ArtCod, string? ArtDes, int NUbic, string? Pasillo, string? Modulo, bool EsPalet, string? PaletTag);

    public Task<PanelMasUbicDto> GetPanelMasUbicAsync(CancellationToken ct = default)
    {
        // Query pesada (expande palets cruzando con Dragon CENTRAL + OPENQUERY locales) → cache + refresh background.
        if (_masUbic.Stale() && _masUbic.TryBeginRefresh())
        {
            _ = Task.Run(async () =>
            {
                try { _masUbic.Complete(await MasUbicQueryAsync(CancellationToken.None)); }
                catch { _masUbic.Fail(); }
            });
        }

        var (val, ts, refreshing) = _masUbic.Snapshot();
        var items = val ?? new List<ArticuloUbicacionesDto>();
        return Task.FromResult(new PanelMasUbicDto
        {
            Items = items,
            Total = items.Count,
            Loading = refreshing && items.Count == 0,
            Actualizado = ts == DateTime.MinValue ? "" : ts.ToString("HH:mm:ss")
        });
    }

    private async Task<List<ArticuloUbicacionesDto>> MasUbicQueryAsync(CancellationToken ct)
    {
        var fechaCorte = DateTime.Now.AddDays(-365).ToString("yyyy-MM-dd HH:mm:ss");
        var insertLuro = HostVivo(LocalesHosts[0].Host) ? PaletLocalInsert("LURO", fechaCorte) : "";
        var insertPeralta = HostVivo(LocalesHosts[1].Host) ? PaletLocalInsert("PERALTA", fechaCorte) : "";

        // Una ubicación de un artículo = cada módulo donde está suelto + cada palet (con su ubicación)
        // que lo contiene. El contenido del palet sale de sus remitos de Dragon.
        var sql = $"""
            SET NOCOUNT ON;

            -- Palets mapeados en CENTRAL con su ubicación (puede haber >1 ubicación por palet).
            SELECT R.IDPalet, P.NroPalet, RTRIM(M.Pasillo) AS Pasillo, RTRIM(M.Modulo) AS Modulo
            INTO #palet_ubic
            FROM MARKET.dbo.MapeoRegistro R
            INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo = M.ID
            INNER JOIN MARKET.dbo.Palets P ON P.ID = R.IDPalet
            WHERE M.IDUbicacion = 1 AND R.Eliminado = 0 AND M.Eliminado = 0 AND ISNULL(R.IDPalet, 0) > 0;

            -- Remitos que componen esos palets (parse del NroRemito por origen).
            SELECT DISTINCT
                pu.IDPalet,
                UPPER(RTRIM(PD.Origen)) AS Origen,
                LEFT(RTRIM(PD.NroRemito), 1) AS FLETRA,
                TRY_CAST(SUBSTRING(RTRIM(PD.NroRemito), 3, 4) AS int) AS FPTOVEN,
                TRY_CAST(SUBSTRING(RTRIM(PD.NroRemito), 8, 8) AS int) AS FNUMCOMP
            INTO #remitos_palets
            FROM (SELECT DISTINCT IDPalet FROM #palet_ubic) pu
            INNER JOIN MARKET.dbo.PaletsDetalle PD ON PD.IDPalet = pu.IDPalet AND PD.Eliminado = 0
            WHERE RTRIM(PD.NroRemito) LIKE 'R [0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]';

            -- Expansión palet → artículos (CENTRAL; locales por OPENQUERY si están vivos).
            SELECT DISTINCT RTRIM(CVD.FART) AS ARTCOD, rp.IDPalet
            INTO #arts_palet
            FROM #remitos_palets rp
            INNER JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV CV
                ON CV.FLETRA = rp.FLETRA AND CV.FPTOVEN = rp.FPTOVEN AND CV.FNUMCOMP = rp.FNUMCOMP
            INNER JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET CVD ON CVD.CODIGO = CV.CODIGO
            WHERE rp.Origen = 'CENTRAL';

            {insertLuro}
            {insertPeralta}

            ;WITH ubic_base AS (
                -- Artículos sueltos (sin palet)
                SELECT RTRIM(R.ARTCOD) AS ARTCOD, RTRIM(M.Pasillo) AS Pasillo, RTRIM(M.Modulo) AS Modulo,
                       CAST(0 AS bit) AS EsPalet, CAST(NULL AS varchar(30)) AS PaletTag
                FROM MARKET.dbo.MapeoRegistro R
                INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo = M.ID
                WHERE M.IDUbicacion = 1 AND R.Eliminado = 0 AND M.Eliminado = 0
                  AND ISNULL(R.IDPalet, 0) = 0 AND R.ARTCOD IS NOT NULL AND RTRIM(R.ARTCOD) <> ''
                UNION ALL
                -- Artículos dentro de palets: heredan la ubicación del palet
                SELECT ap.ARTCOD, pu.Pasillo, pu.Modulo,
                       CAST(1 AS bit), 'PALET ' + RTRIM(CAST(pu.NroPalet AS varchar(20)))
                FROM #arts_palet ap
                INNER JOIN #palet_ubic pu ON pu.IDPalet = ap.IDPalet
            ),
            conteos AS (
                SELECT ARTCOD, COUNT(*) AS n_ubic FROM ubic_base GROUP BY ARTCOD HAVING COUNT(*) >= @min
            ),
            top_art AS (
                SELECT TOP (@top) ARTCOD, n_ubic FROM conteos ORDER BY n_ubic DESC, ARTCOD ASC
            )
            SELECT t.ARTCOD AS ArtCod, RTRIM(ART.ARTDES) AS ArtDes, t.n_ubic AS NUbic,
                   u.Pasillo, u.Modulo, u.EsPalet, u.PaletTag
            FROM top_art t
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = t.ARTCOD
            INNER JOIN ubic_base u ON u.ARTCOD = t.ARTCOD
            ORDER BY t.n_ubic DESC, t.ARTCOD ASC, u.EsPalet ASC, u.Pasillo, u.Modulo;

            DROP TABLE #palet_ubic; DROP TABLE #remitos_palets; DROP TABLE #arts_palet;
            """;
        using var cn = _db.Create();
        var raw = (await cn.QueryAsync<UbicRow>(new CommandDefinition(sql, new { min = MasUbicMin, top = MasUbicTop }, commandTimeout: 120, cancellationToken: ct))).ToList();

        // Agrupar por artículo (preservando el orden de n_ubic DESC del SQL).
        // OJO: las filas de palet pueden traer ARTCOD NULL → las agrupamos bajo "" (como el None de Python).
        var orden = new List<string>();
        var byArt = new Dictionary<string, ArticuloUbicacionesDto>();
        foreach (var r in raw)
        {
            var key = (r.ArtCod ?? "").Trim();
            if (!byArt.TryGetValue(key, out var art))
            {
                art = new ArticuloUbicacionesDto { ArtCod = key, ArtDes = (r.ArtDes ?? "").Trim(), NUbic = r.NUbic };
                byArt[key] = art;
                orden.Add(key);
            }
            if (r.EsPalet)
            {
                var tag = (r.PaletTag ?? "").Trim();
                if (tag.Length > 0) art.Detalle.Add(new UbicChipDto { Tag = tag, EsPalet = true });
            }
            else
            {
                var mod = (r.Modulo ?? "").Trim();
                if (mod.Length > 0) art.Detalle.Add(new UbicChipDto { Tag = mod, EsPalet = false, Cantidad = -1 });  // Cantidad=-1 marca pasillo en buffer
            }
        }

        // Resumen por pasillo (góndola, por cantidad DESC) + palets como chips individuales.
        foreach (var artcod in orden)
        {
            var art = byArt[artcod];
            var conteoPasillo = new Dictionary<string, int>();
            var palets = new List<UbicChipDto>();
            // Recalcular pasillo desde las filas raw del artículo (el módulo no alcanza para el pasillo).
            foreach (var r in raw.Where(x => (x.ArtCod ?? "").Trim() == artcod))
            {
                if (r.EsPalet)
                {
                    var tag = (r.PaletTag ?? "").Trim();
                    if (tag.Length > 0) palets.Add(new UbicChipDto { Tag = tag, Cantidad = 1, EsPalet = true });
                }
                else if (!string.IsNullOrWhiteSpace(r.Modulo))
                {
                    var pas = string.IsNullOrWhiteSpace(r.Pasillo) ? "—" : r.Pasillo!.Trim();
                    conteoPasillo[pas] = conteoPasillo.GetValueOrDefault(pas) + 1;
                }
            }
            art.PorPasillo = conteoPasillo
                .Select(kv => new UbicChipDto { Tag = kv.Key, Cantidad = kv.Value, EsPalet = false })
                .OrderByDescending(x => x.Cantidad).ThenBy(x => x.Tag)
                .Concat(palets)
                .ToList();
            // Limpiar el marcador interno Cantidad=-1 de los chips de detalle.
            foreach (var d in art.Detalle) if (d.Cantidad < 0) d.Cantidad = 0;
        }

        return orden.Select(a => byArt[a]).ToList();
    }

    // ==== PANEL 8: panel de reposición · inteligencia ====
    private static readonly string[] LocalesRepo = { "LURO", "PERALTA" };
    private static string OpenQ(string host, string inner) => $"SELECT * FROM OPENQUERY([{host}], '{inner.Replace("'", "''")}')";
    private static string HostDe(string loc) => LocalesHosts.First(x => x.Loc == loc).Host;

    // OJO: las columnas SUM(...) vuelven como decimal → los records DEBEN declararlas decimal
    // (Dapper hace matching posicional estricto del constructor; int vs decimal tira excepción).
    private sealed record DespDiaRow(string Local, decimal RepoHoy, decimal RefuerzoHoy);
    private sealed record EnvRow(string Local, decimal N);
    private sealed record DespRow(string ArtCod, decimal Desp);
    private sealed record DvRow(string ArtCod, decimal Dv);
    private sealed record UnivRow(string ArtCod, string? ArtDes, int CantPack, int Pendiente, string? Pasillo, string? Modulo);

    public Task<PanelReposicionDto> GetPanelReposicionAsync(CancellationToken ct = default)
    {
        if (_reposFast.Stale() && _reposFast.TryBeginRefresh())
            _ = Task.Run(async () => { try { _reposFast.Complete(await ReposFastQueryAsync(CancellationToken.None)); } catch { _reposFast.Fail(); } });
        if (_abast.Stale() && _abast.TryBeginRefresh())
            _ = Task.Run(async () => { try { _abast.Complete(await AbastQueryAsync(CancellationToken.None)); } catch { _abast.Fail(); } });

        var (fast, fastTs, fastRefreshing) = _reposFast.Snapshot();
        var (abast, _, _) = _abast.Snapshot();

        RepoLocalPanelDto Build(string loc) => new()
        {
            Local = loc,
            Abast = abast != null && abast.TryGetValue(loc, out var a) ? a : new RepoAbastDto(),
            DiaOp = fast != null && fast.DiaOp.TryGetValue(loc, out var d) ? d : new RepoDiaOpDto(),
            VentaHoy = fast != null && fast.VentaHoy.TryGetValue(loc, out var v) ? v : 0,
            Cobertura = fast != null && fast.Cobertura.TryGetValue(loc, out var c) ? c : new RepoCoberturaDto()
        };

        return Task.FromResult(new PanelReposicionDto
        {
            Luro = Build("LURO"),
            Peralta = Build("PERALTA"),
            Loading = fastRefreshing && fast == null,
            Actualizado = fastTs == DateTime.MinValue ? "" : fastTs.ToString("HH:mm:ss")
        });
    }

    public Task<PanelRojosDto> GetPanelRojosAsync(CancellationToken ct = default)
    {
        // Reusa el cache de cobertura del panel 8 (los rojos ya están calculados ahí).
        if (_reposFast.Stale() && _reposFast.TryBeginRefresh())
            _ = Task.Run(async () => { try { _reposFast.Complete(await ReposFastQueryAsync(CancellationToken.None)); } catch { _reposFast.Fail(); } });

        var (fast, ts, refreshing) = _reposFast.Snapshot();
        RepoCoberturaDto Cov(string loc) => fast != null && fast.Cobertura.TryGetValue(loc, out var c) ? c : new RepoCoberturaDto();
        var luro = Cov("LURO");
        var peralta = Cov("PERALTA");
        return Task.FromResult(new PanelRojosDto
        {
            Luro = luro.ItemsRojos,
            Peralta = peralta.ItemsRojos,
            RojoLuro = luro.Rojo,
            RojoPeralta = peralta.Rojo,
            Corrida = luro.Corrida ?? peralta.Corrida,
            Loading = refreshing && fast == null,
            Actualizado = ts == DateTime.MinValue ? "" : ts.ToString("HH:mm:ss")
        });
    }

    private async Task<ReposFast> ReposFastQueryAsync(CancellationToken ct)
    {
        var res = new ReposFast();
        foreach (var loc in LocalesRepo) { res.DiaOp[loc] = new RepoDiaOpDto(); res.VentaHoy[loc] = 0; }
        using var cn = _db.Create();

        var hoy0 = DateTime.Now.Date;
        var ayer0 = hoy0.AddDays(-1);
        var manana0 = hoy0.AddDays(1);
        string F(DateTime d) => d.ToString("yyyy-MM-dd HH:mm:ss");

        // --- Día operativo: despachos CENTRAL+CCENTRAL (repo/refuerzo de la jornada) ---
        const string sqlDesp = """
            SET NOCOUNT ON;
            DECLARE @hoy DATE = CAST(GETDATE() AS DATE), @ayer DATE = DATEADD(DAY,-1,CAST(GETDATE() AS DATE));
            SELECT DestLocal AS Local,
              RepoHoy     = SUM(CASE WHEN ((F=@ayer AND H>='21:00:00') OR (F=@hoy AND H<'05:00:00')) AND Mot<>'13' AND LEFT(Art,1)<>'Z' THEN Cant ELSE 0 END),
              RefuerzoHoy = SUM(CASE WHEN F=@hoy AND H>='05:00:00' AND H<'21:00:00' AND Mot<>'13' AND LEFT(Art,1)<>'Z' THEN Cant ELSE 0 END)
            FROM (
              SELECT DestLocal=UPPER(RTRIM(C.FCLIENTE)), F=CAST(C.FALTAFW AS DATE), H=RTRIM(C.HALTAFW), Mot=RTRIM(C.MOTIVO), Art=RTRIM(D.FART), Cant=D.FCANT*C.SIGNOMOV
              FROM DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV C
              JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
              WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE)) IN('LURO','PERALTA') AND CAST(C.FALTAFW AS DATE) IN(@ayer,@hoy)
              UNION ALL
              SELECT UPPER(RTRIM(C.FCLIENTE)), CAST(C.FALTAFW AS DATE), RTRIM(C.HALTAFW), RTRIM(C.MOTIVO), RTRIM(D.FART), D.FCANT*C.SIGNOMOV
              FROM DRAGONFISH_CCENTRAL.Zoologic.COMPROBANTEV C
              JOIN DRAGONFISH_CCENTRAL.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
              WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE)) IN('LURO','PERALTA') AND CAST(C.FALTAFW AS DATE) IN(@ayer,@hoy)
            ) z GROUP BY DestLocal;
            """;
        try
        {
            foreach (var r in await cn.QueryAsync<DespDiaRow>(new CommandDefinition(sqlDesp, commandTimeout: 60, cancellationToken: ct)))
                if (res.DiaOp.TryGetValue(r.Local, out var d)) { d.RepoHoy = (int)r.RepoHoy; d.RefuerzoHoy = (int)r.RefuerzoHoy; }
        }
        catch { /* despachos caídos */ }

        // --- Venta ayer / devol ayer / venta hoy / cobertura: por local vivo (OPENQUERY) ---
        foreach (var loc in LocalesRepo)
        {
            res.Cobertura[loc] = await CoberturaAsync(cn, loc, ct);

            if (!HostVivo(HostDe(loc))) continue;
            var host = HostDe(loc);
            var db = "DRAGONFISH_" + loc;

            var qVentaAyer = $"SELECT ISNULL(SUM(D.FCANT*C.SIGNOMOV),0) AS n FROM {db}.Zoologic.COMPROBANTEV C JOIN {db}.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO WHERE C.ANULADO=0 AND C.FLETRA NOT IN('R','X') AND C.FFCH >= '{F(ayer0)}' AND C.FFCH < '{F(hoy0)}' AND LEFT(RTRIM(D.FART),1)<>'Z'";
            var qDevolAyer = $"SELECT ISNULL(SUM(Cant),0) AS n FROM (SELECT D.FCANT AS Cant FROM {db}.Zoologic.COMPROBANTEV C JOIN {db}.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE)) IN('CENTRAL','CCENTRAL') AND RTRIM(C.MOTIVO)='07' AND LEFT(RTRIM(D.FART),1)<>'Z' AND C.FALTAFW >= '{F(ayer0)}' AND C.FALTAFW < '{F(hoy0)}' UNION ALL SELECT Dt.CANTI FROM {db}.Zoologic.MSTOCK M JOIN {db}.Zoologic.DETMSTOCK Dt ON M.CODIGO=Dt.NUMR WHERE M.ANULADO=0 AND RTRIM(M.MOTIVO)='07' AND LEFT(RTRIM(Dt.MART),1)<>'Z' AND M.FECHA >= '{F(ayer0)}' AND M.FECHA < '{F(hoy0)}') z";
            var qVentaHoy = $"SELECT ISNULL(SUM(D.FCANT*C.SIGNOMOV),0) AS n FROM {db}.Zoologic.COMPROBANTEV C JOIN {db}.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO WHERE C.ANULADO=0 AND C.FLETRA NOT IN('R','X') AND C.FFCH >= '{F(hoy0)}' AND C.FFCH < '{F(manana0)}' AND LEFT(RTRIM(D.FART),1)<>'Z'";

            try { res.DiaOp[loc].VentaAyer = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(OpenQ(host, qVentaAyer), commandTimeout: 30, cancellationToken: ct)) ?? 0; } catch { }
            try { res.DiaOp[loc].DevolAyer = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(OpenQ(host, qDevolAyer), commandTimeout: 30, cancellationToken: ct)) ?? 0; } catch { }
            try { res.VentaHoy[loc] = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(OpenQ(host, qVentaHoy), commandTimeout: 30, cancellationToken: ct)) ?? 0; } catch { }
        }

        // % devoluciones = devol_ayer / (repo_hoy + refuerzo_hoy)
        foreach (var loc in LocalesRepo)
        {
            var d = res.DiaOp[loc];
            var desp = d.RepoHoy + d.RefuerzoHoy;
            d.DevolPct = desp != 0 ? (double)d.DevolAyer / desp * 100 : 0;
        }
        return res;
    }

    private async Task<RepoCoberturaDto> CoberturaAsync(Microsoft.Data.SqlClient.SqlConnection cn, string local, CancellationToken ct)
    {
        var cov = new RepoCoberturaDto();

        // 1) timestamp de la corrida productiva
        DateTime? corrida = null;
        try
        {
            corrida = await cn.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                "SELECT MAX(FechaHoraCorrida) FROM MARKET.dbo.Reposicion WHERE MachineName='DESKTOP-PGIO2QP' AND ISNULL(Eliminado,0)=0",
                cancellationToken: ct));
        }
        catch { }
        cov.Corrida = corrida;
        if (corrida is null) return cov;

        // 2) despachado real por artículo desde la corrida (CENTRAL+CCENTRAL, ts FALTAFW+HALTAFW)
        const string tsExpr = "DATEADD(SECOND, DATEDIFF(SECOND, 0, CAST(NULLIF(RTRIM(C.HALTAFW),'') AS TIME)), CAST(C.FALTAFW AS DATETIME))";
        var sqlDesp = $"""
            SELECT ARTCOD AS ArtCod, SUM(Cant) AS Desp FROM (
              SELECT ARTCOD=COALESCE(RTRIM(E.CARTICUL), RTRIM(D.FART)), Cant=D.FCANT*C.SIGNOMOV
              FROM DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV C
              JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
              LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.EQUI E ON RTRIM(E.CCODIGO)=RTRIM(D.FART)
              WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE))=@local AND {tsExpr} > @corrida AND LEFT(RTRIM(D.FART),1)<>'Z'
              UNION ALL
              SELECT COALESCE(RTRIM(E.CARTICUL), RTRIM(D.FART)), D.FCANT*C.SIGNOMOV
              FROM DRAGONFISH_CCENTRAL.Zoologic.COMPROBANTEV C
              JOIN DRAGONFISH_CCENTRAL.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
              LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.EQUI E ON RTRIM(E.CCODIGO)=RTRIM(D.FART)
              WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE))=@local AND {tsExpr} > @corrida AND LEFT(RTRIM(D.FART),1)<>'Z'
            ) z GROUP BY ARTCOD;
            """;
        var despachado = new Dictionary<string, int>();
        try
        {
            foreach (var r in await cn.QueryAsync<DespRow>(new CommandDefinition(sqlDesp, new { local, corrida }, commandTimeout: 60, cancellationToken: ct)))
                despachado[r.ArtCod] = (int)r.Desp;
        }
        catch { /* CENTRAL/CCENTRAL caído → despachado parcial */ }

        // 2.5) Δventa: venta del local desde la corrida (EN VIVO por OPENQUERY)
        var deltaVenta = new Dictionary<string, int>();
        if (HostVivo(HostDe(local)))
        {
            var db = "DRAGONFISH_" + local;
            var corridaStr = corrida.Value.ToString("yyyy-MM-dd HH:mm:ss");
            var inner = $"SELECT COALESCE(RTRIM(E.CARTICUL), RTRIM(D.FART)) AS ARTCOD, SUM(D.FCANT*C.SIGNOMOV) AS Dv FROM {db}.Zoologic.COMPROBANTEV C JOIN {db}.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO LEFT JOIN {db}.Zoologic.EQUI E ON RTRIM(E.CCODIGO)=RTRIM(D.FART) WHERE C.ANULADO=0 AND C.FLETRA NOT IN('R','X') AND C.FFCH > '{corridaStr}' AND LEFT(RTRIM(D.FART),1)<>'Z' GROUP BY COALESCE(RTRIM(E.CARTICUL), RTRIM(D.FART))";
            try
            {
                foreach (var r in await cn.QueryAsync<DvRow>(new CommandDefinition(OpenQ(HostDe(local), inner), commandTimeout: 30, cancellationToken: ct)))
                    deltaVenta[r.ArtCod] = (int)r.Dv;
            }
            catch { /* local caído */ }
        }

        // 3) universo REPO-ABLE: mapeados en depósito con Pendiente (RepoResto)
        const string sqlUniv = """
            WITH dep AS (
              SELECT R.ARTCOD,
                Pasillo = (SELECT TOP 1 RTRIM(M2.Pasillo) FROM MARKET.dbo.MapeoRegistro R2 INNER JOIN MARKET.dbo.Mapeo M2 ON R2.IDMapeo=M2.ID WHERE M2.IDUbicacion=1 AND M2.Eliminado=0 AND R2.Eliminado=0 AND RTRIM(R2.ARTCOD)=RTRIM(R.ARTCOD) ORDER BY M2.Pasillo, M2.Modulo, M2.Posicion),
                Modulo  = (SELECT TOP 1 RTRIM(M2.Modulo) FROM MARKET.dbo.MapeoRegistro R2 INNER JOIN MARKET.dbo.Mapeo M2 ON R2.IDMapeo=M2.ID WHERE M2.IDUbicacion=1 AND M2.Eliminado=0 AND R2.Eliminado=0 AND RTRIM(R2.ARTCOD)=RTRIM(R.ARTCOD) ORDER BY M2.Pasillo, M2.Modulo, M2.Posicion)
              FROM (SELECT DISTINCT ARTCOD=RTRIM(R.ARTCOD) FROM MARKET.dbo.MapeoRegistro R INNER JOIN MARKET.dbo.Mapeo M ON R.IDMapeo=M.ID WHERE M.IDUbicacion=1 AND M.Eliminado=0 AND R.Eliminado=0 AND ISNULL(M.Mobiliario,'') NOT LIKE '%DISCONTINUO%') R
            )
            SELECT RTRIM(rr.ARTCOD) AS ArtCod, RTRIM(ART.ARTDES) AS ArtDes,
              ISNULL((SELECT TOP 1 ADX.CantPack FROM MARKET.dbo.ArticulosDatosAdiciones ADX WHERE RTRIM(ADX.ARTCOD)=RTRIM(rr.ARTCOD) AND ADX.Eliminado=0 ORDER BY ADX.ID DESC), 1) AS CantPack,
              ISNULL(rr.Pendiente, 0) AS Pendiente, dep.Pasillo, dep.Modulo
            FROM MARKET.dbo.RepoResto rr
            INNER JOIN dep ON dep.ARTCOD = RTRIM(rr.ARTCOD)
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(rr.ARTCOD)
            WHERE rr.Local=@local AND ISNULL(rr.Eliminado,0)=0;
            """;
        List<UnivRow> rows;
        try { rows = (await cn.QueryAsync<UnivRow>(new CommandDefinition(sqlUniv, new { local }, commandTimeout: 90, cancellationToken: ct))).ToList(); }
        catch { return cov; }

        int verde = 0, ambar = 0, rojo = 0;
        var rojos = new List<RepoRojoItemDto>();
        foreach (var r in rows)
        {
            var pack = r.CantPack <= 0 ? 1.0 : r.CantPack;
            double pend = r.Pendiente
                       - (despachado.TryGetValue(r.ArtCod, out var dsp) ? dsp : 0)
                       + (deltaVenta.TryGetValue(r.ArtCod, out var dv) ? dv : 0);
            var pct = pend / pack * 100;
            if (pct < 80) verde++;
            else if (pct < 100) ambar++;
            else
            {
                rojo++;
                rojos.Add(new RepoRojoItemDto
                {
                    ArtCod = r.ArtCod,
                    ArtDes = (r.ArtDes ?? "").Trim(),
                    Pendientes = (int)Math.Round(pend),
                    Pack = (int)pack,
                    PacksAEnviar = pend > 0 ? (int)Math.Ceiling(pend / pack) : 0,
                    Modulo = (r.Modulo ?? "").Trim(),
                    Pasillo = (r.Pasillo ?? "").Trim(),
                    Pct = pct
                });
            }
        }
        cov.Verde = verde; cov.Ambar = ambar; cov.Rojo = rojo;
        cov.Mapeados = verde + ambar + rojo;
        cov.PctRojo = cov.Mapeados > 0 ? (double)rojo / cov.Mapeados * 100 : 0;
        cov.ItemsRojos = rojos.OrderBy(x => x.Pasillo.Length == 0).ThenBy(x => x.Pasillo).ThenBy(x => x.Modulo).ToList();
        return cov;
    }

    private async Task<Dictionary<string, RepoAbastDto>> AbastQueryAsync(CancellationToken ct)
    {
        var outp = new Dictionary<string, RepoAbastDto> { ["LURO"] = new(), ["PERALTA"] = new() };
        using var cn = _db.Create();
        var hoy0 = DateTime.Now.Date;
        string F(DateTime d) => d.ToString("yyyy-MM-dd HH:mm:ss");

        // E: enviado CENTRAL+CCENTRAL (-7..0), local
        const string sqlE = """
            SET NOCOUNT ON;
            DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
            DECLARE @desde DATETIME = DATEADD(DAY,-7,@hoy), @hasta DATETIME = @hoy;
            SELECT Local, SUM(Cant) AS N FROM (
              SELECT Local=UPPER(RTRIM(C.FCLIENTE)), Cant=D.FCANT*C.SIGNOMOV
              FROM DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEV C JOIN DRAGONFISH_CENTRAL.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
              WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE)) IN('LURO','PERALTA') AND RTRIM(C.MOTIVO)<>'13' AND LEFT(RTRIM(D.FART),1)<>'Z' AND C.FALTAFW >= @desde AND C.FALTAFW < @hasta
              UNION ALL
              SELECT UPPER(RTRIM(C.FCLIENTE)), D.FCANT*C.SIGNOMOV
              FROM DRAGONFISH_CCENTRAL.Zoologic.COMPROBANTEV C JOIN DRAGONFISH_CCENTRAL.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
              WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE)) IN('LURO','PERALTA') AND RTRIM(C.MOTIVO)<>'13' AND LEFT(RTRIM(D.FART),1)<>'Z' AND C.FALTAFW >= @desde AND C.FALTAFW < @hasta
            ) z GROUP BY Local;
            """;
        var env = new Dictionary<string, int> { ["LURO"] = 0, ["PERALTA"] = 0 };
        try { foreach (var r in await cn.QueryAsync<EnvRow>(new CommandDefinition(sqlE, commandTimeout: 30, cancellationToken: ct))) if (env.ContainsKey(r.Local)) env[r.Local] = (int)r.N; }
        catch { /* CENTRAL/CCENTRAL caído */ }

        var envDesde = F(hoy0.AddDays(-7));
        var envHasta = F(hoy0);
        var venDesde = F(hoy0.AddDays(-8));
        var venHasta = F(hoy0.AddDays(-1));
        foreach (var loc in LocalesRepo)
        {
            if (!HostVivo(HostDe(loc))) { outp[loc] = new RepoAbastDto { EnvNeto = env[loc], Venta = 0, Abast = env[loc] }; continue; }
            var host = HostDe(loc);
            var db = "DRAGONFISH_" + loc;

            var qV = $"SELECT ISNULL(SUM(D.FCANT*C.SIGNOMOV),0) AS n FROM {db}.Zoologic.COMPROBANTEV C JOIN {db}.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO WHERE C.ANULADO=0 AND C.FLETRA NOT IN('R','X') AND LEFT(RTRIM(D.FART),1)<>'Z' AND C.FFCH >= '{venDesde}' AND C.FFCH < '{venHasta}'";
            var qDcv = $"SELECT ISNULL(SUM(D.FCANT),0) AS n FROM {db}.Zoologic.COMPROBANTEV C JOIN {db}.Zoologic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO WHERE C.FLETRA='R' AND C.ANULADO=0 AND UPPER(RTRIM(C.FCLIENTE)) IN('CENTRAL','CCENTRAL') AND RTRIM(C.MOTIVO)='07' AND LEFT(RTRIM(D.FART),1)<>'Z' AND C.FALTAFW >= '{envDesde}' AND C.FALTAFW < '{envHasta}'";
            var qDms = $"SELECT ISNULL(SUM(Dt.CANTI),0) AS n FROM {db}.Zoologic.MSTOCK M JOIN {db}.Zoologic.DETMSTOCK Dt ON M.CODIGO=Dt.NUMR WHERE M.ANULADO=0 AND RTRIM(M.MOTIVO)='07' AND LEFT(RTRIM(Dt.MART),1)<>'Z' AND M.FECHA >= '{envDesde}' AND M.FECHA < '{envHasta}'";

            int venta = 0, dCv = 0, dMs = 0;
            try { venta = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(OpenQ(host, qV), commandTimeout: 30, cancellationToken: ct)) ?? 0; } catch { }
            try { dCv = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(OpenQ(host, qDcv), commandTimeout: 30, cancellationToken: ct)) ?? 0; } catch { }
            try { dMs = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(OpenQ(host, qDms), commandTimeout: 30, cancellationToken: ct)) ?? 0; } catch { }

            var envNeto = env[loc] - (dCv + dMs);
            outp[loc] = new RepoAbastDto { EnvNeto = envNeto, Venta = venta, Abast = envNeto - venta };
        }
        return outp;
    }
}
